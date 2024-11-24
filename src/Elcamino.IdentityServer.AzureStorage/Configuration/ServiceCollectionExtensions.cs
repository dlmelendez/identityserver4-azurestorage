// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Duende.IdentityServer.Stores;
using ElCamino.IdentityServer.AzureStorage.Configuration;
using ElCamino.IdentityServer.AzureStorage.Contexts;
using ElCamino.IdentityServer.AzureStorage.Hosted;
using ElCamino.IdentityServer.AzureStorage.Interfaces;
using ElCamino.IdentityServer.AzureStorage.Stores;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public const string IdentityServerAzureTableServiceClientKey = "ElCamino.IdentityServer.AzureStorage.TableServiceClientKey";
        public const string IdentityServerAzureBlobServiceClientKey = "ElCamino.IdentityServer.AzureStorage.BlobServiceClientKey";

        public static IServiceCollection AddIdentityServerTableServiceClient(this IServiceCollection services,
           Func<TableServiceClient> tableServiceClientAction)
        {
            services.AddKeyedSingleton<TableServiceClient>(IdentityServerAzureTableServiceClientKey, (_, _) => tableServiceClientAction());
            return services;
        }

        public static IServiceCollection AddTableServiceClient(this IServiceCollection services,
           Func<IServiceProvider, TableServiceClient> tableServiceClientAction)
        {
            services.AddKeyedSingleton<TableServiceClient>(IdentityServerAzureTableServiceClientKey, (sp, o) => tableServiceClientAction(sp));
            return services;
        }

        public static IServiceCollection AddIdentityServerBlobServiceClient(this IServiceCollection services,
           Func<BlobServiceClient> blobServiceClientAction)
        {
            services.AddKeyedSingleton<BlobServiceClient>(IdentityServerAzureBlobServiceClientKey, (_, _) => blobServiceClientAction());
            return services;
        }

        public static IServiceCollection AddBlobServiceClient(this IServiceCollection services,
           Func<IServiceProvider, BlobServiceClient> blobServiceClientAction)
        {
            services.AddKeyedSingleton<BlobServiceClient>(IdentityServerAzureBlobServiceClientKey, (sp, o) => blobServiceClientAction(sp));
            return services;
        }

        public static IServiceCollection AddPersistedGrantContext(this IServiceCollection services,
           IConfiguration persistedGrantStorageConfigSection)
        {
            services.Configure<PersistedGrantStorageConfig>(persistedGrantStorageConfigSection)
                .AddSingleton<PersistedGrantStorageContext>(sp => 
                { 
                    var blobClient = sp.GetRequiredKeyedService<BlobServiceClient>(IdentityServerAzureBlobServiceClientKey);
                    var tableClient = sp.GetRequiredKeyedService<TableServiceClient>(IdentityServerAzureTableServiceClientKey);
                    var config = sp.GetRequiredService<IOptions<PersistedGrantStorageConfig>>();
                    return new PersistedGrantStorageContext(config, tableClient, blobClient);
                })
                .AddTransient<PersistedGrantCleanup>()
                .AddSingleton<IHostedService, PersistedGrantCleanupHost>();
            //IdSrv4 adds the store,.AddScoped(persistedGrantStoreType, persistedGrantStoreType);
            return services;
        }

        public static IServiceCollection CreatePersistedGrantStorage(this IServiceCollection services)
        {
            var storageContext = services.BuildServiceProvider().GetRequiredService<PersistedGrantStorageContext>();
            storageContext.CreateStorageIfNotExists().Wait();
            return services;
        }

        public static IServiceCollection AddDeviceFlowContext(this IServiceCollection services,
          IConfiguration deviceFlowStorageConfigSection)
        {
            services.Configure<DeviceFlowStorageConfig>(deviceFlowStorageConfigSection)
                .AddSingleton<DeviceFlowStorageContext>(sp =>
                {
                    var blobClient = sp.GetRequiredKeyedService<BlobServiceClient>(IdentityServerAzureBlobServiceClientKey);
                    var config = sp.GetRequiredService<IOptions<DeviceFlowStorageConfig>>();
                    return new DeviceFlowStorageContext(config, blobClient);
                });
            //IdSrv4 adds the store,.AddScoped(deviceFlowStoreType, deviceFlowStoreType);
            return services;
        }

        public static IServiceCollection CreateDeviceFlowStorage(this IServiceCollection services)
        {
            var storageContext = services.BuildServiceProvider().GetRequiredService<DeviceFlowStorageContext>();
            storageContext.CreateStorageIfNotExists().Wait();
            return services;
        }

        public static IServiceCollection AddClientContext(this IServiceCollection services,
          IConfiguration clientStorageConfigSection)
        {
            //IdSrv4 adds the store, Type clientStoreType = typeof(ClientStore);

            services.Configure<ClientStorageConfig>(clientStorageConfigSection)
                .AddSingleton<ClientStorageContext>(sp =>
                {
                    var blobClient = sp.GetRequiredKeyedService<BlobServiceClient>(IdentityServerAzureBlobServiceClientKey);
                    var config = sp.GetRequiredService<IOptions<ClientStorageConfig>>();
                    return new ClientStorageContext(config, blobClient);
                })
                .AddSingleton<IClientStorageStore, ClientStore>()
                .AddSingleton<ClientCacheRefresh>()
                .AddSingleton<IHostedService, ClientCacheRefreshHost>();
            //IdSrv4 adds the store,.AddScoped(clientStoreType, clientStoreType);
            return services;
        }

        public static IServiceCollection CreateClientStorage(this IServiceCollection services)
        {
            var storageContext = services.BuildServiceProvider().GetRequiredService<ClientStorageContext>();
            storageContext.CreateStorageIfNotExists().Wait();
            return services;
        }

        public static IServiceCollection AddResourceContext(this IServiceCollection services,
          IConfiguration resourceStorageConfigSection)
        {
            var resourceStorageContextType = typeof(ResourceStorageContext);
            //IdSrv4 adds the store, Type resourceStoreType = typeof(ResourceStore);

            services.Configure<ResourceStorageConfig>(resourceStorageConfigSection)
                .AddSingleton<ResourceStorageContext>(sp =>
                {
                    var blobClient = sp.GetRequiredKeyedService<BlobServiceClient>(IdentityServerAzureBlobServiceClientKey);
                    var tableClient = sp.GetRequiredKeyedService<TableServiceClient>(IdentityServerAzureTableServiceClientKey);
                    var config = sp.GetRequiredService<IOptions<ResourceStorageConfig>>();
                    return new ResourceStorageContext(config,tableClient, blobClient);
                })
                .AddSingleton<ResourceCacheRefresh>()
                .AddSingleton<IHostedService, ResourceCacheRefreshHost>();
            //IdSrv4 adds the store,.AddScoped(resourceStoreType, resourceStoreType);
            return services;
        }

        public static IServiceCollection CreateResourceStorage(this IServiceCollection services)
        {
            var storageContext = services.BuildServiceProvider().GetRequiredService<ResourceStorageContext>();
            storageContext.CreateStorageIfNotExists().Wait();
            return services;
        }

        public static IServiceCollection AddSigningKeyContext(this IServiceCollection services,
          IConfiguration signingKeyStorageConfigSection)
        {
            var signingKeyStorageContextType = typeof(SigningKeyStorageContext);
            //IdSrv4 adds the store, Type signingKeyStoreType = typeof(SigningKeyStore);

            services.Configure<SigningKeyStorageConfig>(signingKeyStorageConfigSection)
                .AddSingleton<SigningKeyStorageContext>(sp =>
                {
                    var blobClient = sp.GetRequiredKeyedService<BlobServiceClient>(IdentityServerAzureBlobServiceClientKey);
                    var config = sp.GetRequiredService<IOptions<SigningKeyStorageConfig>>();
                    return new SigningKeyStorageContext(config, blobClient);
                });
            //IdSrv4 adds the store,.AddScoped(signingKeyStoreType, signingKeyStoreType);
            return services;
        }

        public static IServiceCollection CreateSigningKeyStorage(this IServiceCollection services)
        {
            var storageContext = services.BuildServiceProvider().GetRequiredService<SigningKeyStorageContext>();
            storageContext.CreateStorageIfNotExists().Wait();
            return services;
        }

        public static IServiceCollection MigrateResourceV3Storage(this IServiceCollection services)
        {
            ResourceStore resourceStore = services.BuildServiceProvider().GetRequiredService<IResourceStore>() as ResourceStore;
            resourceStore.MigrateV3ApiScopesAsync().Wait();
            return services;
        }


    }
}
