// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using ElCamino.IdentityServer4.AzureStorage.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.Cosmos.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ElCamino.IdentityServer4.AzureStorage.Entities;
using ElCamino.IdentityServer4.AzureStorage.Helpers;
using ElCamino.IdentityServer4.AzureStorage.Mappers;

namespace ElCamino.IdentityServer4.AzureStorage.Contexts
{
    public class PersistedGrantStorageContext : StorageContext
    {
        private PersistedGrantStorageConfig _config = null;
        private string BlobContainerName = string.Empty;


        public string PersistedGrantTableName { get; private set; }

        public CloudTable PersistedGrantTable { get; private set; }

        public CloudTableClient TableClient { get; private set; }

        public CloudBlobClient BlobClient { get; private set; }

        public CloudBlobContainer PersistedGrantBlobContainer { get; private set; }


        public PersistedGrantStorageContext(IOptions<PersistedGrantStorageConfig> config) : this(config.Value)
        {
        }


        public PersistedGrantStorageContext(PersistedGrantStorageConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }
            Initialize(config);
        }

        public async Task<bool> CreateStorageIfNotExists()
        {
            var tasks = new Task<bool>[] { PersistedGrantTable.CreateIfNotExistsAsync(),
                PersistedGrantBlobContainer.CreateIfNotExistsAsync()};
            await Task.WhenAll(tasks).ConfigureAwait(false);
            return tasks.Select(t => t.Result).All(a => a);
        }

        protected virtual void Initialize(PersistedGrantStorageConfig config)
        {
            _config = config;
            TableClient = Microsoft.Azure.Cosmos.Table.CloudStorageAccount.Parse(_config.StorageConnectionString).CreateCloudTableClient();
            TableClient.DefaultRequestOptions.PayloadFormat = TablePayloadFormat.Json;
            PersistedGrantTableName = config.PersistedGrantTableName;

            if (string.IsNullOrWhiteSpace(PersistedGrantTableName))
            {
                throw new ArgumentException($"PersistedGrantTableName cannot be null or empty, check your configuration.", nameof(config.PersistedGrantTableName));
            }

            PersistedGrantTable = TableClient.GetTableReference(PersistedGrantTableName);

            BlobClient = Microsoft.Azure.Storage.CloudStorageAccount.Parse(_config.StorageConnectionString).CreateCloudBlobClient();
            BlobContainerName = config.BlobContainerName;
            if (string.IsNullOrWhiteSpace(BlobContainerName))
            {
                throw new ArgumentException($"BlobContainerName cannot be null or empty, check your configuration.", nameof(config.BlobContainerName));
            }
            PersistedGrantBlobContainer = BlobClient.GetContainerReference(BlobContainerName);

        }

        public async Task<IEnumerable<PersistedGrantTblEntity>> GetExpiredAsync(int maxResults)
        {
            TableQuery<PersistedGrantTblEntity> tq = new TableQuery<PersistedGrantTblEntity>();

            tq.FilterString = TableQuery.GenerateFilterConditionForDate("Expiration",
                QueryComparisons.LessThan,
                DateTimeOffset.UtcNow);
            tq.TakeCount = maxResults;
            return (await GetAllByTableQueryAsync(tq, PersistedGrantTable).ConfigureAwait(false)).ToArray();

        }

        public async Task<bool> RemoveAsync(string key)
        {
            CloudTable table = PersistedGrantTable;
            PersistedGrantTblEntity keyEntity = await GetEntityTableAsync<PersistedGrantTblEntity>(key, PersistedGrantTable);
            if (keyEntity != null)
            {
                var entities = keyEntity.ToModel().ToEntities();
                await Task.WhenAll(GetAndDeleteTableEntityByKeysAsync(entities.subjectGrant.PartitionKey, entities.subjectGrant.RowKey, PersistedGrantTable),
                        table.ExecuteAsync(TableOperation.Delete(keyEntity)),
                        DeleteBlobAsync(key, PersistedGrantBlobContainer)).ConfigureAwait(false);
                return true;
            }
            return false;
        }

       

       
    }
}
