# identityserver4-azurestorage
Uses Azure Blob and Table Storage services as an alternative to [Entity Framework/SQL data access for IdentityServer4](https://identityserver4.readthedocs.io/en/latest/quickstarts/7_entity_framework.html#identityserver4-entityframework).
See [initialization](initialization/) as a guide to seeding operational and configuration data.

[![Build Status](https://dev.azure.com/elcamino/Azure%20OpenSource/_apis/build/status/dlmelendez.identityserver4-azurestorage?branchName=master)](https://dev.azure.com/elcamino/Azure%20OpenSource/_build/latest?definitionId=11&branchName=master)

[![NuGet Badge](https://buildstats.info/nuget/ElCamino.IdentityServer4.AzureStorage)](https://www.nuget.org/packages/ElCamino.IdentityServer4.AzureStorage/)

# Getting Started
## startup.cs
```C#
using ElCamino.IdentityServer4.AzureStorage.Stores;
using ElCamino.IdentityServer4.AzureStorage.Services;
using IdentityServer4;
...
        public void ConfigureServices(IServiceCollection services)
        {
            // Add framework services.
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
...
            //Add the Custom IdentityServer PersistentGrantStorageContext/Create Storage Table
            services.AddPersistedGrantContext(Configuration.GetSection("IdentityServer4:persistedGrantStorageConfig"))
                .CreatePersistedGrantStorage() //Can be removed after first run.
                .AddClientContext(Configuration.GetSection("IdentityServer4:clientStorageConfig"))
                .CreateClientStorage() //Can be removed after first run.
                .AddResourceContext(Configuration.GetSection("IdentityServer4:resourceStorageConfig"))
                .CreateResourceStorage() //Can be removed after first run.
                .AddDeviceFlowContext(Configuration.GetSection("IdentityServer4:deviceFlowStorageConfig"))
                .CreateDeviceFlowStorage() //Can be removed after first run.

	    // Adds IdentityServer
            services.AddIdentityServer()
            .AddSigningCredential(credential)            
            .AddResourceStore<ResourceStore>()
            .AddClientStore<ClientStore>()
            .AddCorsPolicyService<StorageCorsPolicyService>()
            .AddPersistedGrantStore<PersistedGrantStore>()
            .AddDeviceFlowStore<DeviceFlowStore>()
...
```
## appsettings.json
```json
{
  "IdentityServer4": {
    "persistedGrantStorageConfig": {
      "storageConnectionString": "UseDevelopmentStorage=true;",
      "blobContainerName": "idsrv4persistedgrants",
      "persistedGrantTableName": "idsrv4persistedgrant",
      "enableTokenCleanup": true,
      "tokenCleanupInterval": 3600,
      "tokenCleanupBatchSize": 100
    },
    "clientStorageConfig": {
      "storageConnectionString": "UseDevelopmentStorage=true;",
      "blobContainerName": "idsrv4clientconfig",
	  "blobCacheContainerName": "idsrv4clientconfigcache",
	  "enableCacheRefresh": true,
	  "cacheRefreshInterval": 1800
    },
    "resourceStorageConfig": {
      "storageConnectionString": "UseDevelopmentStorage=true;",
      "apiTableName": "idsrv4apiscopeindex",
      "apiBlobContainerName": "idsrv4apiresources",
      "identityBlobContainerName": "idsrv4identityresources",
	  "apiBlobCacheContainerName": "idsrv4apiresourcescache",
      "identityBlobCacheContainerName": "idsrv4identityresourcescache",
	  "enableCacheRefresh": true,
	  "cacheRefreshInterval": 1800
    },
    "deviceFlowStorageConfig": {
      "storageConnectionString": "UseDevelopmentStorage=true;",
      "blobUserContainerName": "deviceflowusercodes",
      "blobDeviceContainerName": "deviceflowdevicecodes"
    }
  }
}
```
