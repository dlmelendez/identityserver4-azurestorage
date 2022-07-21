// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.
// Based on work from Brock Allen & Dominick Baier, https://github.com/IdentityServer/Duende.IdentityServer

using ElCamino.Duende.IdentityServer.AzureStorage.Contexts;
using ElCamino.Duende.IdentityServer.AzureStorage.Helpers;
using ElCamino.Duende.IdentityServer.AzureStorage.Interfaces;
using ElCamino.Duende.IdentityServer.AzureStorage.Mappers;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Model = Duende.IdentityServer.Models;
using ElCamino.Duende.IdentityServer.AzureStorage.Entities;
using Azure.Data.Tables;
using Azure;
using System.Text.Json;

namespace ElCamino.Duende.IdentityServer.AzureStorage.Stores
{
    /// <summary>
    /// Implementation of IResourceStore thats uses EF.
    /// </summary>
    /// <seealso cref="Duende.IdentityServer.Stores.IResourceStore" />
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
                var existingEntity = await StorageContext.GetEntityBlobAsync<Entities.ApiResource>(entity.Name, StorageContext.ApiResourceBlobContainer).ConfigureAwait(false);
                if (existingEntity != null 
                    && existingEntity.Scopes != null
                    && existingEntity.Scopes.Count > 0)
                {
                    //Remove old scope indexes
                    var deleteIndexes = existingEntity?.Scopes?.Select(s => s.Name).Distinct().Select(i => GenerateResourceIndexEntity(entity.Name, i));
                    await DeleteScopeIndexesAsync(deleteIndexes, StorageContext.ApiResourceTable).ConfigureAwait(false);
                }
                // Add new scope indexes
                var newIndexes = entity?.Scopes?.Select(s => s.Name).Distinct().Select(i => GenerateResourceIndexEntity(entity.Name, i));
                await CreateScopeIndexesAsync(newIndexes, StorageContext.ApiResourceTable).ConfigureAwait(false);
                await StorageContext.SaveBlobWithHashedKeyAsync(entity.Name,
                    JsonSerializer.Serialize(entity, StorageContext.JsonSerializerDefaultOptions), StorageContext.ApiResourceBlobContainer)
                    .ConfigureAwait(false);
                //Create ApiScopes that don't exist
                List<string> entityScopes = entity.Scopes?.Select(s => s.Name).ToList();
                if(entityScopes.Count > 0)
                {
                    List<Model.ApiScope> existingApiScopes  = (await FindApiScopesByNameAsync(entityScopes).ConfigureAwait(false)).ToList();
                    foreach(string entityScope in entityScopes.Where(w => !String.IsNullOrEmpty(w)).Distinct())
                    {
                        if(!existingApiScopes.Any(a => entityScope.Equals(a.Name, StringComparison.Ordinal)))
                        {
                            await StoreAsync(new Model.ApiScope() { Name = entityScope }).ConfigureAwait(false);
                        }
                    }
                }
                var entities = await GetAllApiResourceEntitiesAsync().ConfigureAwait(false);
                entities = entities.Where(e => entity.Name != e.Name).Concat(new Entities.ApiResource[] { entity });
                await UpdateApiResourceCacheFileAsync(entities).ConfigureAwait(false);
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

        /// <summary>
        /// Should be used for migration only
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<Entities.ApiResourceV3>> GetAllApiResourceV3EntitiesAsync()
        {
            var entities = await StorageContext.SafeGetAllBlobEntitiesAsync<Entities.ApiResourceV3>(StorageContext.ApiResourceBlobContainer, _logger)
                 .ToListAsync()
                 .ConfigureAwait(false);

            return entities;
        }

        /// <summary>
        /// Migrate ApiResources to create associated ApiScopes in V4 from V3 schema
        /// </summary>
        /// <returns></returns>
        public async Task MigrateV3ApiScopesAsync()
        {
            try
            {
                foreach (Entities.ApiResourceV3 apiResourceV3 in await GetAllApiResourceV3EntitiesAsync().ConfigureAwait(false))
                {
                    try
                    {
                        await MigrateV3ApiScopeAsync(apiResourceV3).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, $"{nameof(MigrateV3ApiScopeAsync)}, {nameof(Entities.ApiResourceV3)}.{nameof(Entities.ApiResourceV3.Name)}: {apiResourceV3?.Name?.ToString()}");
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogDebug(e, $"{nameof(MigrateV3ApiScopesAsync)}");
            }
        }

        /// <summary>
        /// Takes a v3 ApiResource and creates ApiScopes if they don't exist already.
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public async Task MigrateV3ApiScopeAsync(Entities.ApiResourceV3 entity)
        {
            try
            {
                //Create ApiScopes that don't exist
                List<Entities.ApiScope> entityScopes = entity.Scopes.ToList();
                if (entityScopes.Count > 0)
                {
                    foreach (Entities.ApiScope entityScope in entityScopes)
                    {
                        if (!String.IsNullOrWhiteSpace(entityScope.Name))
                        {
                            await StoreAsync(entityScope.ToModel()).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (AggregateException agg)
            {
                ExceptionHelper.LogStorageExceptions(agg, (tblEx) =>
                {
                    _logger.LogWarning("exception updating {apiName} api resource in table storage: {error}", entity.Name, tblEx.Message);
                }, (blobEx) =>
                {
                    _logger.LogWarning("exception updating {apiName} api resource in blob storage: {error}", entity.Name, blobEx.Message);
                });
                throw;
            }
        }

        public async Task StoreAsync(Model.IdentityResource model)
        {
            var entity = model.ToEntity();
            try
            {                
                await StorageContext
                    .SaveBlobWithHashedKeyAsync(entity.Name, JsonSerializer.Serialize(entity, StorageContext.JsonSerializerDefaultOptions), StorageContext.IdentityResourceBlobContainer)
                    .ConfigureAwait(false);
                var entities = await GetAllIdentityResourceEntitiesAsync().ConfigureAwait(false);
                entities = entities.Where(e => entity.Name != e.Name).Concat(new Entities.IdentityResource[] { entity });
                await UpdateIdentityResourceCacheFileAsync(entities).ConfigureAwait(false);
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

        public async Task StoreAsync(Model.ApiScope model) 
        {
            var entity = model.ToEntity();
            try
            {
                await StorageContext
                    .SaveBlobWithHashedKeyAsync(entity.Name, JsonSerializer.Serialize(entity, StorageContext.JsonSerializerDefaultOptions), StorageContext.ApiScopeBlobContainer)
                    .ConfigureAwait(false);
                var entities = await GetAllApiScopeEntitiesAsync().ConfigureAwait(false);
                entities = entities.Where(e => entity.Name != e.Name).Concat(new Entities.ApiScope[] { entity });
                await UpdateApiScopeCacheFileAsync(entities).ConfigureAwait(false);
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
                await StorageContext.DeleteBlobAsync(name, StorageContext.IdentityResourceBlobContainer).ConfigureAwait(false);
                var entities = await GetAllIdentityResourceEntitiesAsync().ConfigureAwait(false);
                entities = entities.Where(e => name != e.Name);
                await UpdateIdentityResourceCacheFileAsync(entities).ConfigureAwait(false);
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
                var existingEntity = await StorageContext.GetEntityBlobAsync<Entities.ApiResource>(name, StorageContext.ApiResourceBlobContainer).ConfigureAwait(false);
                if (existingEntity != null
                    && existingEntity.Scopes != null
                    && existingEntity.Scopes.Count > 0)
                {
                    //Remove old scope indexes
                    var deleteIndexes = existingEntity?.Scopes?.Select(s => s.Name).Distinct().Select(i => GenerateResourceIndexEntity(name, i));
                    await DeleteScopeIndexesAsync(deleteIndexes, StorageContext.ApiResourceTable).ConfigureAwait(false);
                }
                await StorageContext.DeleteBlobAsync(name, StorageContext.ApiResourceBlobContainer).ConfigureAwait(false);
                var entities = await GetAllApiResourceEntitiesAsync().ConfigureAwait(false);
                entities = entities.Where(e => name != e.Name);
                await UpdateApiResourceCacheFileAsync(entities).ConfigureAwait(false);
            }
            catch (AggregateException agg)
            {
                ExceptionHelper.LogStorageExceptions(agg, tableStorageLogger: null, (blobEx) =>
                {
                    _logger.LogWarning("exception removing {0} client in blob storage: {1}", name, blobEx.Message);
                });
            }
        }

        private async Task DeleteScopeIndexesAsync(IEnumerable<Entities.ResourceScopeIndexTblEntity> indexes, TableClient table)
        {
            if (indexes != null )
            {                
                await Task.WhenAll(indexes.Select((index) => table.DeleteEntityAsync(index.PartitionKey, index.RowKey, KeyGeneratorHelper.ETagWildCard)))
                    .ConfigureAwait(false);
            }
        }
        
        private async Task CreateScopeIndexesAsync(IEnumerable<Entities.ResourceScopeIndexTblEntity> indexes, TableClient table)
        {
            if (indexes != null)
            {
                await Task.WhenAll(indexes.Select((index) => table.UpdateEntityAsync(index, KeyGeneratorHelper.ETagWildCard, TableUpdateMode.Replace)))
                    .ConfigureAwait(false);
            }
        }

        public async Task UpdateApiResourceCacheFileAsync(IEnumerable<Entities.ApiResource> entities)
        {
            DateTime dateTimeNow = DateTime.UtcNow;
            (string blobName, int count) = await StorageContext.UpdateBlobCacheFileAsync<Entities.ApiResource>(entities, StorageContext.ApiResourceBlobCacheContainer)
                .ConfigureAwait(false);
            _logger.LogInformation($"{nameof(UpdateApiResourceCacheFileAsync)} client count {count} saved in blob storage: {blobName}");
        }

        public async Task UpdateApiScopeCacheFileAsync(IEnumerable<Entities.ApiScope> entities)
        {
            DateTime dateTimeNow = DateTime.UtcNow;
            (string blobName, int count) = await StorageContext.UpdateBlobCacheFileAsync<Entities.ApiScope>(entities, StorageContext.ApiScopeBlobCacheContainer)
                .ConfigureAwait(false);
            _logger.LogInformation($"{nameof(UpdateApiScopeCacheFileAsync)} client count {count} saved in blob storage: {blobName}");
        }

        public async Task UpdateIdentityResourceCacheFileAsync(IEnumerable<Entities.IdentityResource> entities)
        {
            DateTime dateTimeNow = DateTime.UtcNow;
            (string blobName, int count) = await StorageContext.UpdateBlobCacheFileAsync<Entities.IdentityResource>(entities, StorageContext.IdentityResourceBlobCacheContainer)
                .ConfigureAwait(false);
            _logger.LogInformation($"{nameof(UpdateIdentityResourceCacheFileAsync)} client count {count} saved in blob storage: {blobName}");
        }

        public async Task<IEnumerable<Entities.ApiResource>> GetLatestApiResourceCacheAsync()
        {
            try
            {
                return await StorageContext.GetLatestFromCacheBlobAsync<Entities.ApiResource>(StorageContext.ApiResourceBlobCacheContainer).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{nameof(GetLatestApiResourceCacheAsync)} error");
            }
            return null;
        }

        public async Task<IEnumerable<Entities.ApiScope>> GetLatestApiScopeCacheAsync()
        {
            try
            {
                return await StorageContext.GetLatestFromCacheBlobAsync<Entities.ApiScope>(StorageContext.ApiScopeBlobCacheContainer).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{nameof(GetLatestApiScopeCacheAsync)} error");
            }
            return null;
        }

        public async Task<IEnumerable<Entities.IdentityResource>> GetLatestIdentityResourceCacheAsync()
        {
            try
            {
                return await StorageContext.GetLatestFromCacheBlobAsync<Entities.IdentityResource>(StorageContext.IdentityResourceBlobCacheContainer).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{nameof(GetLatestIdentityResourceCacheAsync)} error");
            }
            return null;
        }

        private async Task<IEnumerable<Entities.ResourceScopeIndexTblEntity>> GetResourceScopeIndexTblEntitiesAsync(string scope, TableClient table)
        {
            string partitionKeyFilter = TableQuery.GenerateFilterCondition("PartitionKey",
                QueryComparisons.Equal,
                KeyGeneratorHelper.GenerateHashValue(scope));

            TableQuery tq = new TableQuery();
            tq.FilterString = partitionKeyFilter;

            return await StorageContext.GetAllByTableQueryAsync<Entities.ResourceScopeIndexTblEntity>(tq, table).ToListAsync();
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
        private async Task<Model.ApiResource> FindApiResourceAsync(string name)
        {
            var api = await StorageContext.GetEntityBlobAsync<Entities.ApiResource>(name, StorageContext.ApiResourceBlobContainer).ConfigureAwait(false);

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
        public async Task<IEnumerable<Model.ApiResource>> FindApiResourcesByScopeNameAsync(IEnumerable<string> scopeNames)
        {
            var entities = await GetResourcesByScopeAsync<Entities.ApiResource>(scopeNames, StorageContext.ApiResourceTable, StorageContext.ApiResourceBlobContainer).ConfigureAwait(false);
            _logger.LogDebug("Found {scopes} API scopes in blob storage", entities.Count());

            return entities.Select(s => s?.ToModel());
        }

        private async Task<IEnumerable<Entity>> GetResourcesByScopeAsync<Entity>(IEnumerable<string> scopeNames, TableClient table, BlobContainerClient container) where Entity : class, new()
        {
            var scopeTasks = scopeNames.Distinct().Select(scope => GetResourceScopeIndexTblEntitiesAsync(scope, table));

            await Task.WhenAll(scopeTasks).ConfigureAwait(false);

            IEnumerable<string> resourceNames = scopeTasks
                .Where(w => w != null && w.Result != null)
                .SelectMany(m => m.Result)
                .Where(w2 => !string.IsNullOrWhiteSpace(w2.ResourceName))
                .Select(s => s.ResourceName)
                .Distinct();

            var keyTasks = resourceNames.Select(resourceName => StorageContext.GetEntityBlobAsync<Entity>(resourceName, container)).ToArray();
            await Task.WhenAll(keyTasks).ConfigureAwait(false);

            return keyTasks.Where(k => k?.Result != null).Select(s => s.Result);

        }

        /// <summary>
        /// Gets identity resources by scope name.
        /// </summary>
        /// <param name="scopeNames"></param>
        /// <returns></returns>
        public async Task<IEnumerable<Model.IdentityResource>> FindIdentityResourcesByScopeNameAsync(IEnumerable<string> scopeNames)
        {

            var scopes = scopeNames.Where(w => !string.IsNullOrWhiteSpace(w)).Distinct();

            var keyTasks = scopes
                .Select(scope => StorageContext.GetEntityBlobAsync<Entities.IdentityResource>(scope, StorageContext.IdentityResourceBlobContainer)).ToArray();

            await Task.WhenAll(keyTasks).ConfigureAwait(false);

            IEnumerable<Entities.IdentityResource> entities = keyTasks.Where(k => k?.Result != null).Select(s => s.Result);

            _logger.LogDebug($"Found {scopes.Count()} {nameof(Model.IdentityResource)} scopes in blob storage");

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

            var apiScopeTask = GetAllApiScopeEntitiesAsync();

            await Task.WhenAll(identityTask, apisTask, apiScopeTask).ConfigureAwait(false);

            var identity = identityTask.Result;
            var apis = apisTask.Result;
            var apiscopes = apiScopeTask.Result;

            var result = new Resources(
                identity.Select(x => x.ToModel()).AsEnumerable(),
                apis.Select(x => x.ToModel()).AsEnumerable(),
                apiscopes.Select(x => x.ToModel()).AsEnumerable());

            _logger.LogDebug("Found {0} as all scopes in blob storage", result.IdentityResources.Select(x=>x.Name).Union(result.ApiResources.SelectMany(x=>x.Scopes).Select(x=> x)));

            return result;
        }

        private async Task<IEnumerable<Entities.ApiResource>> GetAllApiResourceEntitiesAsync()
        {
            var entities = await GetLatestApiResourceCacheAsync().ConfigureAwait(false);
            if (entities == null)
            {
                entities = await StorageContext.GetAllBlobEntitiesAsync<Entities.ApiResource>(StorageContext.ApiResourceBlobContainer, _logger)
                    .ToListAsync()
                    .ConfigureAwait(false);
                await UpdateApiResourceCacheFileAsync(entities).ConfigureAwait(false);
            }

            return entities;
        }
        
        private async Task<IEnumerable<Entities.IdentityResource>> GetAllIdentityResourceEntitiesAsync()
        {
            var entities = await GetLatestIdentityResourceCacheAsync().ConfigureAwait(false);
            if (entities == null)
            {
                entities = await StorageContext.GetAllBlobEntitiesAsync<Entities.IdentityResource>(StorageContext.IdentityResourceBlobContainer, _logger)
                    .ToListAsync()
                    .ConfigureAwait(false);
                await UpdateIdentityResourceCacheFileAsync(entities).ConfigureAwait(false);
            }

            return entities;
        }

        private async Task<IEnumerable<Entities.ApiScope>> GetAllApiScopeEntitiesAsync()
        {
            var entities = await GetLatestApiScopeCacheAsync().ConfigureAwait(false);
            if (entities == null)
            {
                entities = await StorageContext.GetAllBlobEntitiesAsync<Entities.ApiScope>(StorageContext.ApiScopeBlobContainer, _logger)
                    .ToListAsync()
                    .ConfigureAwait(false);
                await UpdateApiScopeCacheFileAsync(entities).ConfigureAwait(false);
            }

            return entities;
        }

        public async Task<IEnumerable<Model.ApiScope>> FindApiScopesByNameAsync(IEnumerable<string> scopeNames)
        {
            var scopes = scopeNames.Where(w => !string.IsNullOrWhiteSpace(w)).Distinct();

            var keyTasks = scopes.Select(scope => StorageContext.GetEntityBlobAsync<Entities.ApiScope>(scope, StorageContext.ApiScopeBlobContainer)).ToArray();

            await Task.WhenAll(keyTasks).ConfigureAwait(false);

            IEnumerable<Entities.ApiScope> entities = keyTasks.Where(k => k?.Result != null).Select(s => s.Result);

            _logger.LogDebug($"Found {scopes.Count()} {nameof(Model.ApiScope)} scopes in blob storage");

            return entities.Where(w => w != null).Select(s => s?.ToModel());
        }

        public async Task<IEnumerable<Model.ApiResource>> FindApiResourcesByNameAsync(IEnumerable<string> apiResourceNames)
        {
            IEnumerable<string> apiResources = apiResourceNames.Where(w => !string.IsNullOrWhiteSpace(w)).Distinct();

            Task<Model.ApiResource>[] keyTasks = apiResources.Select(apiResourceName => FindApiResourceAsync(apiResourceName)).ToArray();

            await Task.WhenAll(keyTasks).ConfigureAwait(false);

            var models = keyTasks.Select(t => t.Result);
            _logger.LogDebug($"Found {models.Count()} {nameof(Model.ApiResource)} scopes in blob storage");

            return models;
        }
    }
}