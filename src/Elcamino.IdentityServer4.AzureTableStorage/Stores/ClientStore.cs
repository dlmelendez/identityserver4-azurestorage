// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.
// Based on work from Brock Allen & Dominick Baier, https://github.com/IdentityServer/IdentityServer4

using ElCamino.IdentityServer4.AzureStorage.Contexts;
using ElCamino.IdentityServer4.AzureStorage.Helpers;
using ElCamino.IdentityServer4.AzureStorage.Interfaces;
using ElCamino.IdentityServer4.AzureStorage.Mappers;
using IdentityServer4.Models;
using IdentityServer4.Stores;
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
            var entities = await StorageContext.GetAllBlobEntitiesAsync<Entities.Client>(StorageContext.ClientBlobContainer, _logger);
            
            return entities.Select(s => s?.ToModel()).ToArray();
        }

        public async Task<Client> FindClientByIdAsync(string clientId)
        {
            Model.Client model = null;
            Entities.Client entity = await StorageContext.GetEntityBlobAsync<Entities.Client>(clientId, StorageContext.ClientBlobContainer);
            model = entity?.ToModel();
            _logger.LogDebug("{clientName} found in blob storage: {clientFound}", clientId, model != null);

            return model;
        }

        public async Task StoreAsync(Client model)
        {
            var entity = model.ToEntity();
            try
            {
                await StorageContext.SaveBlobAsync(entity.ClientId, JsonConvert.SerializeObject(entity), StorageContext.ClientBlobContainer);
            }
            catch (AggregateException agg)
            {
                ExceptionHelper.LogStorageExceptions(agg, tableStorageLogger:null, (blobEx) =>
                {
                    _logger.LogWarning("exception updating {clientName} persisted grant in blob storage: {error}", model.ClientName, blobEx.Message);
                });
            }

        }

        public async Task RemoveAsync(string clientId)
        {
            try
            {
                await  StorageContext.DeleteBlobAsync(clientId, StorageContext.ClientBlobContainer);
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
