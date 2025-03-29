// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using ElCamino.IdentityServer.AzureStorage.Configuration;
using ElCamino.IdentityServer.AzureStorage.Entities;
using ElCamino.IdentityServer.AzureStorage.Helpers;
using ElCamino.IdentityServer.AzureStorage.Mappers;
using Microsoft.Extensions.Options;

namespace ElCamino.IdentityServer.AzureStorage.Contexts
{
    public class PersistedGrantStorageContext : StorageContext
    {
        private string BlobContainerName = string.Empty;

        public string PersistedGrantTableName { get; private set; }

        public TableClient PersistedGrantTable { get; private set; }

        public TableServiceClient TableClient { get; private set; }

        public BlobServiceClient BlobClient { get; private set; }

        public BlobContainerClient PersistedGrantBlobContainer { get; private set; }


        public PersistedGrantStorageContext(IOptions<PersistedGrantStorageConfig> config,
            TableServiceClient tableClient,
            BlobServiceClient blobClient) : this(config.Value, tableClient, blobClient)
        {
        }

        public PersistedGrantStorageContext(PersistedGrantStorageConfig config,
            TableServiceClient tableClient,
            BlobServiceClient blobClient)
        {
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(tableClient);
            ArgumentNullException.ThrowIfNull(blobClient);

            TableClient = tableClient;
            BlobClient = blobClient;

            Initialize(config);
        }

        public async Task<bool> CreateStorageIfNotExists()
        {
            Task[] tasks = [ PersistedGrantTable.CreateIfNotExistsAsync(),
                PersistedGrantBlobContainer.CreateIfNotExistsAsync() ];
            await Task.WhenAll(tasks).ConfigureAwait(false);
            return true;
        }

        protected virtual void Initialize(PersistedGrantStorageConfig config)
        {
            PersistedGrantTableName = config.PersistedGrantTableName;

            if (string.IsNullOrWhiteSpace(PersistedGrantTableName))
            {
                throw new ArgumentException($"{nameof(config.PersistedGrantTableName)} cannot be null or empty, check your configuration.", nameof(config));
            }

            PersistedGrantTable = TableClient.GetTableClient(PersistedGrantTableName);

            BlobContainerName = config.BlobContainerName;
            if (string.IsNullOrWhiteSpace(BlobContainerName))
            {
                throw new ArgumentException($"{nameof(config.BlobContainerName)} cannot be null or empty, check your configuration.", nameof(config));
            }
            PersistedGrantBlobContainer = BlobClient.GetBlobContainerClient(BlobContainerName);

        }

        public async Task<IEnumerable<PersistedGrantTblEntity>> GetExpiredAsync(int maxResults, CancellationToken cancellationToken = default)
        {
            TableQuery tq = new TableQuery()
            {
                FilterString = TableQuery.GenerateFilterConditionForDate("Expiration",
                    QueryComparisons.LessThan,
                    DateTimeOffset.UtcNow).ToString(),
                TakeCount = maxResults
            };            
            return await PersistedGrantTable.ExecuteQueryAsync<PersistedGrantTblEntity>(tq, cancellationToken).ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            TableClient table = PersistedGrantTable;
            PersistedGrantTblEntity keyEntity = await GetEntityTableAsync<PersistedGrantTblEntity>(key, table, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (keyEntity != null)
            {
                var entities = keyEntity.ToModel().ToEntities();
                await Task.WhenAll(table.DeleteEntityAsync(entities.subjectGrant.PartitionKey, entities.subjectGrant.RowKey, KeyGeneratorHelper.ETagWildCard, cancellationToken: cancellationToken),
                        table.DeleteEntityAsync(keyEntity.PartitionKey, keyEntity.RowKey, KeyGeneratorHelper.ETagWildCard, cancellationToken: cancellationToken),
                        DeleteBlobAsync(key, PersistedGrantBlobContainer, cancellationToken)).ConfigureAwait(false);
                return true;
            }
            return false;
        }
    }
}
