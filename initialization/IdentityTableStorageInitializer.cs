using ElCamino.IdentityServer4.AzureStorage.Contexts;
using ElCamino.IdentityServer4.AzureStorage.Stores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace identitytablestorageinitializer
{
    /// <summary>
    /// Simply takes the data in the IdentityServer4 Config.cs file and persists it to our Azure Table Storage stores.
    /// 
    /// Prerequisite:  Make sure you have called services.AddPersistedGrantContext()... etc
    /// </summary>
    public class IdentityTableStorageInitializer
    {
        IServiceCollection Services;

        public IdentityTableStorageInitializer(IServiceCollection _services)
        {
            Services = _services;
        }

        public async Task OneTimeInitializeAsync()
        {
            await InitializeIdentityResources();
            await InitializeApiResources();
            await InitializeClients();
        }

        private async Task InitializeIdentityResources()
        {
            ResourceStorageContext ctx = Services.BuildServiceProvider().GetService<ResourceStorageContext>();
            if (ctx == null)
                throw new ArgumentException("ResourceStorageContext could not be found.  Have you previously called .AddResourceContext?");
            ILogger<ResourceStore> logger = Services.BuildServiceProvider().GetService<ILogger<ResourceStore>>();

            var store = new ResourceStore(ctx, logger);
            foreach(var id in Config.Ids)
            {
                await store.StoreAsync(id);
            }
        }

        private async Task InitializeApiResources()
        {
            ResourceStorageContext ctx = Services.BuildServiceProvider().GetService<ResourceStorageContext>();
            if (ctx == null)
                throw new ArgumentException("ResourceStorageContext could not be found.  Have you previously called .AddResourceContext?");
            ILogger<ResourceStore> logger = Services.BuildServiceProvider().GetService<ILogger<ResourceStore>>();

            var store = new ResourceStore(ctx, logger);
            foreach (var api in Config.Apis)
            {
                await store.StoreAsync(api);
            }
        }

        private async Task InitializeClients()
        {
            ILogger<ClientStore> logger = Services.BuildServiceProvider().GetService<ILogger<ClientStore>>();
            ClientStorageContext ctx = Services.BuildServiceProvider().GetService<ClientStorageContext>();
            if (ctx == null)
                throw new ArgumentException("ClientStorageContext could not be found.  Have you previously called .AddClientContext?");

            var store = new ClientStore(ctx, logger);
            foreach (var c in Config.Clients)
            {
                await store.StoreAsync(c);
            }
        }
    }
}
