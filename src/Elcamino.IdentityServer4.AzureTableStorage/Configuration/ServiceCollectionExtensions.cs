﻿// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using ElCamino.Duende.IdentityServer.AzureStorage.Configuration;
using ElCamino.Duende.IdentityServer.AzureStorage.Contexts;
using ElCamino.Duende.IdentityServer.AzureStorage.Hosted;
using ElCamino.Duende.IdentityServer.AzureStorage.Interfaces;
using ElCamino.Duende.IdentityServer.AzureStorage.Stores;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddPersistedGrantContext(this IServiceCollection services,
           IConfiguration persistedGrantStorageConfigSection)
        {
            var persistedGrantStorageContextType = typeof(PersistedGrantStorageContext);

            services.Configure<PersistedGrantStorageConfig>(persistedGrantStorageConfigSection)
                .AddScoped(persistedGrantStorageContextType, persistedGrantStorageContextType)
                .AddTransient<PersistedGrantCleanup>()
                .AddSingleton<IHostedService, PersistedGrantCleanupHost>();
            //IdSrv4 adds the store,.AddScoped(persistedGrantStoreType, persistedGrantStoreType);
            return services;
        }

        public static IServiceCollection CreatePersistedGrantStorage(this IServiceCollection services)
        {
            var storageContext = services.BuildServiceProvider().GetService<PersistedGrantStorageContext>();
            storageContext.CreateStorageIfNotExists().Wait();
            return services;
        }

        public static IServiceCollection AddDeviceFlowContext(this IServiceCollection services,
          IConfiguration deviceFlowStorageConfigSection)
        {
            var deviceFlowStorageContextType = typeof(DeviceFlowStorageContext);

            services.Configure<DeviceFlowStorageConfig>(deviceFlowStorageConfigSection)
                .AddScoped(deviceFlowStorageContextType, deviceFlowStorageContextType); ;
            //IdSrv4 adds the store,.AddScoped(deviceFlowStoreType, deviceFlowStoreType);
            return services;
        }

        public static IServiceCollection CreateDeviceFlowStorage(this IServiceCollection services)
        {
            var storageContext = services.BuildServiceProvider().GetService<DeviceFlowStorageContext>();
            storageContext.CreateStorageIfNotExists().Wait();
            return services;
        }

        public static IServiceCollection AddClientContext(this IServiceCollection services,
          IConfiguration clientStorageConfigSection)
        {
            var clientStorageContextType = typeof(ClientStorageContext);
            //IdSrv4 adds the store, Type clientStoreType = typeof(ClientStore);

            services.Configure<ClientStorageConfig>(clientStorageConfigSection)
                .AddScoped(clientStorageContextType, clientStorageContextType)
                .AddTransient<IClientStorageStore, ClientStore>()
                .AddTransient<ClientCacheRefresh>()
                .AddSingleton<IHostedService, ClientCacheRefreshHost>();
            //IdSrv4 adds the store,.AddScoped(clientStoreType, clientStoreType);
            return services;
        }

        public static IServiceCollection CreateClientStorage(this IServiceCollection services)
        {
            var storageContext = services.BuildServiceProvider().GetService<ClientStorageContext>();
            storageContext.CreateStorageIfNotExists().Wait();
            return services;
        }

        public static IServiceCollection AddResourceContext(this IServiceCollection services,
          IConfiguration resourceStorageConfigSection)
        {
            var resourceStorageContextType = typeof(ResourceStorageContext);
            //IdSrv4 adds the store, Type resourceStoreType = typeof(ResourceStore);

            services.Configure<ResourceStorageConfig>(resourceStorageConfigSection)
                .AddScoped(resourceStorageContextType, resourceStorageContextType)
                .AddTransient<ResourceCacheRefresh>()
                .AddSingleton<IHostedService, ResourceCacheRefreshHost>();
            //IdSrv4 adds the store,.AddScoped(resourceStoreType, resourceStoreType);
            return services;
        }

        public static IServiceCollection CreateResourceStorage(this IServiceCollection services)
        {
            var storageContext = services.BuildServiceProvider().GetService<ResourceStorageContext>();
            storageContext.CreateStorageIfNotExists().Wait();
            return services;
        }

        public static IServiceCollection AddSigningKeyContext(this IServiceCollection services,
          IConfiguration signingKeyStorageConfigSection)
        {
            var signingKeyStorageContextType = typeof(SigningKeyStorageContext);
            //IdSrv4 adds the store, Type signingKeyStoreType = typeof(SigningKeyStore);

            services.Configure<SigningKeyStorageConfig>(signingKeyStorageConfigSection)
                .AddScoped(signingKeyStorageContextType, signingKeyStorageContextType);
            //IdSrv4 adds the store,.AddScoped(signingKeyStoreType, signingKeyStoreType);
            return services;
        }

        public static IServiceCollection CreateSigningKeyStorage(this IServiceCollection services)
        {
            var storageContext = services.BuildServiceProvider().GetService<SigningKeyStorageContext>();
            storageContext.CreateStorageIfNotExists().Wait();
            return services;
        }

        public static IServiceCollection MigrateResourceV3Storage(this IServiceCollection services)
        {
            ResourceStore resourceStore = services.BuildServiceProvider().GetService<IResourceStore>() as ResourceStore;
            resourceStore.MigrateV3ApiScopesAsync().Wait();
            return services;
        }


    }
}
