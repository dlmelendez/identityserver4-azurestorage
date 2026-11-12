// Copyright (c) David Melendez. All rights reserved.
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

        async Task<IReadOnlyCollection<PersistedGrant>> IPersistedGrantStore.GetAllAsync(PersistedGrantFilter grantFilter, CancellationToken cancellationToken)
        {
            return await GetAllAsync(grantFilter, cancellationToken).ToListAsync(cancellationToken).ConfigureAwait(false);
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
                string tokenData = await StorageContext.GetBlobContentAsync(model.Key, StorageContext.PersistedGrantBlobContainer, cancellationToken)
                        .ConfigureAwait(false);
                if (string.IsNullOrEmpty(tokenData))
                {
                    _logger.LogWarning("{persistedGrantKey} grant row found but token data blob is missing; skipping", model.Key);
                    continue;
                }
                model.Data = tokenData;
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
                if (string.IsNullOrEmpty(tokenDataTask.Result))
                {
                    _logger.LogWarning("{persistedGrantKey} grant row found but token data blob is missing; treating grant as not found", key);
                    return null;
                }
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

        /// <remarks>
        /// Follows Duende's reference stores: a non-empty <see cref="PersistedGrantFilter.ClientIds"/> matches those ids
        /// plus <see cref="PersistedGrantFilter.ClientId"/> when set; otherwise <see cref="PersistedGrantFilter.ClientId"/>
        /// alone applies. <see cref="PersistedGrantFilter.Types"/> and <see cref="PersistedGrantFilter.Type"/> work the same way.
        /// An empty list is no constraint.
        /// </remarks>
        private static ReadOnlySpan<char> GetTableFilter(PersistedGrantFilter grantFilter)
        {
            var hashedSubject = KeyGeneratorHelper.GenerateHashValue(grantFilter.SubjectId);

            var tableFilter = TableQuery.GenerateFilterCondition(nameof(PersistedGrantTblEntity.PartitionKey),
                QueryComparisons.Equal,
                hashedSubject);

            string[] clientIds = GetFilterValues(grantFilter.ClientIds, grantFilter.ClientId);
            string[] types = GetFilterValues(grantFilter.Types, grantFilter.Type);

            if (clientIds.Length > 0
                || types.Length > 0
                || !string.IsNullOrWhiteSpace(grantFilter.SessionId))
            {
                TableQueryBuilder queryBuilder = new TableQueryBuilder();
                queryBuilder.AddFilter(tableFilter);
                AddAnyOfFilter(queryBuilder, nameof(PersistedGrantTblEntity.ClientId), clientIds);
                AddAnyOfFilter(queryBuilder, nameof(PersistedGrantTblEntity.Type), types);

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

        /// <returns>
        /// The list plus <paramref name="value"/> when the list is not empty; otherwise <paramref name="value"/> when set;
        /// otherwise no values, meaning no constraint.
        /// </returns>
        private static string[] GetFilterValues(IReadOnlyCollection<string> values, string value)
        {
            if (values is { Count: > 0 })
            {
                IEnumerable<string> allValues = string.IsNullOrWhiteSpace(value) ? values : values.Append(value);
                return [.. allValues.Distinct(StringComparer.Ordinal)];
            }
            return string.IsNullOrWhiteSpace(value) ? [] : [value];
        }

        /// <summary>
        /// Adds "and (property eq value1 or property eq value2 ...)". A single value is added without the group,
        /// so the filter is the same as for a plain ClientId or Type. Adds nothing when there are no values.
        /// </summary>
        private static void AddAnyOfFilter(TableQueryBuilder queryBuilder, string propertyName, string[] values)
        {
            if (values.Length == 0)
            {
                return;
            }

            queryBuilder.CombineFilters(TableOperator.And);
            if (values.Length > 1)
            {
                queryBuilder.BeginGroup();
            }
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0)
                {
                    queryBuilder.CombineFilters(TableOperator.Or);
                }
                queryBuilder.AddFilter(propertyName, QueryComparison.Equal, values[i].AsSpan());
            }
            if (values.Length > 1)
            {
                queryBuilder.EndGroup();
            }
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
                // Invariant: the key row is written before the subject row and blob.
                // PersistedGrantStorageContext.RemoveExpiredAsync relies on this ordering to make
                // its ETag-guarded key-row delete the gate for the subject-row and blob deletes.
                await table.UpsertEntityAsync(entities.keyGrant, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
                await Task.WhenAll(table.UpsertEntityAsync(entities.subjectGrant, TableUpdateMode.Replace, cancellationToken),
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
