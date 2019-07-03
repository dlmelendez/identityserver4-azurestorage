// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using ElCamino.IdentityServer4.AzureStorage.Helpers;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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
            if (await blob.ExistsAsync())
            {
                return await blob.DownloadTextAsync();
            }
            return string.Empty;
        }

        public async Task<Entity> GetEntityBlobAsync<Entity>(string keyNotHashed, CloudBlobContainer container) where Entity : class, new()
        {
            CloudBlockBlob blob = container.GetBlockBlobReference(KeyGeneratorHelper.GenerateHashValue(keyNotHashed));
            return await GetEntityBlobAsync<Entity>(blob);
        }

        public async Task<Entity> GetEntityBlobAsync<Entity>(CloudBlockBlob blobJson) where Entity : class, new()
        {
            string clientJson = await GetBlobContentAsync(blobJson);

            if (!string.IsNullOrWhiteSpace(clientJson))
            {
                return JsonConvert.DeserializeObject<Entity>(clientJson);
            }

            return null;
        }

        public async Task<IEnumerable<Entity>> GetAllBlobEntitiesAsync<Entity>(CloudBlobContainer container, ILogger logger) where Entity : class, new()
        {
            List<Task<Entity>> entityTasks = new List<Task<Entity>>(100);

            BlobContinuationToken token = new BlobContinuationToken();
            while (token != null)
            {
                var blobJsonSegment = await container.ListBlobsSegmentedAsync(token);

                foreach (var blobJsonItem in blobJsonSegment.Results)
                {
                    CloudBlockBlob blobJson = blobJsonItem as CloudBlockBlob;
                    if (blobJson != null)
                    {
                        try
                        {
                            entityTasks.Add(GetEntityBlobAsync<Entity>(blobJson));
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, $"Blob {blobJson.Name} invalid client object found in blob storage.");
                        }
                    }
                }
                token = blobJsonSegment.ContinuationToken;
            }

            await Task.WhenAll(entityTasks);
            return entityTasks.Where(w => w?.Result != null).Select(s => s.Result).ToArray();
        }
        public async Task DeleteBlobAsync(string keyNotHashed, CloudBlobContainer container)
        {
            CloudBlockBlob blob = container.GetBlockBlobReference(KeyGeneratorHelper.GenerateHashValue(keyNotHashed));
            await blob.DeleteIfExistsAsync();
        }

        public async Task SaveBlobAsync(string keyNotHashed, string jsonEntityContent, CloudBlobContainer container)
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

        public async Task GetAndDeleteTableEntityByKeys(string partitionKey, string rowKey, CloudTable table)
        {
            var entity = (await table.ExecuteAsync(TableOperation.Retrieve(partitionKey, rowKey))).Result as ITableEntity;
            if (entity != null)
            {
                await table.ExecuteAsync(TableOperation.Delete(entity));
            }

        }

        public async Task<IEnumerable<Entity>> GetAllByTableQueryAsync<Entity>(TableQuery<Entity> tableQuery, CloudTable table)
            where Entity : class, ITableEntity, new()
        {
            List<Entity> results = new List<Entity>(100);
            TableContinuationToken continuationToken = new TableContinuationToken();
            while (continuationToken != null)
            {
                var tableResults = await table.ExecuteQuerySegmentedAsync<Entity>(tableQuery, continuationToken);
                results.AddRange(tableResults.Results);
                continuationToken = tableResults.ContinuationToken;
            }

            return results;
        }

    }

}
