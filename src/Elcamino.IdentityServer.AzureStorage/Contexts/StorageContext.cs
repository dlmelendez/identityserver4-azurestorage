// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using ElCamino.IdentityServer.AzureStorage.Helpers;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs.Models;
using Azure.Data.Tables;
using System.Text.Json;
using Azure.Data.Tables.Models;
using System.Threading;
using System.Runtime.CompilerServices;

namespace ElCamino.IdentityServer.AzureStorage.Contexts
{
    public class StorageContext
    {
        protected StorageContext() { }

        public virtual JsonSerializerOptions JsonSerializerDefaultOptions => new(JsonSerializerDefaults.Web);

        public async Task<string> GetBlobContentAsync(string keyNotHashed, BlobContainerClient container, CancellationToken cancellationToken = default)
        {
            BlobClient blob = container.GetBlobClient(KeyGeneratorHelper.GenerateHashValue(keyNotHashed));
            return await GetBlobContentAsync(blob, cancellationToken).ConfigureAwait(false);
        }

        public async Task<string> GetBlobContentAsync(BlobClient blob, CancellationToken cancellationToken = default)
        {
            try
            {
                Response<BlobDownloadInfo> download = await blob.DownloadAsync(cancellationToken).ConfigureAwait(false);
                using (StreamReader sr = new StreamReader(download.Value.Content, Encoding.UTF8))
                {
                    return await sr.ReadToEndAsync().ConfigureAwait(false);
                }
            }
            catch (RequestFailedException ex)
               when (ex.ErrorCode == BlobErrorCode.BlobNotFound)
            {
                return string.Empty;
            }

        }

        public async Task<(string blobName, int count)> UpdateBlobCacheFileAsync<Entity>(IEnumerable<Entity> entities, BlobContainerClient cacheContainer, CancellationToken cancellationToken = default)
        {
            DateTime dateTimeNow = DateTime.UtcNow;
            string blobName = KeyGeneratorHelper.GenerateDateTimeDecendingId(dateTimeNow);
            await SaveBlobAsync(blobName, JsonSerializer.Serialize<IEnumerable<Entity>>(entities, JsonSerializerDefaultOptions), cacheContainer, cancellationToken)
                .ConfigureAwait(false);
            return (blobName, count: entities.Count());
        }

        public async Task<(string blobName, int count)> UpdateBlobCacheFileAsync<Entity>(IAsyncEnumerable<Entity> entities, BlobContainerClient cacheContainer, CancellationToken cancellationToken = default)
        {
            return await UpdateBlobCacheFileAsync(await entities.ToListAsync(cancellationToken).ConfigureAwait(false), cacheContainer, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes all blobs older than the latestBlobName by name comparsion
        /// </summary>
        /// <param name="latestBlobName"></param>
        /// <param name="cacheContainer"></param>
        /// <returns></returns>
        public async Task DeleteBlobCacheFilesAsync(string latestBlobName, BlobContainerClient cacheContainer, ILogger logger, CancellationToken cancellationToken = default)
        {
            await foreach (BlobItem blobName in cacheContainer.GetBlobsAsync(BlobTraits.None, BlobStates.None, cancellationToken: cancellationToken))
            {
                if (String.Compare(latestBlobName, blobName.Name) == -1)
                {
                    BlobClient blobClient = cacheContainer.GetBlobClient(blobName.Name);
                    await blobClient.DeleteAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                    logger.LogInformation($"container: {cacheContainer.Name} blob: {blobName.Name} - cache file deleted");
                }
            }
        }

        public async Task<IEnumerable<Entity>> GetLatestFromCacheBlobAsync<Entity>(BlobContainerClient cacheContainer, CancellationToken cancellationToken = default)
        {
            BlobClient blob = await GetFirstBlobAsync(cacheContainer, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (blob != null)
            {
                var entities = await GetEntityBlobAsync<List<Entity>>(blob, cancellationToken).ConfigureAwait(false);
                if (entities != null)
                {
                    return entities;
                }
            }
            return null;
        }

        public async Task<Entity> GetEntityBlobAsync<Entity>(string keyNotHashed, BlobContainerClient container, CancellationToken cancellationToken = default) where Entity : class, new()
        {
            BlobClient blob = container.GetBlobClient(KeyGeneratorHelper.GenerateHashValue(keyNotHashed));
            return await GetEntityBlobAsync<Entity>(blob, cancellationToken).ConfigureAwait(false);
        }

        public async Task<Entity> GetEntityBlobAsync<Entity>(BlobClient blobJson, CancellationToken cancellationToken = default) where Entity : class, new()
        {
            try
            {
                var download = await blobJson.DownloadAsync(cancellationToken).ConfigureAwait(false);
                using (Stream s = download.Value.Content)
                {
                    return JsonSerializer.Deserialize<Entity>(s, JsonSerializerDefaultOptions);
                }
            }
            catch (RequestFailedException ex)
               when (ex.ErrorCode == BlobErrorCode.BlobNotFound)
            {
                return null;
            }
        }

        public async IAsyncEnumerable<Entity> GetAllBlobEntitiesAsync<Entity>(BlobContainerClient container, ILogger logger, [EnumeratorCancellation]CancellationToken cancellationToken = default) where Entity : class, new()
        {
            await foreach(var blobJson in GetAllBlobsAsync(container, cancellationToken).ConfigureAwait(false))
            {
                Entity e = await GetEntityBlobAsync<Entity>(blobJson, cancellationToken).ConfigureAwait(false);
                if (e != null)
                {
                    yield return e;
                }
            }
        }

        /// <summary>
        /// Only gets blob entities that have no serialization errors
        /// </summary>
        /// <typeparam name="Entity"></typeparam>
        /// <param name="container"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        public async IAsyncEnumerable<Entity> SafeGetAllBlobEntitiesAsync<Entity>(BlobContainerClient container, ILogger logger, [EnumeratorCancellation]CancellationToken cancellationToken = default) where Entity : class, new()
        {
            await foreach (var blobJson in GetAllBlobsAsync(container, cancellationToken).ConfigureAwait(false))
            {
                Entity e = null;
                try
                {
                    e = await GetEntityBlobAsync<Entity>(blobJson, cancellationToken).ConfigureAwait(false);                    
                }
                catch(Exception ex)
                {
                    // log and continue
                    logger.LogError(ex, $"{nameof(SafeGetAllBlobEntitiesAsync)}-{nameof(GetEntityBlobAsync)} error:");
                    continue;
                }
                if (e != null)
                {
                    yield return e;
                }
            }
        }

        public async IAsyncEnumerable<BlobClient> GetAllBlobsAsync(BlobContainerClient container, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            AsyncPageable<BlobItem> pageable = container.GetBlobsAsync(BlobTraits.None, BlobStates.None, string.Empty, cancellationToken: cancellationToken);
            IAsyncEnumerable<Page<BlobItem>> pages = pageable.AsPages();
            await foreach (Page<BlobItem> page in pages.ConfigureAwait(false))
            {
                foreach(BlobItem blob in page.Values)
                {
                    yield return container.GetBlobClient(blob.Name);
                }
            }
        }

        public async Task<BlobClient> GetFirstBlobAsync(BlobContainerClient container, CancellationToken cancellationToken = default)
        {
            AsyncPageable<BlobItem> pageable = container.GetBlobsAsync(BlobTraits.None, BlobStates.None, string.Empty, cancellationToken: cancellationToken);
            IAsyncEnumerable<Page<BlobItem>> pages = pageable.AsPages(pageSizeHint:1);

            var blobList = (await pages.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false))?.Values;
            if (blobList.Count > 0)
            {
                BlobItem blob = blobList[0];
                if (blob != null)
                {
                    return container.GetBlobClient(blob.Name);
                }
            }
            return null;
        }       

        public Task DeleteBlobAsync(string keyNotHashed, BlobContainerClient container, CancellationToken cancellationToken = default)
        {
            BlobClient blob = container.GetBlobClient(KeyGeneratorHelper.GenerateHashValue(keyNotHashed));
            return blob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
        }

        public Task SaveBlobWithHashedKeyAsync(string keyNotHashed, string jsonEntityContent, BlobContainerClient container, CancellationToken cancellationToken = default)
        {
            BlobClient blob = container.GetBlobClient(KeyGeneratorHelper.GenerateHashValue(keyNotHashed));

            return blob.UploadAsync(new MemoryStream(Encoding.UTF8.GetBytes(jsonEntityContent)), new BlobHttpHeaders() 
            { 
                ContentType = "application/json"
            }, cancellationToken: cancellationToken);
        }

        public Task SaveBlobAsync(string blobName, string jsonEntityContent, BlobContainerClient container, CancellationToken cancellationToken = default)
        {
            BlobClient blob = container.GetBlobClient(blobName);
            return blob.UploadAsync(new MemoryStream(Encoding.UTF8.GetBytes(jsonEntityContent)), new BlobHttpHeaders()
            {
                ContentType = "application/json"
            }, cancellationToken: cancellationToken); ;
        }
       
        /// <summary>
        /// Get table entity where the partition and row key are the hash of the entity key provided
        /// </summary>
        /// <typeparam name="Entity"></typeparam>
        /// <param name="keyNotHashed">Key of the entity (not hashed)</param>
        /// <returns></returns>
        public static async Task<Entity> GetEntityTableAsync<Entity>(string keyNotHashed, TableClient table, CancellationToken cancellationToken = default) 
            where Entity : class, ITableEntity, new()
        {
            string hashedKey = KeyGeneratorHelper.GenerateHashValue(keyNotHashed);
            return await table.GetEntityOrDefaultAsync<Entity>(hashedKey, hashedKey, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        
        public static async IAsyncEnumerable<Model> GetAllByTableQueryAsync<Entity, Model>(string tableQuery, TableClient table, Func<Entity, Model> mapFunc, [EnumeratorCancellation]CancellationToken cancellationToken = default)
           where Entity : class, ITableEntity, new()
        {
            await foreach (var entity in table.QueryAsync<Entity>(filter: tableQuery, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                yield return mapFunc(entity);
            }
        }
    }

}
