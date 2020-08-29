# identityserver4-azurestorage
Uses Azure Blob and Table Storage services as an alternative to [Entity Framework/SQL data access for IdentityServer4](https://identityserver4.readthedocs.io/en/latest/quickstarts/5_entityframework.html).
Use the unit tests as a guide to seeding operational and configuration data.
- ElCamino.IdentityServer4.AzureStorage v1.x uses IdentityServer4 2.x & 3.x
- ElCamino.IdentityServer4.AzureStorage v2.x uses IdentityServer4 >= 4.x 

[![Build Status](https://dev.azure.com/elcamino/Azure%20OpenSource/_apis/build/status/dlmelendez.identityserver4-azurestorage?branchName=master)](https://dev.azure.com/elcamino/Azure%20OpenSource/_build/latest?definitionId=11&branchName=master)

[![NuGet Badge](https://buildstats.info/nuget/ElCamino.IdentityServer4.AzureStorage)](https://www.nuget.org/packages/ElCamino.IdentityServer4.AzureStorage/)

# IdentityServer4 v3 to v4
There are breaking changes when moving to IdentityServer4 v3 to v4. and respectively upgrading ElCamino.IdentityServer4.AzureStorage v1.x to v2.x.

## Config changes to support ApiScope blobs
New config settings, complete settings further down.
```json
{
  "IdentityServer4": {
   ...
    "resourceStorageConfig": {
      "apiScopeBlobContainerName": "idsrv4apiscopes",
      "apiScopeBlobCacheContainerName": "idsrv4apiscopescache",
    }...
  }
}
```

## Changes to startup.cs

Shown below in complete context, add .MigrateResourceV3Storage() into the startup services pipline. **Must be added __after__ services.AddIdentityServer().AddResourceStore() in the pipeline.** Remove after the first run.

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
                .CreateDeviceFlowStorage(); //Can be removed after first run.

	    // Adds IdentityServer
            services.AddIdentityServer()
            .AddSigningCredential(credential)            
            .AddResourceStore<ResourceStore>()
            .AddClientStore<ClientStore>()
            .AddCorsPolicyService<StorageCorsPolicyService>()
            .AddPersistedGrantStore<PersistedGrantStore>()
            .AddDeviceFlowStore<DeviceFlowStore>()
...            
            //Use for migrating ApiScopes from IdentityServer4 v3 ApiResources
            //Must be added after services.AddIdentityServer().AddResourceStore() in the pipeline
            //services.MigrateResourceV3Storage();

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
      "apiScopeBlobContainerName": "idsrv4apiscopes",
      "identityBlobContainerName": "idsrv4identityresources",
      "apiBlobCacheContainerName": "idsrv4apiresourcescache",
      "apiScopeBlobCacheContainerName": "idsrv4apiscopescache",
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
