// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.
// Based on work from Brock Allen & Dominick Baier, https://github.com/IdentityServer/Duende.IdentityServer

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using ElCamino.IdentityServer.AzureStorage.Contexts;
using ElCamino.IdentityServer.AzureStorage.Helpers;
using ElCamino.IdentityServer.AzureStorage.Mappers;
using Microsoft.Extensions.Logging;
using Model = Duende.IdentityServer.Models;

namespace ElCamino.IdentityServer.AzureStorage.Stores
{
    /// <summary>
    /// Implementation of IResourceStore thats uses Azure Storage.
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

        public async Task StoreAsync(Model.ApiResource model, CancellationToken cancellationToken = default)
        {
            var entity = model.ToEntity();
            try
            {
                // Remove old scope indexes
                var existingEntity = await StorageContext.GetEntityBlobAsync<Entities.ApiResource>(entity.Name, StorageContext.ApiResourceBlobContainer, cancellationToken).ConfigureAwait(false);
                if (existingEntity != null 
                    && existingEntity.Scopes != null
                    && existingEntity.Scopes.Count > 0)
                {
                    //Remove old scope indexes
                    var deleteIndexes = existingEntity?.Scopes?.Select(s => s.Name).Distinct().Select(i => GenerateResourceIndexEntity(entity.Name, i));
                    await DeleteScopeIndexesAsync(deleteIndexes, StorageContext.ApiResourceTable, cancellationToken).ConfigureAwait(false);
                }
                // Add new scope indexes
                var newIndexes = entity?.Scopes?.Select(s => s.Name).Distinct().Select(i => GenerateResourceIndexEntity(entity.Name, i));
                await CreateScopeIndexesAsync(newIndexes, StorageContext.ApiResourceTable, cancellationToken).ConfigureAwait(false);
                await StorageContext.SaveBlobWithHashedKeyAsync(entity.Name,
                    JsonSerializer.Serialize(entity, StorageContext.JsonSerializerDefaultOptions), StorageContext.ApiResourceBlobContainer, cancellationToken)
                    .ConfigureAwait(false);
                //Create ApiScopes that don't exist
                List<string> entityScopes = entity.Scopes?.Select(s => s.Name).ToList();
                if(entityScopes.Count > 0)
                {
                    List<Model.ApiScope> existingApiScopes  = (await FindApiScopesByNameAsync(entityScopes, cancellationToken).ConfigureAwait(false)).ToList();
                    foreach(string entityScope in entityScopes.Where(w => !String.IsNullOrEmpty(w)).Distinct())
                    {
                        if(!existingApiScopes.Any(a => entityScope.Equals(a.Name, StringComparison.Ordinal)))
                        {
                            await StoreAsync(new Model.ApiScope() { Name = entityScope }, cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
                var entities = await GetAllApiResourceEntitiesAsync(cancellationToken).ConfigureAwait(false);
                entities = entities.Where(e => entity.Name != e.Name).Concat(new Entities.ApiResource[] { entity });
                await UpdateApiResourceCacheFileAsync(entities, cancellationToken).ConfigureAwait(false);
            }
            catch (AggregateException agg)
            {
                _logger.LogError(agg, agg.Message);
                ExceptionHelper.LogStorageExceptions(agg, (rfex) =>
                {
                    _logger.LogStorageError(rfex);
                    _logger.LogWarning("exception updating {apiName} api resource in storage: {error}", model.Name, rfex.Message);
                });
                throw;
            }
            catch (RequestFailedException rfex)
            {
                _logger.LogStorageError(rfex);
                _logger.LogWarning("exception updating {apiName} api resource in storage: {error}", model.Name, rfex.Message);
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
                        if (!string.IsNullOrWhiteSpace(entityScope.Name))
                        {
                            await StoreAsync(entityScope.ToModel()).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (AggregateException agg)
            {
                _logger.LogError(agg, agg.Message);
                ExceptionHelper.LogStorageExceptions(agg, (rfex) =>
                {
                    _logger.LogStorageError(rfex);
                    _logger.LogWarning("exception updating {apiName} api resource in storage: {error}", entity.Name, rfex.Message);
                });
                throw;
            }
            catch (RequestFailedException rfex)
            {
                _logger.LogError(rfex, rfex.Message);
                _logger.LogWarning($"storage exception ErrorCode: {rfex.ErrorCode ?? string.Empty}, Http Status Code: {rfex.Status}");
                throw;
            }
        }

        public async Task StoreAsync(Model.IdentityResource model, CancellationToken cancellationToken = default)
        {
            var entity = model.ToEntity();
            try
            {                
                await StorageContext
                    .SaveBlobWithHashedKeyAsync(entity.Name, JsonSerializer.Serialize(entity, StorageContext.JsonSerializerDefaultOptions), StorageContext.IdentityResourceBlobContainer, cancellationToken)
                    .ConfigureAwait(false);
                var entities = await GetAllIdentityResourceEntitiesAsync(cancellationToken).ConfigureAwait(false);
                entities = entities.Where(e => entity.Name != e.Name).Concat(new Entities.IdentityResource[] { entity });
                await UpdateIdentityResourceCacheFileAsync(entities, cancellationToken).ConfigureAwait(false);
            }
            catch (AggregateException agg)
            {
                _logger.LogError(agg, agg.Message);
                ExceptionHelper.LogStorageExceptions(agg, (rfex) =>
                {
                    _logger.LogStorageError(rfex);
                    _logger.LogWarning("exception updating {apiName} identity resource in storage: {error}", model.Name, rfex.Message);
                });
                throw;
            }
            catch (RequestFailedException rfex)
            {
                _logger.LogStorageError(rfex);
                _logger.LogWarning("exception updating {apiName} identity resource in storage: {error}", model.Name, rfex.Message);
                throw;
            }

        }

        public async Task StoreAsync(Model.ApiScope model, CancellationToken cancellationToken = default) 
        {
            var entity = model.ToEntity();
            try
            {
                await StorageContext
                    .SaveBlobWithHashedKeyAsync(entity.Name, JsonSerializer.Serialize(entity, StorageContext.JsonSerializerDefaultOptions), StorageContext.ApiScopeBlobContainer, cancellationToken)
                    .ConfigureAwait(false);
                var entities = await GetAllApiScopeEntitiesAsync(cancellationToken).ConfigureAwait(false);
                entities = entities.Where(e => entity.Name != e.Name).Concat(new Entities.ApiScope[] { entity });
                await UpdateApiScopeCacheFileAsync(entities, cancellationToken).ConfigureAwait(false);
            }
            catch (AggregateException agg)
            {
                _logger.LogError(agg, agg.Message);
                ExceptionHelper.LogStorageExceptions(agg, (rfex) =>
                {
                    _logger.LogStorageError(rfex);
                    _logger.LogWarning("exception updating {apiName} identity resource in storage: {error}", model.Name, rfex.Message);
                });
                throw;
            }
            catch (RequestFailedException rfex)
            {
                _logger.LogStorageError(rfex);
                _logger.LogWarning("exception updating {apiName} identity resource in storage: {error}", model.Name, rfex.Message);
                throw;
            }
        }

        public async Task RemoveIdentityResourceAsync(string name, CancellationToken cancellationToken = default)
        {
            try
            {
                await StorageContext.DeleteBlobAsync(name, StorageContext.IdentityResourceBlobContainer, cancellationToken).ConfigureAwait(false);
                var entities = await GetAllIdentityResourceEntitiesAsync(cancellationToken).ConfigureAwait(false);
                entities = entities.Where(e => name != e.Name);
                await UpdateIdentityResourceCacheFileAsync(entities, cancellationToken).ConfigureAwait(false);
            }
            catch (AggregateException agg)
            {
                _logger.LogError(agg, agg.Message);
                ExceptionHelper.LogStorageExceptions(agg, storageLogger: (rfex) =>
                {
                    _logger.LogStorageError(rfex);
                    _logger.LogWarning("exception removing {clientId} client in blob storage: {error}", name, rfex.Message);
                });
                throw;
            }
            catch (RequestFailedException rfex)
            {
                _logger.LogStorageError(rfex);
                _logger.LogWarning("exception removing {clientId} client in blob storage: {error}", name, rfex.Message);
                throw;
            }
        }

        public async Task RemoveApiResourceAsync(string name, CancellationToken cancellationToken = default)
        {
            try
            {
                // Remove old scope indexes
                var existingEntity = await StorageContext.GetEntityBlobAsync<Entities.ApiResource>(name, StorageContext.ApiResourceBlobContainer, cancellationToken).ConfigureAwait(false);
                if (existingEntity != null
                    && existingEntity.Scopes != null
                    && existingEntity.Scopes.Count > 0)
                {
                    //Remove old scope indexes
                    var deleteIndexes = existingEntity?.Scopes?.Select(s => s.Name).Distinct().Select(i => GenerateResourceIndexEntity(name, i));
                    await DeleteScopeIndexesAsync(deleteIndexes, StorageContext.ApiResourceTable, cancellationToken).ConfigureAwait(false);
                }
                await StorageContext.DeleteBlobAsync(name, StorageContext.ApiResourceBlobContainer, cancellationToken).ConfigureAwait(false);
                var entities = await GetAllApiResourceEntitiesAsync(cancellationToken).ConfigureAwait(false);
                entities = entities.Where(e => name != e.Name);
                await UpdateApiResourceCacheFileAsync(entities, cancellationToken).ConfigureAwait(false);
            }
            catch (AggregateException agg)
            {
                _logger.LogError(agg, agg.Message);
                ExceptionHelper.LogStorageExceptions(agg, storageLogger:(rfex) =>
                {
                    _logger.LogStorageError(rfex);
                    _logger.LogWarning("exception removing {name} client in blob storage", name);
                });
                throw;
            }
            catch (RequestFailedException rfex)
            {
                _logger.LogStorageError(rfex);
                _logger.LogWarning("exception removing {name} client in blob storage", name);

                throw;
            }
        }

        private async Task DeleteScopeIndexesAsync(IEnumerable<Entities.ResourceScopeIndexTblEntity> indexes, TableClient table, CancellationToken cancellationToken = default)
        {
            if (indexes != null )
            {                
                await Task.WhenAll(indexes.Select((index) => table.DeleteEntityAsync(index.PartitionKey, index.RowKey, KeyGeneratorHelper.ETagWildCard, cancellationToken)))
                    .ConfigureAwait(false);
            }
        }
        
        private async Task CreateScopeIndexesAsync(IEnumerable<Entities.ResourceScopeIndexTblEntity> indexes, TableClient table, CancellationToken cancellationToken = default)
        {
            if (indexes != null)
            {
                await Task.WhenAll(indexes.Select((index) => table.UpsertEntityAsync(index, TableUpdateMode.Replace, cancellationToken)))
                    .ConfigureAwait(false);
            }
        }

        public async Task UpdateApiResourceCacheFileAsync(IEnumerable<Entities.ApiResource> entities, CancellationToken cancellationToken = default)
        {
            (string blobName, int count) = await StorageContext.UpdateBlobCacheFileAsync<Entities.ApiResource>(entities, StorageContext.ApiResourceBlobCacheContainer, cancellationToken)
                .ConfigureAwait(false);
            _logger.LogInformation($"{nameof(UpdateApiResourceCacheFileAsync)} client count {count} saved in blob storage: {blobName}");
        }

        public async Task UpdateApiScopeCacheFileAsync(IEnumerable<Entities.ApiScope> entities, CancellationToken cancellationToken = default)
        {
            (string blobName, int count) = await StorageContext.UpdateBlobCacheFileAsync<Entities.ApiScope>(entities, StorageContext.ApiScopeBlobCacheContainer, cancellationToken)
                .ConfigureAwait(false);
            _logger.LogInformation($"{nameof(UpdateApiScopeCacheFileAsync)} client count {count} saved in blob storage: {blobName}");
        }

        public async Task UpdateIdentityResourceCacheFileAsync(IEnumerable<Entities.IdentityResource> entities, CancellationToken cancellationToken = default)
        {
            (string blobName, int count) = await StorageContext.UpdateBlobCacheFileAsync<Entities.IdentityResource>(entities, StorageContext.IdentityResourceBlobCacheContainer, cancellationToken)
                .ConfigureAwait(false);
            _logger.LogInformation($"{nameof(UpdateIdentityResourceCacheFileAsync)} client count {count} saved in blob storage: {blobName}");
        }

        public async Task<IEnumerable<Entities.ApiResource>> GetLatestApiResourceCacheAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await StorageContext.GetLatestFromCacheBlobAsync<Entities.ApiResource>(StorageContext.ApiResourceBlobCacheContainer, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{nameof(GetLatestApiResourceCacheAsync)} error");
            }
            return null;
        }

        public async Task<IEnumerable<Entities.ApiScope>> GetLatestApiScopeCacheAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await StorageContext.GetLatestFromCacheBlobAsync<Entities.ApiScope>(StorageContext.ApiScopeBlobCacheContainer, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{nameof(GetLatestApiScopeCacheAsync)} error");
            }
            return null;
        }

        public async Task<IEnumerable<Entities.IdentityResource>> GetLatestIdentityResourceCacheAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await StorageContext.GetLatestFromCacheBlobAsync<Entities.IdentityResource>(StorageContext.IdentityResourceBlobCacheContainer, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{nameof(GetLatestIdentityResourceCacheAsync)} error");
            }
            return null;
        }

        private async Task<IEnumerable<Entities.ResourceScopeIndexTblEntity>> GetResourceScopeIndexTblEntitiesAsync(string scope, TableClient table, CancellationToken cancellationToken = default)
        {
            var partitionKeyFilter = TableQuery.GenerateFilterCondition(nameof(TableEntity.PartitionKey),
                QueryComparisons.Equal,
                KeyGeneratorHelper.GenerateHashValue(scope));

            return await table.QueryAsync<Entities.ResourceScopeIndexTblEntity>(filter: partitionKeyFilter.ToString(), cancellationToken: cancellationToken).ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        private static Entities.ResourceScopeIndexTblEntity GenerateResourceIndexEntity(string name, string scope)
        {
            return new Entities.ResourceScopeIndexTblEntity()
            {
                PartitionKey = KeyGeneratorHelper.GenerateHashValue(scope).ToString(),
                RowKey = KeyGeneratorHelper.GenerateHashValue(name).ToString(),
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
        private async Task<Model.ApiResource> FindApiResourceAsync(string name, CancellationToken cancellationToken = default)
        {
            var api = await StorageContext.GetEntityBlobAsync<Entities.ApiResource>(name, StorageContext.ApiResourceBlobContainer, cancellationToken).ConfigureAwait(false);

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
        public Task<IEnumerable<Model.ApiResource>> FindApiResourcesByScopeNameAsync(IEnumerable<string> scopeNames)
        {
            return FindApiResourcesByScopeNameAsync(scopeNames, default);
        }

        public async Task<IEnumerable<Model.ApiResource>> FindApiResourcesByScopeNameAsync(IEnumerable<string> scopeNames, CancellationToken cancellationToken = default)
        {
            var entities = await GetResourcesByScopeAsync<Entities.ApiResource>(scopeNames, StorageContext.ApiResourceTable, StorageContext.ApiResourceBlobContainer, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Found {scopes} API scopes in blob storage", entities.Count());

            return entities.Select(s => s?.ToModel());
        }

        private async Task<IEnumerable<Entity>> GetResourcesByScopeAsync<Entity>(IEnumerable<string> scopeNames, TableClient table, BlobContainerClient container, CancellationToken cancellationToken = default) where Entity : class, new()
        {
            var scopeTasks = scopeNames.Distinct().Select(scope => GetResourceScopeIndexTblEntitiesAsync(scope, table, cancellationToken));

            await Task.WhenAll(scopeTasks).ConfigureAwait(false);

            IEnumerable<string> resourceNames = scopeTasks
                .Where(w => w != null && w.Result != null)
                .SelectMany(m => m.Result)
                .Where(w2 => !string.IsNullOrWhiteSpace(w2.ResourceName))
                .Select(s => s.ResourceName)
                .Distinct();

            var keyTasks = resourceNames.Select(resourceName => StorageContext.GetEntityBlobAsync<Entity>(resourceName, container, cancellationToken)).ToArray();
            await Task.WhenAll(keyTasks).ConfigureAwait(false);

            return keyTasks.Where(k => k?.Result != null).Select(s => s.Result);

        }

        /// <summary>
        /// Gets identity resources by scope name.
        /// </summary>
        /// <param name="scopeNames"></param>
        /// <returns></returns>
        public Task<IEnumerable<Model.IdentityResource>> FindIdentityResourcesByScopeNameAsync(IEnumerable<string> scopeNames)
        {
            return FindIdentityResourcesByScopeNameAsync(scopeNames, default);
        }

        public async Task<IEnumerable<Model.IdentityResource>> FindIdentityResourcesByScopeNameAsync(IEnumerable<string> scopeNames, CancellationToken cancellationToken = default)
        {
            var scopes = scopeNames.Where(w => !string.IsNullOrWhiteSpace(w)).Distinct();

            var keyTasks = scopes
                .Select(scope => StorageContext.GetEntityBlobAsync<Entities.IdentityResource>(scope, StorageContext.IdentityResourceBlobContainer, cancellationToken)).ToArray();

            await Task.WhenAll(keyTasks).ConfigureAwait(false);

            IEnumerable<Entities.IdentityResource> entities = keyTasks.Where(k => k?.Result != null).Select(s => s.Result);

            _logger.LogDebug($"Found {scopes.Count()} {nameof(Model.IdentityResource)} scopes in blob storage");

            return entities.Where(w => w != null).Select(s => s?.ToModel());

        }

        /// <summary>
        /// Gets all resources.
        /// </summary>
        /// <returns></returns>
        public Task<Resources> GetAllResourcesAsync()
        {
            return GetAllResourcesAsync(default);
        }

        public async Task<Resources> GetAllResourcesAsync(CancellationToken cancellationToken = default)
        {
            var identityTask = GetAllIdentityResourceEntitiesAsync(cancellationToken);

            var apisTask = GetAllApiResourceEntitiesAsync(cancellationToken);

            var apiScopeTask = GetAllApiScopeEntitiesAsync(cancellationToken);

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

        private async Task<IEnumerable<Entities.ApiResource>> GetAllApiResourceEntitiesAsync(CancellationToken cancellationToken = default)
        {
            var entities = await GetLatestApiResourceCacheAsync(cancellationToken).ConfigureAwait(false);
            if (entities == null)
            {
                entities = await StorageContext.GetAllBlobEntitiesAsync<Entities.ApiResource>(StorageContext.ApiResourceBlobContainer, _logger, cancellationToken)
                    .ToListAsync()
                    .ConfigureAwait(false);
                await UpdateApiResourceCacheFileAsync(entities, cancellationToken).ConfigureAwait(false);
            }

            return entities;
        }
        
        private async Task<IEnumerable<Entities.IdentityResource>> GetAllIdentityResourceEntitiesAsync(CancellationToken cancellationToken = default)
        {
            var entities = await GetLatestIdentityResourceCacheAsync(cancellationToken).ConfigureAwait(false);
            if (entities == null)
            {
                entities = await StorageContext.GetAllBlobEntitiesAsync<Entities.IdentityResource>(StorageContext.IdentityResourceBlobContainer, _logger, cancellationToken)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                await UpdateIdentityResourceCacheFileAsync(entities, cancellationToken).ConfigureAwait(false);
            }

            return entities;
        }

        private async Task<IEnumerable<Entities.ApiScope>> GetAllApiScopeEntitiesAsync(CancellationToken cancellationToken = default)
        {
            var entities = await GetLatestApiScopeCacheAsync(cancellationToken).ConfigureAwait(false);
            if (entities == null)
            {
                entities = await StorageContext.GetAllBlobEntitiesAsync<Entities.ApiScope>(StorageContext.ApiScopeBlobContainer, _logger, cancellationToken)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                await UpdateApiScopeCacheFileAsync(entities, cancellationToken).ConfigureAwait(false);
            }

            return entities;
        }

        public Task<IEnumerable<Model.ApiScope>> FindApiScopesByNameAsync(IEnumerable<string> scopeNames)
        {
            return FindApiScopesByNameAsync(scopeNames, default);
        }

        public async Task<IEnumerable<Model.ApiScope>> FindApiScopesByNameAsync(IEnumerable<string> scopeNames, CancellationToken cancellationToken = default)
        {
            var scopes = scopeNames.Where(w => !string.IsNullOrWhiteSpace(w)).Distinct();

            var keyTasks = scopes.Select(scope => StorageContext.GetEntityBlobAsync<Entities.ApiScope>(scope, StorageContext.ApiScopeBlobContainer, cancellationToken)).ToArray();

            await Task.WhenAll(keyTasks).ConfigureAwait(false);

            IEnumerable<Entities.ApiScope> entities = keyTasks.Where(k => k?.Result != null).Select(s => s.Result);

            _logger.LogDebug($"Found {scopes.Count()} {nameof(Model.ApiScope)} scopes in blob storage");

            return entities.Where(w => w != null).Select(s => s?.ToModel());
        }

        public Task<IEnumerable<Model.ApiResource>> FindApiResourcesByNameAsync(IEnumerable<string> apiResourceNames)
        {
            return FindApiResourcesByNameAsync(apiResourceNames, default);
        }

        public async Task<IEnumerable<Model.ApiResource>> FindApiResourcesByNameAsync(IEnumerable<string> apiResourceNames, CancellationToken cancellationToken = default)
        {
            IEnumerable<string> apiResources = apiResourceNames.Where(w => !string.IsNullOrWhiteSpace(w)).Distinct();

            Task<Model.ApiResource>[] keyTasks = apiResources.Select(apiResourceName => FindApiResourceAsync(apiResourceName, cancellationToken)).ToArray();

            await Task.WhenAll(keyTasks).ConfigureAwait(false);

            var models = keyTasks.Select(t => t.Result);
            _logger.LogDebug($"Found {models.Count()} {nameof(Model.ApiResource)} scopes in blob storage");

            return models;
        }
    }
}
