// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.
// Based on work from Brock Allen & Dominick Baier, https://github.com/IdentityServer/Duende.IdentityServer

using ElCamino.Duende.IdentityServer.AzureStorage.Contexts;
using ElCamino.Duende.IdentityServer.AzureStorage.Helpers;
using ElCamino.Duende.IdentityServer.AzureStorage.Mappers;
using ElCamino.Duende.IdentityServer.AzureStorage.Entities;
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

namespace ElCamino.Duende.IdentityServer.AzureStorage.Stores
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
            return await GetAllExAsync(grantFilter).ToListAsync();
        }

        public async IAsyncEnumerable<PersistedGrant> GetAllExAsync(PersistedGrantFilter grantFilter)
        {
            int counter = 0;
            grantFilter.Validate();
            string tableFilter = GetTableFilter(grantFilter);

            TableQuery tq = new TableQuery();
            tq.FilterString = tableFilter;

            await foreach (var model in StorageContext.GetAllByTableQueryAsync<PersistedGrantTblEntity, PersistedGrant>
                (tq, StorageContext.PersistedGrantTable, p => p.ToModel()).ConfigureAwait(false))
            {
                await StorageContext.GetBlobContentAsync(model.Key, StorageContext.PersistedGrantBlobContainer)
                        .ContinueWith((blobTask) =>
                        {
                            model.Data = blobTask.Result;
                        }).ConfigureAwait(false);
                counter++;
                yield return model;
            }
            _logger.LogDebug($"{counter} persisted grants found for table filter {tableFilter}");
        }

        public async Task<PersistedGrant> GetAsync(string key)
        {
            //Getting greedy
            Task<PersistedGrantTblEntity> entityTask = StorageContext.GetEntityTableAsync<PersistedGrantTblEntity>(key, StorageContext.PersistedGrantTable);
            Task<string> tokenDataTask =  StorageContext.GetBlobContentAsync(key, StorageContext.PersistedGrantBlobContainer);
            await Task.WhenAll(entityTask, tokenDataTask).ConfigureAwait(false);
            var model = entityTask.Result?.ToModel();
            if (model != null)
            {
                model.Data = tokenDataTask.Result;
            }
            _logger.LogDebug("{0} found in table storage and blob data: {1}", key, model != null);
            return model;
        }


        public async Task RemoveAllAsync(PersistedGrantFilter grantFilter)
        {
            grantFilter.Validate();
            TableClient table = StorageContext.PersistedGrantTable;

            string tableFilter = GetTableFilter(grantFilter);
            TableQuery tq = new TableQuery();
            tq.FilterString = tableFilter;

            _logger.LogDebug($"removing persisted grants from database for table filter {tableFilter} ");


            var mainTasks = (await StorageContext.GetAllByTableQueryAsync<PersistedGrantTblEntity>(tq, table)
                .ToListAsync()
                .ConfigureAwait(false))
                .Select(subjectEntity =>
                {
                    var deletes = subjectEntity.ToModel().ToEntities();
                    return Task.WhenAll(table.DeleteEntityAsync(deletes.keyGrant.PartitionKey, deletes.keyGrant.RowKey, KeyGeneratorHelper.ETagWildCard),
                                table.DeleteEntityAsync(subjectEntity.PartitionKey, subjectEntity.RowKey, KeyGeneratorHelper.ETagWildCard),
                                StorageContext.DeleteBlobAsync(subjectEntity.Key, StorageContext.PersistedGrantBlobContainer));
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
                    _logger.LogWarning($"storage exception ErrorCode: {rfex.ErrorCode ?? string.Empty}, Http Status Code: {rfex.Status}");
                    _logger.LogDebug($"error removing persisted grants from storage for table filter {tableFilter} ");
                });
                throw;
            }
            catch (RequestFailedException rfex)
            {
                _logger.LogError(rfex, rfex.Message);
                _logger.LogWarning($"storage exception ErrorCode: {rfex.ErrorCode ?? string.Empty}, Http Status Code: {rfex.Status}");
                _logger.LogDebug($"error removing persisted grants from storage for table filter {tableFilter} ");

                throw;
            }
        }

        private string GetTableFilter(PersistedGrantFilter grantFilter)
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

        public async Task RemoveAsync(string key)
        {
            try
            {
                bool entityFound = await StorageContext.RemoveAsync(key).ConfigureAwait(false);
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
                    _logger.LogWarning($"storage exception ErrorCode: {rfex.ErrorCode ?? string.Empty}, Http Status Code: {rfex.Status}");
                    _logger.LogWarning("exception removing {persistedGrantKey} persisted grant in storage: {error}", key?? string.Empty, rfex.Message);
                });
                throw;
            }
            catch (RequestFailedException rfex)
            {
                _logger.LogError(rfex, rfex.Message);
                _logger.LogWarning($"storage exception ErrorCode: {rfex.ErrorCode ?? string.Empty}, Http Status Code: {rfex.Status}");
                _logger.LogWarning("exception removing {persistedGrantKey} persisted grant in storage: {error}", key ?? string.Empty, rfex.Message);

                throw;
            }
        }

        
        public async Task StoreAsync(PersistedGrant grant)
        {
            var entities = grant.ToEntities();
            TableClient table = StorageContext.PersistedGrantTable;
            try
            {
                await Task.WhenAll(table.UpsertEntityAsync(entities.keyGrant, TableUpdateMode.Replace),
                    table.UpsertEntityAsync(entities.subjectGrant, TableUpdateMode.Replace),
                    StorageContext.SaveBlobWithHashedKeyAsync(grant.Key, grant.Data, StorageContext.PersistedGrantBlobContainer))
                    .ConfigureAwait(false);
            }
            catch (AggregateException agg)
            {
                _logger.LogError(agg, agg.Message);
                ExceptionHelper.LogStorageExceptions(agg, (rfex) =>
                {
                    _logger.LogWarning($"storage exception ErrorCode: {rfex.ErrorCode ?? string.Empty}, Http Status Code: {rfex.Status}");
                    _logger.LogWarning("exception updating {persistedGrantKey} persisted grant in blob storage: {error}", grant.Key, rfex.Message);
                });
                throw;
            }
            catch (RequestFailedException rfex)
            {
                _logger.LogError(rfex, rfex.Message);
                _logger.LogWarning($"storage exception ErrorCode: {rfex.ErrorCode ?? string.Empty}, Http Status Code: {rfex.Status}");
                _logger.LogWarning("exception updating {persistedGrantKey} persisted grant in blob storage: {error}", grant.Key, rfex.Message);
                throw;
            }

        }        

        
    }
}
