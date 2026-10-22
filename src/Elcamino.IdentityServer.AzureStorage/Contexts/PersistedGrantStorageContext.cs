// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
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

        /// <summary>
        /// Removes one row returned by <see cref="GetExpiredAsync"/>, guarded by its query-time ETag
        /// so a concurrently renewed grant is never destroyed. Rows are processed independently, so
        /// query pagination splitting a grant's two rows across batches is safe. The token data blob
        /// is removed with the key row; its ETag is captured before the guarded key-row delete, which
        /// PersistedGrantStore.StoreAsync's key-row-first write order makes the concurrency gate.
        /// </summary>
        /// <param name="queriedRow">A row previously returned by <see cref="GetExpiredAsync"/>.</param>
        /// <returns>true if the row was removed; false if the grant was renewed or already removed.</returns>
        public async Task<bool> RemoveExpiredAsync(PersistedGrantTblEntity queriedRow, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(queriedRow);
            TableClient table = PersistedGrantTable;

            if (queriedRow.PartitionKey != queriedRow.RowKey)
            {
                return await DeleteEntityIfMatchAsync(table, queriedRow.PartitionKey, queriedRow.RowKey, queriedRow.ETag, cancellationToken).ConfigureAwait(false);
            }

            BlobClient blob = PersistedGrantBlobContainer.GetBlobClient(KeyGeneratorHelper.GenerateHashValue(queriedRow.Key).ToString());
            ETag? blobETag = await GetBlobETagAsync(blob, cancellationToken).ConfigureAwait(false);

            if (!await DeleteEntityIfMatchAsync(table, queriedRow.PartitionKey, queriedRow.RowKey, queriedRow.ETag, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            if (blobETag.HasValue)
            {
                await DeleteBlobIfMatchAsync(blob, blobETag.Value, cancellationToken).ConfigureAwait(false);
            }
            return true;
        }

        private static async Task<ETag?> GetBlobETagAsync(BlobClient blob, CancellationToken cancellationToken)
        {
            try
            {
                Response<BlobProperties> properties = await blob.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                return properties.Value.ETag;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return null;
            }
        }

        private static async Task<bool> DeleteEntityIfMatchAsync(TableClient table, string partitionKey, string rowKey, ETag eTag, CancellationToken cancellationToken)
        {
            try
            {
                Response response = await table.DeleteEntityAsync(partitionKey, rowKey, eTag, cancellationToken).ConfigureAwait(false);
                return response is null || !response.IsError;
            }
            catch (RequestFailedException ex) when (ex.Status == 412 || ex.Status == 404)
            {
                return false;
            }
        }

        private static async Task DeleteBlobIfMatchAsync(BlobClient blob, ETag eTag, CancellationToken cancellationToken)
        {
            try
            {
                await blob.DeleteIfExistsAsync(conditions: new BlobRequestConditions { IfMatch = eTag }, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (RequestFailedException ex) when (ex.Status == 412 || ex.Status == 409)
            {
            }
        }
    }
}
