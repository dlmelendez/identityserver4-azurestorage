// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.
// Based on work from Brock Allen & Dominick Baier, https://github.com/IdentityServer/IdentityServer4

using ElCamino.IdentityServer4.AzureStorage.Contexts;
using ElCamino.IdentityServer4.AzureStorage.Helpers;
using ElCamino.IdentityServer4.AzureStorage.Interfaces;
using ElCamino.IdentityServer4.AzureStorage.Mappers;
using IdentityServer4.Models;
using IdentityServer4.Stores;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Model = IdentityServer4.Models;


namespace ElCamino.IdentityServer4.AzureStorage.Stores
{
    /// <summary>
    /// Implementation of IResourceStore thats uses EF.
    /// </summary>
    /// <seealso cref="IdentityServer4.Stores.IResourceStore" />
    public class ResourceStore : IResourceStore
    {
        public ResourceStorageContext StorageContext { get; private set; }
        private readonly ILogger<ResourceStore> _logger;

        
        public ResourceStore(ResourceStorageContext storageContext, ILogger<ResourceStore> logger)
        {
            StorageContext = storageContext;
            _logger = logger;
        }

        public async Task StoreAsync(Model.ApiResource model)
        {
            var entity = model.ToEntity();
            try
            {
                // Remove old scope indexes
                var existingEntity = await StorageContext.GetEntityBlobAsync<Entities.ApiResource>(entity.Name, StorageContext.ApiResourceBlobContainer);
                if (existingEntity != null 
                    && existingEntity.Scopes != null
                    && existingEntity.Scopes.Count > 0)
                {
                    //Remove old scope indexes
                    var deleteIndexes = existingEntity?.Scopes?.Select(s => s.Name).Distinct().Select(i => GenerateResourceIndexEntity(entity.Name, i));
                    await DeleteScopeIndexesAsync(deleteIndexes, StorageContext.ApiResourceTable);
                }
                // Add new scope indexes
                var newIndexes = entity?.Scopes?.Select(s => s.Name).Distinct().Select(i => GenerateResourceIndexEntity(entity.Name, i));
                await CreateScopeIndexesAsync(newIndexes, StorageContext.ApiResourceTable);
                await StorageContext.SaveBlobWithHashedKeyAsync(entity.Name, JsonConvert.SerializeObject(entity), StorageContext.ApiResourceBlobContainer);
                var entities = await GetAllApiResourceEntitiesAsync();
                entities = entities.Where(e => entity.Name != e.Name).Concat(new Entities.ApiResource[] { entity });
                await UpdateApiResourceCacheFileAsync(entities);
            }
            catch (AggregateException agg)
            {
                ExceptionHelper.LogStorageExceptions(agg, (tblEx) =>
                {
                    _logger.LogWarning("exception updating {apiName} api resource in table storage: {error}", model.Name, tblEx.Message);
                }, (blobEx) =>
                {
                    _logger.LogWarning("exception updating {apiName} api resource in blob storage: {error}", model.Name, blobEx.Message);
                });
                throw;
            }

        }

        public async Task StoreAsync(Model.IdentityResource model)
        {
            var entity = model.ToEntity();
            try
            {                
                await StorageContext.SaveBlobWithHashedKeyAsync(entity.Name, JsonConvert.SerializeObject(entity), StorageContext.IdentityResourceBlobContainer);
                var entities = await GetAllIdentityResourceEntitiesAsync();
                entities = entities.Where(e => entity.Name != e.Name).Concat(new Entities.IdentityResource[] { entity });
                await UpdateIdentityResourceCacheFileAsync(entities);
            }
            catch (AggregateException agg)
            {
                ExceptionHelper.LogStorageExceptions(agg, (tblEx) =>
                {
                    _logger.LogWarning("exception updating {apiName} identity resource in table storage: {error}", model.Name, tblEx.Message);
                }, (blobEx) =>
                {
                    _logger.LogWarning("exception updating {apiName} identity resource in blob storage: {error}", model.Name, blobEx.Message);
                });
                throw;
            }

        }

        public async Task RemoveIdentityResourceAsync(string name)
        {
            try
            {
                await StorageContext.DeleteBlobAsync(name, StorageContext.IdentityResourceBlobContainer);
                var entities = await GetAllIdentityResourceEntitiesAsync();
                entities = entities.Where(e => name != e.Name);
                await UpdateIdentityResourceCacheFileAsync(entities);
            }
            catch (AggregateException agg)
            {
                ExceptionHelper.LogStorageExceptions(agg, tableStorageLogger: null, (blobEx) =>
                {
                    _logger.LogWarning("exception removing {clientId} client in blob storage: {error}", name, blobEx.Message);
                });
            }
        }

        public async Task RemoveApiResourceAsync(string name)
        {
            try
            {
                // Remove old scope indexes
                var existingEntity = await StorageContext.GetEntityBlobAsync<Entities.ApiResource>(name, StorageContext.ApiResourceBlobContainer);
                if (existingEntity != null
                    && existingEntity.Scopes != null
                    && existingEntity.Scopes.Count > 0)
                {
                    //Remove old scope indexes
                    var deleteIndexes = existingEntity?.Scopes?.Select(s => s.Name).Distinct().Select(i => GenerateResourceIndexEntity(name, i));
                    await DeleteScopeIndexesAsync(deleteIndexes, StorageContext.ApiResourceTable);
                }
                await StorageContext.DeleteBlobAsync(name, StorageContext.ApiResourceBlobContainer);
                var entities = await GetAllApiResourceEntitiesAsync();
                entities = entities.Where(e => name != e.Name);
                await UpdateApiResourceCacheFileAsync(entities);
            }
            catch (AggregateException agg)
            {
                ExceptionHelper.LogStorageExceptions(agg, tableStorageLogger: null, (blobEx) =>
                {
                    _logger.LogWarning("exception removing {0} client in blob storage: {1}", name, blobEx.Message);
                });
            }
        }

        private async Task DeleteScopeIndexesAsync(IEnumerable<Entities.ResourceScopeIndexTblEntity> indexes, CloudTable table)
        {
            if (indexes != null )
            {                
                await Task.WhenAll(indexes.Select((index) => StorageContext.GetAndDeleteTableEntityByKeysAsync(index.PartitionKey, index.RowKey, table)))
                    .ConfigureAwait(false);
            }
        }
        
        private async Task CreateScopeIndexesAsync(IEnumerable<Entities.ResourceScopeIndexTblEntity> indexes, CloudTable table)
        {
            if (indexes != null)
            {
                await Task.WhenAll(indexes.Select((index) => table.ExecuteAsync(TableOperation.InsertOrReplace(index))))
                    .ConfigureAwait(false);
            }
        }

        public async Task UpdateApiResourceCacheFileAsync(IEnumerable<Entities.ApiResource> entities)
        {
            DateTime dateTimeNow = DateTime.UtcNow;
            string blobName = await StorageContext.UpdateBlobCacheFileAsync<Entities.ApiResource>(entities, StorageContext.ApiResourceBlobCacheContainer);
            _logger.LogInformation($"{nameof(UpdateApiResourceCacheFileAsync)} client count {entities.Count()} saved in blob storage: {blobName}");
        }

        public async Task UpdateIdentityResourceCacheFileAsync(IEnumerable<Entities.IdentityResource> entities)
        {
            DateTime dateTimeNow = DateTime.UtcNow;
            string blobName = await StorageContext.UpdateBlobCacheFileAsync<Entities.IdentityResource>(entities, StorageContext.IdentityResourceBlobCacheContainer);
            _logger.LogInformation($"{nameof(UpdateIdentityResourceCacheFileAsync)} client count {entities.Count()} saved in blob storage: {blobName}");
        }

        public async Task<IEnumerable<Entities.ApiResource>> GetLatestApiResourceCacheAsync()
        {
            try
            {
                return await StorageContext.GetLatestFromCacheBlobAsync<Entities.ApiResource>(StorageContext.ApiResourceBlobCacheContainer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{nameof(GetLatestApiResourceCacheAsync)} error");
            }
            return null;
        }

        public async Task<IEnumerable<Entities.IdentityResource>> GetLatestIdentityResourceCacheAsync()
        {
            try
            {
                return await StorageContext.GetLatestFromCacheBlobAsync<Entities.IdentityResource>(StorageContext.IdentityResourceBlobCacheContainer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{nameof(GetLatestIdentityResourceCacheAsync)} error");
            }
            return null;
        }

        private async Task<IEnumerable<Entities.ResourceScopeIndexTblEntity>> GetResourceScopeIndexTblEntitiesAsync(string scope, CloudTable table)
        {
            string partitionKeyFilter = TableQuery.GenerateFilterCondition("PartitionKey",
                QueryComparisons.Equal,
                KeyGeneratorHelper.GenerateHashValue(scope));

            TableQuery<Entities.ResourceScopeIndexTblEntity> tq = new TableQuery<Entities.ResourceScopeIndexTblEntity>();
            tq.FilterString = partitionKeyFilter;

            return (await StorageContext.GetAllByTableQueryAsync(tq, table));
        }

        private Entities.ResourceScopeIndexTblEntity GenerateResourceIndexEntity(string name, string scope)
        {
            return new Entities.ResourceScopeIndexTblEntity()
            {
                PartitionKey = KeyGeneratorHelper.GenerateHashValue(scope),
                RowKey = KeyGeneratorHelper.GenerateHashValue(name),
                ResourceName = name,
                ScopeName = scope,
                ETag = KeyGeneratorHelper.ETagWildCard
            };
        }                      

        /// <summary>
        /// Finds the API resource by name.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns></returns>
        public async Task<ApiResource> FindApiResourceAsync(string name)
        {
            var api = await StorageContext.GetEntityBlobAsync<Entities.ApiResource>(name, StorageContext.ApiResourceBlobContainer);

            if (api != null)
            {
                _logger.LogDebug("Found {api} API resource in blob storage", name);
            }
            else
            {
                _logger.LogDebug("Did not find {api} API resource in blob storage", name);
            }

            return api?.ToModel();
        }

        /// <summary>
        /// Gets API resources by scope name.
        /// </summary>
        /// <param name="scopeNames"></param>
        /// <returns></returns>
        public async Task<IEnumerable<ApiResource>> FindApiResourcesByScopeAsync(IEnumerable<string> scopeNames)
        {
            var entities = await GetResourcesByScopeAsync<Entities.ApiResource>(scopeNames, StorageContext.ApiResourceTable, StorageContext.ApiResourceBlobContainer);
            _logger.LogDebug("Found {scopes} API scopes in blob storage", entities.Count());

            return entities.Select(s => s?.ToModel());
        }

        private async Task<IEnumerable<Entity>> GetResourcesByScopeAsync<Entity>(IEnumerable<string> scopeNames, CloudTable table, CloudBlobContainer container) where Entity : class, new()
        {
            var scopeTasks = scopeNames.Distinct().Select(scope => GetResourceScopeIndexTblEntitiesAsync(scope, table));

            await Task.WhenAll(scopeTasks);

            IEnumerable<string> resourceNames = scopeTasks
                .Where(w => w != null && w.Result != null)
                .SelectMany(m => m.Result)
                .Where(w2 => !string.IsNullOrWhiteSpace(w2.ResourceName))
                .Select(s => s.ResourceName)
                .Distinct();

            var keyTasks = resourceNames.Select(resourceName => StorageContext.GetEntityBlobAsync<Entity>(resourceName, container)).ToArray();
            await Task.WhenAll(keyTasks);

            return keyTasks.Where(k => k?.Result != null).Select(s => s.Result);

        }

        /// <summary>
        /// Gets identity resources by scope name.
        /// </summary>
        /// <param name="scopeNames"></param>
        /// <returns></returns>
        public async Task<IEnumerable<IdentityResource>> FindIdentityResourcesByScopeAsync(IEnumerable<string> scopeNames)
        {

            var scopes = scopeNames.Where(w => !string.IsNullOrWhiteSpace(w)).Distinct();

            var keyTasks = scopes.Select(scope => StorageContext.GetEntityBlobAsync<Entities.IdentityResource>(scope, StorageContext.IdentityResourceBlobContainer));

            await Task.WhenAll(keyTasks).ConfigureAwait(false);

            IEnumerable<Entities.IdentityResource> entities = keyTasks.Where(k => k?.Result != null).Select(s => s.Result);

            _logger.LogDebug($"Found {scopes.Count()} Identity scopes in blob storage");

            return entities.Where(w => w != null).Select(s => s?.ToModel());

        }

        /// <summary>
        /// Gets all resources.
        /// </summary>
        /// <returns></returns>
        public async Task<Resources> GetAllResourcesAsync()
        {
            var identityTask = GetAllIdentityResourceEntitiesAsync();

            var apisTask = GetAllApiResourceEntitiesAsync();

            await Task.WhenAll(identityTask, apisTask);

            var identity = identityTask.Result;
            var apis = apisTask.Result;

            var result = new Resources(
                identity.Select(x => x.ToModel()).AsEnumerable(),
                apis.Select(x => x.ToModel()).AsEnumerable());

            _logger.LogDebug("Found {0} as all scopes in blob storage", result.IdentityResources.Select(x=>x.Name).Union(result.ApiResources.SelectMany(x=>x.Scopes).Select(x=>x.Name)));

            return result;
        }

        private async Task<IEnumerable<Entities.ApiResource>> GetAllApiResourceEntitiesAsync()
        {
            var entities = await GetLatestApiResourceCacheAsync();
            if (entities == null)
            {
                entities = await StorageContext.GetAllBlobEntitiesAsync<Entities.ApiResource>(StorageContext.ApiResourceBlobContainer, _logger);
                await UpdateApiResourceCacheFileAsync(entities);
            }

            return entities;
        }

        private async Task<IEnumerable<Entities.IdentityResource>> GetAllIdentityResourceEntitiesAsync()
        {
            var entities = await GetLatestIdentityResourceCacheAsync();
            if (entities == null)
            {
                entities = await StorageContext.GetAllBlobEntitiesAsync<Entities.IdentityResource>(StorageContext.IdentityResourceBlobContainer, _logger);
                await UpdateIdentityResourceCacheFileAsync(entities);
            }

            return entities;
        }

    }
}