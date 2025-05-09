﻿// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.
// Based on work from Brock Allen & Dominick Baier, https://github.com/IdentityServer/Duende.IdentityServer

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using ElCamino.Azure.Data.Tables;
using ElCamino.IdentityServer.AzureStorage.Contexts;
using ElCamino.IdentityServer.AzureStorage.Entities;
using ElCamino.IdentityServer.AzureStorage.Helpers;
using ElCamino.IdentityServer.AzureStorage.Mappers;
using Microsoft.Extensions.Logging;

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
            return await GetAllAsync(grantFilter, default).ToListAsync().ConfigureAwait(false);
        }

        public async IAsyncEnumerable<PersistedGrant> GetAllAsync(PersistedGrantFilter grantFilter, [EnumeratorCancellation]CancellationToken cancellationToken = default)
        {
            int counter = 0;
            grantFilter.Validate();
#if DEBUG
            Stopwatch sw = new Stopwatch();
            sw.Start();
#endif
            string tableFilter = GetTableFilter(grantFilter).ToString();
#if DEBUG
            sw.Stop();
            Debug.WriteLine(message: $"{nameof(GetTableFilter)}: {sw.ElapsedMilliseconds} ms");
#endif

            await foreach (var model in Contexts.StorageContext.GetAllByTableQueryAsync<PersistedGrantTblEntity, PersistedGrant>
                (tableFilter, StorageContext.PersistedGrantTable, p => p.ToModel(), cancellationToken).ConfigureAwait(false))
            {
                await StorageContext.GetBlobContentAsync(model.Key, StorageContext.PersistedGrantBlobContainer, cancellationToken)
                        .ContinueWith((blobTask) =>
                        {
                            model.Data = blobTask.Result;
                        }, cancellationToken)
                        .ConfigureAwait(false);
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
            Task<PersistedGrantTblEntity> entityTask = Contexts.StorageContext.GetEntityTableAsync<PersistedGrantTblEntity>(key, StorageContext.PersistedGrantTable, cancellationToken);
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
#if DEBUG
            Stopwatch sw = new Stopwatch();
            sw.Start();
#endif
            string tableFilter = GetTableFilter(grantFilter).ToString();
#if DEBUG
            sw.Stop();
            Debug.WriteLine(message: $"{nameof(GetTableFilter)}: {sw.ElapsedMilliseconds} ms");
#endif

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

        private static ReadOnlySpan<char> GetTableFilter(PersistedGrantFilter grantFilter)
        {
            var hashedSubject = KeyGeneratorHelper.GenerateHashValue(grantFilter.SubjectId);

            var tableFilter = TableQuery.GenerateFilterCondition(nameof(PersistedGrantTblEntity.PartitionKey),
                QueryComparisons.Equal,
                hashedSubject);

            if (!string.IsNullOrWhiteSpace(grantFilter.ClientId)
                || !string.IsNullOrWhiteSpace(grantFilter.Type)
                || !string.IsNullOrWhiteSpace(grantFilter.SessionId))
            {
                TableQueryBuilder queryBuilder = new TableQueryBuilder();
                queryBuilder.AddFilter(tableFilter);
                if (!string.IsNullOrWhiteSpace(grantFilter.ClientId))
                {
                    queryBuilder.CombineFilters(TableOperator.And);
                    queryBuilder.AddFilter(nameof(PersistedGrantTblEntity.ClientId),
                        QueryComparison.Equal,
                        grantFilter.ClientId.AsSpan());
                }

                if (!string.IsNullOrWhiteSpace(grantFilter.Type))
                {
                    queryBuilder.CombineFilters(TableOperator.And);
                    queryBuilder.AddFilter(nameof(PersistedGrantTblEntity.Type),
                        QueryComparison.Equal,
                        grantFilter.Type.AsSpan());
                }

                if (!string.IsNullOrWhiteSpace(grantFilter.SessionId))
                {
                    queryBuilder.CombineFilters(TableOperator.And);
                    queryBuilder.AddFilter(nameof(PersistedGrantTblEntity.SessionId),
                        QueryComparison.Equal,
                        grantFilter.SessionId.AsSpan());
                }

                return queryBuilder.ToString();
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
