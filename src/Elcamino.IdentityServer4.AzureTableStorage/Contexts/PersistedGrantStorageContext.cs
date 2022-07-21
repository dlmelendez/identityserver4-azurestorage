// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using ElCamino.Duende.IdentityServer.AzureStorage.Configuration;
using Microsoft.Extensions.Options;
using Azure.Storage;
using Azure.Storage.Blobs;
using Microsoft.Azure.Cosmos.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ElCamino.Duende.IdentityServer.AzureStorage.Entities;
using ElCamino.Duende.IdentityServer.AzureStorage.Helpers;
using ElCamino.Duende.IdentityServer.AzureStorage.Mappers;

namespace ElCamino.Duende.IdentityServer.AzureStorage.Contexts
{
    public class PersistedGrantStorageContext : StorageContext
    {
        private string BlobContainerName = string.Empty;


        public string PersistedGrantTableName { get; private set; }

        public CloudTable PersistedGrantTable { get; private set; }

        public CloudTableClient TableClient { get; private set; }

        public BlobServiceClient BlobClient { get; private set; }

        public BlobContainerClient PersistedGrantBlobContainer { get; private set; }


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
            var tasks = new Task[] { PersistedGrantTable.CreateIfNotExistsAsync(),
                PersistedGrantBlobContainer.CreateIfNotExistsAsync()};
            await Task.WhenAll(tasks).ConfigureAwait(false);
            return true;
        }

        protected virtual void Initialize(PersistedGrantStorageConfig config)
        {
            TableClient = Microsoft.Azure.Cosmos.Table.CloudStorageAccount.Parse(config.StorageConnectionString).CreateCloudTableClient();
            TableClient.DefaultRequestOptions.PayloadFormat = TablePayloadFormat.Json;
            PersistedGrantTableName = config.PersistedGrantTableName;

            if (string.IsNullOrWhiteSpace(PersistedGrantTableName))
            {
                throw new ArgumentException($"PersistedGrantTableName cannot be null or empty, check your configuration.", nameof(config.PersistedGrantTableName));
            }

            PersistedGrantTable = TableClient.GetTableReference(PersistedGrantTableName);

            BlobClient = new BlobServiceClient(config.StorageConnectionString);
            BlobContainerName = config.BlobContainerName;
            if (string.IsNullOrWhiteSpace(BlobContainerName))
            {
                throw new ArgumentException($"BlobContainerName cannot be null or empty, check your configuration.", nameof(config.BlobContainerName));
            }
            PersistedGrantBlobContainer = BlobClient.GetBlobContainerClient(BlobContainerName);

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
