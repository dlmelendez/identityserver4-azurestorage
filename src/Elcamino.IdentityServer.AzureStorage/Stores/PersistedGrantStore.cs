// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.
// Based on work from Brock Allen & Dominick Baier, https://github.com/IdentityServer/Duende.IdentityServer

using ElCamino.IdentityServer.AzureStorage.Contexts;
using ElCamino.IdentityServer.AzureStorage.Helpers;
using ElCamino.IdentityServer.AzureStorage.Mappers;
using ElCamino.IdentityServer.AzureStorage.Entities;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Azure.Storage.Blobs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Duende.IdentityServer.Extensions;
using Azure.Data.Tables;
using Azure;
using System.Threading;
using System.Runtime.CompilerServices;

namespace ElCamino.IdentityServer.AzureStorage.Stores
{
    public class PersistedGrantStore : IPersistedGrantStore
    {
        public PersistedGrantStorageContext StorageContext { get; private set; }
        private readonly ILogger _logger;

        public PersistedGrantStore(PersistedGrantStorageContext storageContext, ILogger<PersistedGrantStore> logger)
        {
            StorageContext = storageContext;
            _logger = logger;
        }

        public async Task<IEnumerable<PersistedGrant>> GetAllAsync(PersistedGrantFilter grantFilter)
        {
            return await GetAllAsync(grantFilter, default).ToListAsync();
        }

        public async IAsyncEnumerable<PersistedGrant> GetAllAsync(PersistedGrantFilter grantFilter, [EnumeratorCancellation]CancellationToken cancellationToken = default)
        {
            int counter = 0;
            grantFilter.Validate();
            string tableFilter = GetTableFilter(grantFilter);

            await foreach (var model in StorageContext.GetAllByTableQueryAsync<PersistedGrantTblEntity, PersistedGrant>
                (tableFilter, StorageContext.PersistedGrantTable, p => p.ToModel(), cancellationToken).ConfigureAwait(false))
            {
                await StorageContext.GetBlobContentAsync(model.Key, StorageContext.PersistedGrantBlobContainer, cancellationToken)
                        .ContinueWith((blobTask) =>
                        {
                            model.Data = blobTask.Result;
                        }, cancellationToken).ConfigureAwait(false);
                counter++;
                yield return model;
            }
            _logger.LogDebug($"{counter} persisted grants found for table filter {tableFilter}");
        }

        public Task<PersistedGrant> GetAsync(string key)
        { 
            return GetAsync(key, default);
        }

        public async Task<PersistedGrant> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            //Getting greedy
            Task<PersistedGrantTblEntity> entityTask = StorageContext.GetEntityTableAsync<PersistedGrantTblEntity>(key, StorageContext.PersistedGrantTable, cancellationToken);
            Task<string> tokenDataTask =  StorageContext.GetBlobContentAsync(key, StorageContext.PersistedGrantBlobContainer, cancellationToken);
            await Task.WhenAll(entityTask, tokenDataTask).ConfigureAwait(false);
            var model = entityTask.Result?.ToModel();
            if (model != null)
            {
                model.Data = tokenDataTask.Result;
            }
            _logger.LogDebug("{0} found in table storage and blob data: {1}", key, model != null);
            return model;
        }

        public Task RemoveAllAsync(PersistedGrantFilter grantFilter)
        {
            return RemoveAllAsync(grantFilter, default);
        }

        public async Task RemoveAllAsync(PersistedGrantFilter grantFilter, CancellationToken cancellationToken = default)
        {
            grantFilter.Validate();
            TableClient table = StorageContext.PersistedGrantTable;

            string tableFilter = GetTableFilter(grantFilter);
            _logger.LogDebug(message: $"removing persisted grants from database for table filter {tableFilter} ");

            var mainTasks = (await table.QueryAsync<PersistedGrantTblEntity>(filter: tableFilter, cancellationToken: cancellationToken)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
                .Select(subjectEntity =>
                {
                    var deletes = subjectEntity.ToModel().ToEntities();
                    return Task.WhenAll(table.DeleteEntityAsync(deletes.keyGrant.PartitionKey, deletes.keyGrant.RowKey, KeyGeneratorHelper.ETagWildCard, cancellationToken),
                                table.DeleteEntityAsync(subjectEntity.PartitionKey, subjectEntity.RowKey, KeyGeneratorHelper.ETagWildCard, cancellationToken),
                                StorageContext.DeleteBlobAsync(subjectEntity.Key, StorageContext.PersistedGrantBlobContainer, cancellationToken));
                });
            try
            {
                await Task.WhenAll(mainTasks).ConfigureAwait(false);
            }
            catch (AggregateException agg)
            {
                _logger.LogError(agg, agg.Message);
                ExceptionHelper.LogStorageExceptions(agg, (rfex) =>
                {
                    _logger.LogStorageError(rfex);
                    _logger.LogDebug("error removing persisted grants from storage for table filter {tableFilter} ", tableFilter);
                });
                throw;
            }
            catch (RequestFailedException rfex)
            {
                _logger.LogStorageError(rfex);
                _logger.LogDebug($"error removing persisted grants from storage for table filter {tableFilter} ");
                throw;
            }
        }

        private static string GetTableFilter(PersistedGrantFilter grantFilter)
        {
            string hashedSubject = KeyGeneratorHelper.GenerateHashValue(grantFilter.SubjectId);

            string tableFilter = TableQuery.GenerateFilterCondition(nameof(PersistedGrantTblEntity.PartitionKey),
                QueryComparisons.Equal,
                hashedSubject);

            if (!String.IsNullOrWhiteSpace(grantFilter.ClientId))
            {
                string rowClientFilter = TableQuery.GenerateFilterCondition(nameof(PersistedGrantTblEntity.ClientId),
                    QueryComparisons.Equal,
                    grantFilter.ClientId);
                tableFilter = TableQuery.CombineFilters(tableFilter, TableOperators.And, rowClientFilter);
            }

            if (!String.IsNullOrWhiteSpace(grantFilter.Type))
            {
                string rowTypeFilter = TableQuery.GenerateFilterCondition(nameof(PersistedGrantTblEntity.Type),
                   QueryComparisons.Equal,
                   grantFilter.Type);
                tableFilter = TableQuery.CombineFilters(tableFilter, TableOperators.And, rowTypeFilter);
            }

            if (!String.IsNullOrWhiteSpace(grantFilter.SessionId))
            {
                string rowSessionIdFilter = TableQuery.GenerateFilterCondition(nameof(PersistedGrantTblEntity.SessionId),
                   QueryComparisons.Equal,
                   grantFilter.SessionId);
                tableFilter = TableQuery.CombineFilters(tableFilter, TableOperators.And, rowSessionIdFilter);
            }

            return tableFilter;
        }

        public Task RemoveAsync(string key)
        {
            return RemoveAsync(key, default);
        }

        public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                bool entityFound = await StorageContext.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
                if (!entityFound)
                {
                    _logger.LogDebug($"no {key} persisted grant found in table storage to remove");
                }
            }
            catch (AggregateException agg)
            {
                _logger.LogError(agg, agg.Message);
                ExceptionHelper.LogStorageExceptions(agg, (rfex) =>
                {
                    _logger.LogStorageError(rfex);
                    _logger.LogWarning("exception removing {persistedGrantKey} persisted grant in storage: {error}", key?? string.Empty, rfex.Message);
                });
                throw;
            }
            catch (RequestFailedException rfex)
            {
                _logger.LogStorageError(rfex);
                _logger.LogWarning("exception removing {persistedGrantKey} persisted grant in storage: {error}", key ?? string.Empty, rfex.Message);
                throw;
            }
        }


        public Task StoreAsync(PersistedGrant grant)
        {
            return StoreAsync(grant, default);
        }

        public async Task StoreAsync(PersistedGrant grant, CancellationToken cancellationToken = default)
        {
            var entities = grant.ToEntities();
            TableClient table = StorageContext.PersistedGrantTable;
            try
            {
                await Task.WhenAll(table.UpsertEntityAsync(entities.keyGrant, TableUpdateMode.Replace, cancellationToken),
                    table.UpsertEntityAsync(entities.subjectGrant, TableUpdateMode.Replace, cancellationToken),
                    StorageContext.SaveBlobWithHashedKeyAsync(grant.Key, grant.Data, StorageContext.PersistedGrantBlobContainer, cancellationToken))
                    .ConfigureAwait(false);
            }
            catch (AggregateException agg)
            {
                _logger.LogError(agg, agg.Message);
                ExceptionHelper.LogStorageExceptions(agg, (rfex) =>
                {
                    _logger.LogStorageError(rfex);
                    _logger.LogWarning("exception updating {persistedGrantKey} persisted grant in blob storage: {error}", grant.Key, rfex.Message);
                });
                throw;
            }
            catch (RequestFailedException rfex)
            {
                _logger.LogStorageError(rfex);
                _logger.LogWarning("exception updating {persistedGrantKey} persisted grant in blob storage: {error}", grant.Key, rfex.Message);
                throw;
            }

        }        
    }
}
