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
                    var deleteIndexes = existingEntity?.Scopes?.Select(s => s.Name).Distinct().Select(i => GenerateResourceIndexEntity(entity.Name, i)).ToList();
                    await DeleteScopeIndexesAsync(deleteIndexes, StorageContext.ApiResourceTable);

                }
                // Add new scope indexes
                var newIndexes = entity?.Scopes?.Select(s => s.Name).Distinct().Select(i => GenerateResourceIndexEntity(entity.Name, i)).ToList();
                await CreateScopeIndexesAsync(newIndexes, StorageContext.ApiResourceTable);
                await StorageContext.SaveBlobAsync(entity.Name, JsonConvert.SerializeObject(entity), StorageContext.ApiResourceBlobContainer);
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
                await StorageContext.SaveBlobAsync(entity.Name, JsonConvert.SerializeObject(entity), StorageContext.IdentityResourceBlobContainer);
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
       

        private async Task DeleteScopeIndexesAsync(List<Entities.ResourceScopeIndexTblEntity> indexes, CloudTable table)
        {
            if (indexes != null && indexes.Count > 0)
            {
                List<Task> tasks = new List<Task>(indexes.Count);
                indexes.ForEach((index) => tasks.Add(StorageContext.GetAndDeleteTableEntityByKeys(index.PartitionKey, index.RowKey, table)));
                await Task.WhenAll(tasks);
            }
        }
        
        private async Task CreateScopeIndexesAsync(List<Entities.ResourceScopeIndexTblEntity> indexes, CloudTable table)
        {
            if (indexes != null && indexes.Count > 0)
            {
                List<Task> tasks = new List<Task>(indexes.Count);
                indexes.ForEach((index) => tasks.Add(table.ExecuteAsync(TableOperation.InsertOrReplace(index))));
                await Task.WhenAll(tasks);
            }
        }

        private async Task<List<Entities.ResourceScopeIndexTblEntity>> GetResourceScopeIndexTblEntitiesAsync(string scope, CloudTable table)
        {
            string partitionKeyFilter = TableQuery.GenerateFilterCondition("PartitionKey",
                QueryComparisons.Equal,
                KeyGeneratorHelper.GenerateHashValue(scope));

            TableQuery<Entities.ResourceScopeIndexTblEntity> tq = new TableQuery<Entities.ResourceScopeIndexTblEntity>();
            tq.FilterString = partitionKeyFilter;

            return (await StorageContext.GetAllByTableQueryAsync(tq, table))?.ToList();
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

            return entities.Select(s => s?.ToModel()).ToList();
        }

        private async Task<IEnumerable<Entity>> GetResourcesByScopeAsync<Entity>(IEnumerable<string> scopeNames, CloudTable table, CloudBlobContainer container) where Entity : class, new()
        {
            var names = scopeNames.Distinct().ToArray();
            List<Task<List<Entities.ResourceScopeIndexTblEntity>>> scopeTasks = new List<Task<List<Entities.ResourceScopeIndexTblEntity>>>(names.Length);
            scopeTasks.AddRange(names.Select(scope => GetResourceScopeIndexTblEntitiesAsync(scope, table)).ToArray());

            await Task.WhenAll(scopeTasks);

            string[] resourceNames = scopeTasks
                .Where(w => w != null && w.Result != null)
                .SelectMany(m => m.Result)
                .Where(w2 => !string.IsNullOrWhiteSpace(w2.ResourceName))
                .Select(s => s.ResourceName)
                .Distinct()
                .ToArray();

            List<Task<Entity>> keyTasks = new List<Task<Entity>>(resourceNames.Count());
            foreach (string resourceName in resourceNames)
            {
                keyTasks.Add(StorageContext.GetEntityBlobAsync<Entity>(resourceName, container));                
            }

            await Task.WhenAll(keyTasks);

            return keyTasks.Where(k => k?.Result != null).Select(s => s.Result).ToArray();

        }

        /// <summary>
        /// Gets identity resources by scope name.
        /// </summary>
        /// <param name="scopeNames"></param>
        /// <returns></returns>
        public async Task<IEnumerable<IdentityResource>> FindIdentityResourcesByScopeAsync(IEnumerable<string> scopeNames)
        {

            string[] scopes = scopeNames.Where(w => !string.IsNullOrWhiteSpace(w)).Distinct().ToArray();

            List<Task<Entities.IdentityResource>> keyTasks = new List<Task<Entities.IdentityResource>>(scopes.Length);

            foreach (string scope in scopes)
            {
                keyTasks.Add(StorageContext.GetEntityBlobAsync<Entities.IdentityResource>(scope, StorageContext.IdentityResourceBlobContainer));                
            }

            await Task.WhenAll(keyTasks);

            Entities.IdentityResource[] entities = keyTasks.Where(k => k?.Result != null).Select(s => s.Result).ToArray();

            _logger.LogDebug("Found {scopes} Identity scopes in blob storage", entities.Count());

            return entities.Select(s => s?.ToModel()).ToList();

        }

        /// <summary>
        /// Gets all resources.
        /// </summary>
        /// <returns></returns>
        public async Task<Resources> GetAllResourcesAsync()
        {
            var identityTask = StorageContext.GetAllBlobEntitiesAsync<Entities.IdentityResource>(StorageContext.IdentityResourceBlobContainer, _logger);

            var apisTask = StorageContext.GetAllBlobEntitiesAsync<Entities.ApiResource>(StorageContext.ApiResourceBlobContainer, _logger);

            await Task.WhenAll(identityTask, apisTask);

            var identity = identityTask.Result;
            var apis = apisTask.Result;

            var result = new Resources(
                identity.ToArray().Select(x => x.ToModel()).AsEnumerable(),
                apis.ToArray().Select(x => x.ToModel()).AsEnumerable());

            _logger.LogDebug("Found {scopes} as all scopes in blob storage", result.IdentityResources.Select(x=>x.Name).Union(result.ApiResources.SelectMany(x=>x.Scopes).Select(x=>x.Name)));

            return result;
        }
    }
}