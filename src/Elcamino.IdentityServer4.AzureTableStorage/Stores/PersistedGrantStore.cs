// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.
// Based on work from Brock Allen & Dominick Baier, https://github.com/IdentityServer/IdentityServer4

using ElCamino.IdentityServer4.AzureStorage.Contexts;
using ElCamino.IdentityServer4.AzureStorage.Helpers;
using ElCamino.IdentityServer4.AzureStorage.Mappers;
using ElCamino.IdentityServer4.AzureStorage.Entities;
using IdentityServer4.Models;
using IdentityServer4.Stores;
using Azure.Storage.Blobs;
using Microsoft.Azure.Cosmos.Table;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ElCamino.IdentityServer4.AzureStorage.Stores
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

        public async Task<IEnumerable<PersistedGrant>> GetAllAsync(string subjectId)
        {
            string hashedSubject = KeyGeneratorHelper.GenerateHashValue(subjectId);

            string partitionKeyFilter = TableQuery.GenerateFilterCondition("PartitionKey",
                QueryComparisons.Equal,
                hashedSubject);

            TableQuery<PersistedGrantTblEntity> tq = new TableQuery<PersistedGrantTblEntity>();
            tq.FilterString = partitionKeyFilter;

            var list = (await StorageContext.GetAllByTableQueryAsync(tq, StorageContext.PersistedGrantTable).ConfigureAwait(false))
                .Select(s => s.ToModel()).ToArray(); //without .ToArray() error occurs. 

            await Task.WhenAll(list.Select(m =>
            {
                return StorageContext.GetBlobContentAsync(m.Key, StorageContext.PersistedGrantBlobContainer)
                        .ContinueWith((blobTask) =>
                       {
                           m.Data = blobTask.Result;
                       });
            }));
            _logger.LogDebug($"{list.Count()} persisted grants found for {subjectId}");
            return list;
        }

        public async Task<PersistedGrant> GetAsync(string key)
        {
            //Getting greedy
            Task<PersistedGrantTblEntity> entityTask = StorageContext.GetEntityTableAsync<PersistedGrantTblEntity>(key, StorageContext.PersistedGrantTable);
            Task<string> tokenDataTask =  StorageContext.GetBlobContentAsync(key, StorageContext.PersistedGrantBlobContainer);
            await Task.WhenAll(entityTask, tokenDataTask);
            var model = entityTask.Result?.ToModel();
            if (model != null)
            {
                model.Data = tokenDataTask.Result;
            }
            _logger.LogDebug("{0} found in table storage and blob data: {1}", key, model != null);
            return model;
        }


        public async Task RemoveAllAsync(string subjectId, string clientId)
        {
            CloudTable table = StorageContext.PersistedGrantTable;

            string hashedSubject = KeyGeneratorHelper.GenerateHashValue(subjectId);

            string partitionKeyFilter = TableQuery.GenerateFilterCondition("PartitionKey",
                QueryComparisons.Equal,
                hashedSubject);

            string rowFilter = TableQuery.GenerateFilterCondition("ClientId",
                QueryComparisons.Equal,
                clientId);

            string filter = TableQuery.CombineFilters(partitionKeyFilter, TableOperators.And, rowFilter);

            TableQuery<PersistedGrantTblEntity> tq = new TableQuery<PersistedGrantTblEntity>();
            tq.FilterString = filter;

            _logger.LogDebug($"removing persisted grants from database for subject {subjectId}, clientId {clientId}");
            
            var mainTasks = (await StorageContext.GetAllByTableQueryAsync(tq, StorageContext.PersistedGrantTable).ConfigureAwait(false))
                .Select(subjectEntity =>
                {
                    var (keyGrant, subjectGrant) = subjectEntity.ToModel().ToEntities();
                    return Task.WhenAll(StorageContext.GetAndDeleteTableEntityByKeysAsync(keyGrant.PartitionKey, keyGrant.RowKey, StorageContext.PersistedGrantTable),
                                table.ExecuteAsync(TableOperation.Delete(subjectEntity)),
                                StorageContext.DeleteBlobAsync(subjectEntity.Key, StorageContext.PersistedGrantBlobContainer));
                }).ToArray();
            try
            {
                await Task.WhenAll(mainTasks);
            }
            catch (AggregateException agg)
            {
                ExceptionHelper.LogStorageExceptions(agg, (tableEx) =>
                {
                    _logger.LogDebug("removing persisted grants from table storage for subject {subjectId}, clientId {clientId}");
                }, (blobEx) =>
                {
                    _logger.LogDebug("removing persisted grants from blob storage for subject {subjectId}, clientId {clientId}");
                });
            }
        }


        public async Task RemoveAllAsync(string subjectId, string clientId, string type)
        {
            CloudTable table = StorageContext.PersistedGrantTable;

            string hashedSubject = KeyGeneratorHelper.GenerateHashValue(subjectId);

            string partitionKeyFilter = TableQuery.GenerateFilterCondition("PartitionKey",
                QueryComparisons.Equal,
                hashedSubject);

            string rowClientFilter = TableQuery.GenerateFilterCondition("ClientId",
                QueryComparisons.Equal,
                clientId);

            string rowTypeFilter = TableQuery.GenerateFilterCondition("Type",
               QueryComparisons.Equal,
               type);

            string rowFilter = TableQuery.CombineFilters(rowClientFilter, TableOperators.And, rowTypeFilter);

            string filter = TableQuery.CombineFilters(partitionKeyFilter, TableOperators.And, rowFilter);

            TableQuery<PersistedGrantTblEntity> tq = new TableQuery<PersistedGrantTblEntity>();
            tq.FilterString = filter;

            _logger.LogDebug($"removing persisted grants from database for subject {subjectId}, clientId {clientId}, grantType {type}");
            
            var mainTasks = (await StorageContext.GetAllByTableQueryAsync(tq, StorageContext.PersistedGrantTable).ConfigureAwait(false))
                .Select(subjectEntity =>
                {
                    var deletes = subjectEntity.ToModel().ToEntities();
                    return Task.WhenAll(StorageContext.GetAndDeleteTableEntityByKeysAsync(deletes.keyGrant.PartitionKey, deletes.keyGrant.RowKey, StorageContext.PersistedGrantTable),
                                table.ExecuteAsync(TableOperation.Delete(subjectEntity)),
                                StorageContext.DeleteBlobAsync(subjectEntity.Key, StorageContext.PersistedGrantBlobContainer));
                });
            try
            {
                await Task.WhenAll(mainTasks);
            }
            catch (AggregateException agg)
            {
                ExceptionHelper.LogStorageExceptions(agg, (tableEx) =>
                {
                    _logger.LogDebug($"error removing persisted grants from table storage for subject {subjectId}, clientId {clientId}");
                }, (blobEx) =>
                {
                    _logger.LogDebug($"error removing persisted grants from blob storage for subject {subjectId}, clientId {clientId}");
                });
            }
        }

        public async Task RemoveAsync(string key)
        {
            try
            {
                bool entityFound = await StorageContext.RemoveAsync(key);
                if (!entityFound)
                {
                    _logger.LogDebug($"no {key} persisted grant found in table storage to remove");
                }
            }
            catch (AggregateException agg)
            {
                ExceptionHelper.LogStorageExceptions(agg, (tableEx) =>
                {
                    _logger.LogWarning("exception removing {persistedGrantKey} persisted grant in table storage: {error}", key?? string.Empty, tableEx.Message);
                }, (blobEx) =>
                {
                    _logger.LogWarning("exception removing {persistedGrantKey} persisted grant in blob storage: {error}", key?? string.Empty, blobEx.Message);
                });
            }
           
        }

        
        public async Task StoreAsync(PersistedGrant grant)
        {
            var entities = grant.ToEntities();
            CloudTable table = StorageContext.PersistedGrantTable;
            try
            {
                await Task.WhenAll(table.ExecuteAsync(TableOperation.InsertOrReplace(entities.keyGrant)),
                    table.ExecuteAsync(TableOperation.InsertOrReplace(entities.subjectGrant)),
                    StorageContext.SaveBlobWithHashedKeyAsync(grant.Key, grant.Data, StorageContext.PersistedGrantBlobContainer)).ConfigureAwait(false);
            }
            catch (AggregateException agg)
            {
                ExceptionHelper.LogStorageExceptions(agg, (tableEx) =>
                {
                    _logger.LogWarning("exception updating {persistedGrantKey} persisted grant in table storage: {error}", grant.Key, tableEx.Message);
                }, (blobEx) =>
                {
                    _logger.LogWarning("exception updating {persistedGrantKey} persisted grant in blob storage: {error}", grant.Key, blobEx.Message);
                });                
            }

        }

        
    }
}
