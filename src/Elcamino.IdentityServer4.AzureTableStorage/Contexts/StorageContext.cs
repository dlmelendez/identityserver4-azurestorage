// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using ElCamino.IdentityServer4.AzureStorage.Helpers;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ElCamino.IdentityServer4.AzureStorage.Contexts
{
    public class StorageContext
    {
        protected StorageContext() { }

        public async Task<string> GetBlobContentAsync(string keyNotHashed, CloudBlobContainer container)
        {
            CloudBlockBlob blob = container.GetBlockBlobReference(KeyGeneratorHelper.GenerateHashValue(keyNotHashed));
            return await GetBlobContentAsync(blob);
        }

        public async Task<string> GetBlobContentAsync(CloudBlockBlob blob)
        {
            try
            {
                return await blob.DownloadTextAsync();
            }
            catch (Microsoft.Azure.Storage.StorageException storageEx)
            {
                if (!(storageEx.RequestInformation.ErrorCode == Microsoft.Azure.Storage.Blob.Protocol.BlobErrorCodeStrings.BlobNotFound ))
                {
                    throw; 
                }
            }
            
            return string.Empty;
        }

        public async Task<string> UpdateBlobCacheFileAsync<Entity>(IEnumerable<Entity> entities, CloudBlobContainer cacheContainer)
        {
            DateTime dateTimeNow = DateTime.UtcNow;
            string blobName = KeyGeneratorHelper.GenerateDateTimeDecendingId(dateTimeNow);
            await SaveBlobAsync(blobName, JsonConvert.SerializeObject(entities), cacheContainer);
            return blobName;
        }

        public async Task<IEnumerable<Entity>> GetLatestFromCacheBlobAsync<Entity>(CloudBlobContainer cacheContainer)
        {
            CloudBlockBlob blob = await GetFirstBlobAsync(cacheContainer);
            if (blob != null)
            {
                var entities = await GetEntityBlobAsync<List<Entity>>(blob);
                if (entities != null)
                {
                    return entities;
                }
            }
            return null;
        }

        public async Task<Entity> GetEntityBlobAsync<Entity>(string keyNotHashed, CloudBlobContainer container) where Entity : class, new()
        {
            CloudBlockBlob blob = container.GetBlockBlobReference(KeyGeneratorHelper.GenerateHashValue(keyNotHashed));
            return await GetEntityBlobAsync<Entity>(blob);
        }

        public async Task<Entity> GetEntityBlobAsync<Entity>(CloudBlockBlob blobJson) where Entity : class, new()
        {
            try
            {
                using (Stream s = await blobJson.OpenReadAsync())
                {
                    using (StreamReader sr = new StreamReader(s))
                    {
                        using (JsonReader reader = new JsonTextReader(sr))
                        {
                            JsonSerializer serializer = new JsonSerializer();
                            return serializer.Deserialize<Entity>(reader);
                        }
                    }
                }
            }
            catch (Microsoft.Azure.Storage.StorageException storageEx)
            {
                if (!(storageEx.RequestInformation.ErrorCode == Microsoft.Azure.Storage.Blob.Protocol.BlobErrorCodeStrings.BlobNotFound))
                {
                    throw;
                }
            }
            return null;
        }

        public async Task<IEnumerable<Entity>> GetAllBlobEntitiesAsync<Entity>(CloudBlobContainer container, ILogger logger) where Entity : class, new()
        {

            var entityTasks = (await GetAllBlobsAsync(container).ConfigureAwait(false))
                .Select(blobJson =>
                {
                    return GetEntityBlobAsync<Entity>(blobJson);
                }).ToArray(); //.ToArray actually speeds up the task processing


            await Task.WhenAll(entityTasks);
            return entityTasks.Where(w => w?.Result != null).Select(s => s.Result);
        }

        public Task<IEnumerable<CloudBlockBlob>> GetAllBlobsAsync(CloudBlobContainer container)
        {
            return Task.Run<IEnumerable<CloudBlockBlob>>(() => GetAllBlobs(container));
        }

        public IEnumerable<CloudBlockBlob> GetAllBlobs(CloudBlobContainer container)
        {
            BlobContinuationToken token = new BlobContinuationToken();
            while (token != null)
            {
                var blobSegment = container.ListBlobsSegmented(string.Empty, true, 
                    BlobListingDetails.None, 100, 
                    token, 
                    new BlobRequestOptions() , 
                    new Microsoft.Azure.Storage.OperationContext() );

                foreach (var blobItem in blobSegment.Results)
                {
                    CloudBlockBlob blockBlob = blobItem as CloudBlockBlob;
                    if (blockBlob != null)
                    {
                        yield return blockBlob;
                    }
                }
                token = blobSegment.ContinuationToken;
            }
        }

        public async Task<CloudBlockBlob> GetFirstBlobAsync(CloudBlobContainer container)
        {
            BlobContinuationToken token = new BlobContinuationToken();
            var blobSegment = await container.ListBlobsSegmentedAsync(string.Empty, true,
                BlobListingDetails.None, 1,
                token,
                new BlobRequestOptions(),
                new Microsoft.Azure.Storage.OperationContext());

            CloudBlockBlob blockBlob = blobSegment.Results.FirstOrDefault() as CloudBlockBlob;
            if (blockBlob != null)
            {
                return blockBlob;
            }
            return null;
        }

        public async Task DeleteBlobAsync(string keyNotHashed, CloudBlobContainer container)
        {
            CloudBlockBlob blob = container.GetBlockBlobReference(KeyGeneratorHelper.GenerateHashValue(keyNotHashed));
            await blob.DeleteIfExistsAsync();
        }

        public async Task SaveBlobWithHashedKeyAsync(string keyNotHashed, string jsonEntityContent, CloudBlobContainer container)
        {
            CloudBlockBlob blob = container.GetBlockBlobReference(KeyGeneratorHelper.GenerateHashValue(keyNotHashed));
            blob.Properties.ContentType = "application/json";

            await blob.UploadTextAsync(jsonEntityContent,
                Encoding.UTF8,
                Microsoft.Azure.Storage.AccessCondition.GenerateEmptyCondition(),
                new BlobRequestOptions() { },
                new Microsoft.Azure.Storage.OperationContext() { },
                new System.Threading.CancellationToken());
            await blob.SetPropertiesAsync();
        }

        public async Task SaveBlobAsync(string blobName, string jsonEntityContent, CloudBlobContainer container)
        {
            CloudBlockBlob blob = container.GetBlockBlobReference(blobName);
            blob.Properties.ContentType = "application/json";

            await blob.UploadTextAsync(jsonEntityContent,
                Encoding.UTF8,
                Microsoft.Azure.Storage.AccessCondition.GenerateEmptyCondition(),
                new BlobRequestOptions() { },
                new Microsoft.Azure.Storage.OperationContext() { },
                new System.Threading.CancellationToken());
            await blob.SetPropertiesAsync();
        }

       

        /// <summary>
        /// Get table entity where the partition and row key are the hash of the entity key provided
        /// </summary>
        /// <typeparam name="Entity"></typeparam>
        /// <param name="keyNotHashed">Key of the entity (not hashed)</param>
        /// <returns></returns>
        public async Task<Entity> GetEntityTableAsync<Entity>(string keyNotHashed, CloudTable table) 
            where Entity : class, ITableEntity, new()
        {
            string hashedKey = KeyGeneratorHelper.GenerateHashValue(keyNotHashed);
            var r = await table.ExecuteAsync(TableOperation.Retrieve<Entity>(hashedKey, hashedKey));
            return r.Result as Entity;
        }

        public async Task GetAndDeleteTableEntityByKeysAsync(string partitionKey, string rowKey, CloudTable table)
        {
            var entity = (await table.ExecuteAsync(TableOperation.Retrieve(partitionKey, rowKey))).Result as ITableEntity;
            if (entity != null)
            {
                await table.ExecuteAsync(TableOperation.Delete(entity));
            }

        }

        
        /// <summary>
        /// Performs better than using ExecuteQuerySegmentedAsync and building an internal list
        /// </summary>
        /// <typeparam name="Entity"></typeparam>
        /// <param name="tableQuery"></param>
        /// <param name="table"></param>
        /// <returns></returns>
        public Task<IEnumerable<Entity>> GetAllByTableQueryAsync<Entity>(TableQuery<Entity> tableQuery, CloudTable table)
           where Entity : class, ITableEntity, new()
        {
            return Task.Run<IEnumerable<Entity>>(() => GetAllByTableQuery(tableQuery, table));
        }

        public IEnumerable<Entity> GetAllByTableQuery<Entity>(TableQuery<Entity> tableQuery, CloudTable table)
           where Entity : class, ITableEntity, new()
        {
            TableContinuationToken continuationToken = new TableContinuationToken();
            while (continuationToken != null)
            {
                var tableResults = table.ExecuteQuerySegmented<Entity>(tableQuery, continuationToken);
                foreach (var result in tableResults.Results)
                {
                    yield return result;
                }
                continuationToken = tableResults.ContinuationToken;
            }
        }
    }

}
