﻿// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.
// Based on work from Brock Allen & Dominick Baier, https://github.com/IdentityServer/IdentityServer4

using ElCamino.IdentityServer4.AzureStorage.Contexts;
using ElCamino.IdentityServer4.AzureStorage.Helpers;
using ElCamino.IdentityServer4.AzureStorage.Interfaces;
using ElCamino.IdentityServer4.AzureStorage.Mappers;
using IdentityServer4.Models;
using IdentityServer4.Stores;
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
    public class ClientStore : IClientStore, IClientStorageStore
    {
        public ClientStorageContext StorageContext { get; private set; }
        private readonly ILogger _logger;

        public ClientStore(ClientStorageContext storageContext, ILogger<ClientStore> logger)
        {
            StorageContext = storageContext;
            _logger = logger;
        }

        public async Task<IEnumerable<Client>> GetAllClients()
        {
            var entities = await GetAllClientEntities().ConfigureAwait(false);

            return entities.Select(s => s?.ToModel()).ToArray();
        }

        private async Task<IEnumerable<Entities.Client>> GetAllClientEntities()
        {
            var entities = await GetLatestClientCacheAsync().ConfigureAwait(false);
            if (entities == null)
            {
                entities = await StorageContext.GetAllBlobEntitiesAsync<Entities.Client>(StorageContext.ClientBlobContainer, _logger)
                    .ToListAsync()
                    .ConfigureAwait(false);
                await UpdateClientCacheFileAsync(entities).ConfigureAwait(false);
            }

            return entities;
        }

        public async Task<Client> FindClientByIdAsync(string clientId)
        {
            Model.Client model = null;
            Entities.Client entity = await StorageContext.GetEntityBlobAsync<Entities.Client>(clientId, StorageContext.ClientBlobContainer)
                .ConfigureAwait(false);
            model = entity?.ToModel();
            _logger.LogDebug("{clientName} found in blob storage: {clientFound}", clientId, model != null);

            return model;
        }

        public async Task StoreAsync(Client model)
        {
            var entity = model.ToEntity();
            try
            {
                await StorageContext.SaveBlobWithHashedKeyAsync(entity.ClientId, JsonConvert.SerializeObject(entity), StorageContext.ClientBlobContainer)
                    .ConfigureAwait(false);
                var entities = await GetAllClientEntities().ConfigureAwait(false);
                entities = entities.Where(e => entity.ClientId != e.ClientId).Concat(new Entities.Client[] { entity });
                await UpdateClientCacheFileAsync(entities).ConfigureAwait(false);
            }
            catch (AggregateException agg)
            {
                ExceptionHelper.LogStorageExceptions(agg, tableStorageLogger:null, (blobEx) =>
                {
                    _logger.LogWarning("exception updating {clientName} persisted grant in blob storage: {error}", model.ClientName, blobEx.Message);
                });
            }

        }      

        public async Task UpdateClientCacheFileAsync(IEnumerable<Entities.Client> entities)
        {
            (string blobName, int count) = await StorageContext.UpdateBlobCacheFileAsync<Entities.Client>(entities, StorageContext.ClientCacheBlobContainer)
                .ConfigureAwait(false);
            _logger.LogInformation($"{nameof(UpdateClientCacheFileAsync)} client count {count} saved in blob storage: {blobName}");
        }

        public async Task UpdateClientCacheFileAsync(IAsyncEnumerable<Entities.Client> entities)
        {
            (string blobName, int count) = await StorageContext.UpdateBlobCacheFileAsync<Entities.Client>(entities, StorageContext.ClientCacheBlobContainer)
                .ConfigureAwait(false);
            _logger.LogInformation($"{nameof(UpdateClientCacheFileAsync)} client count {count} saved in blob storage: {blobName}");
        }

        public async Task<IEnumerable<Entities.Client>> GetLatestClientCacheAsync()
        {
            try
            {
                return await StorageContext.GetLatestFromCacheBlobAsync<Entities.Client>(StorageContext.ClientCacheBlobContainer)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{nameof(GetLatestClientCacheAsync)} error");
            }
            return null;
        }

        public async Task RemoveAsync(string clientId)
        {
            try
            {
                await  StorageContext.DeleteBlobAsync(clientId, StorageContext.ClientBlobContainer)
                    .ConfigureAwait(false);
                var entities = await GetAllClientEntities().ConfigureAwait(false);
                entities = entities.Where(e => clientId != e.ClientId);
                await UpdateClientCacheFileAsync(entities).ConfigureAwait(false);
            }
            catch (AggregateException agg)
            {
                ExceptionHelper.LogStorageExceptions(agg, tableStorageLogger:null, (blobEx) =>
                {
                    _logger.LogWarning("exception updating {clientId} client in blob storage: {error}", clientId, blobEx.Message);
                });
            }
        }

        
       
        
    }
}
