# identityserver4-azurestorage
Uses Azure Blob and Table Storage services as an alternative to [Entity Framework/SQL data access for IdentityServer4](https://identityserver4.readthedocs.io/en/latest/quickstarts/5_entityframework.html) and [Entity Framework/SQL data access for Duende IdentityServer](https://docs.duendesoftware.com/identityserver/v6/quickstarts/4_ef/).
Use the unit tests as a guide to seeding operational and configuration data.

- ElCamino.IdentityServer.AzureStorage v6.x match major version of Duende IdentityServer 
- ElCamino.IdentityServer4.AzureStorage v2.x uses IdentityServer4 >= 4.x 
- ElCamino.IdentityServer4.AzureStorage v1.x uses IdentityServer4 2.x & 3.x

[![Build Status](https://dev.azure.com/elcamino/Azure%20OpenSource/_apis/build/status/dlmelendez.identityserver4-azurestorage?branchName=master)](https://dev.azure.com/elcamino/Azure%20OpenSource/_build/latest?definitionId=11&branchName=master)

[![NuGet Badge](https://buildstats.info/nuget/ElCamino.IdentityServer4.AzureStorage)](https://www.nuget.org/packages/ElCamino.IdentityServer4.AzureStorage/)

[![NuGet Badge](https://buildstats.info/nuget/ElCamino.IdentityServer.AzureStorage)](https://www.nuget.org/packages/ElCamino.IdentityServer.AzureStorage/)

# Duende IdentityServer v6
- Removed Newtonsoft.Json
- Updated to Azure.Data.Tables SDK
- Added support for SignedKeys Store

# IdentityServer4 v3 to v4
There are breaking changes when moving to IdentityServer4 v3 to v4. and respectively upgrading ElCamino.IdentityServer4.AzureStorage v1.x to v2.x.

## Config changes to support ApiScope blobs
New config settings, complete settings further down.
```json
{": {
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
using ElCamino.IdentityServer.AzureStorage.Stores;
using ElCamino.IdentityServer.AzureStorage.Services;
using Duende.IdentityServer;
...
        public void ConfigureServices(IServiceCollection services)
        {
            // Add framework services.
            services.AddMvc();
...
            //Add the Custom IdentityServer PersistentGrantStorageContext/Create Storage Table
            services.AddPersistedGrantContext(Configuration.GetSection("IdentityServer:persistedGrantStorageConfig"))
                .CreatePersistedGrantStorage() //Can be removed after first run.
                .AddClientContext(Configuration.GetSection("IdentityServer:clientStorageConfig"))
                .CreateClientStorage() //Can be removed after first run.
                .AddResourceContext(Configuration.GetSection("IdentityServer:resourceStorageConfig"))
                .CreateResourceStorage() //Can be removed after first run.
                .AddDeviceFlowContext(Configuration.GetSection("IdentityServer:deviceFlowStorageConfig"))
                .CreateDeviceFlowStorage() //Can be removed after first run.
                .AddSigningKeyContext(Configuration.GetSection("IdentityServer:signingKeyStorageConfig"))
                .CreateSigningKeyStorage(); //Can be removed after first run.

	    // Adds IdentityServer
            services.AddIdentityServer()
            .AddSigningCredential(credential)            
            .AddResourceStore<ResourceStore>()
            .AddClientStore<ClientStore>()
            .AddCorsPolicyService<StorageCorsPolicyService>()
            .AddPersistedGrantStore<PersistedGrantStore>()
            .AddDeviceFlowStore<DeviceFlowStore>()
            .AddSigningKeyStore<SigningKeyStore>()
...            
            //Use for migrating ApiScopes from IdentityServer4 v3 ApiResources
            //Must be added after services.AddIdentityServer().AddResourceStore() in the pipeline
            //services.MigrateResourceV3Storage();

...
```
## appsettings.json
```json
{
  "IdentityServer": {
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
      "blobContainerName": "idsrvclientconfig",
	  "blobCacheContainerName": "idsrvclientconfigcache",
	  "enableCacheRefresh": true,
	  "cacheRefreshInterval": 1800
    },
    "resourceStorageConfig": {
     "storageConnectionString": "UseDevelopmentStorage=true;",
      "apiTableName": "idsrvapiscopeindex",
      "apiBlobContainerName": "idsrvapiresources",
      "apiScopeBlobContainerName": "idsrvapiscopes",
      "identityBlobContainerName": "idsrvidentityresources",
      "apiBlobCacheContainerName": "idsrvapiresourcescache",
      "apiScopeBlobCacheContainerName": "idsrvapiscopescache",
      "identityBlobCacheContainerName": "idsrvidentityresourcescache",
	  "enableCacheRefresh": true,
	  "cacheRefreshInterval": 1800
    },
    "deviceFlowStorageConfig": {
      "storageConnectionString": "UseDevelopmentStorage=true;",
      "blobUserContainerName": "idsrvdeviceflowusercodes",
      "blobDeviceContainerName": "idsrvdeviceflowdevicecodes"
    },
    "signingKeyStorageConfig": {
      "storageConnectionString": "UseDevelopmentStorage=true;",
      "blobContainerName": "idsrvsigningkeys"
    }
  }
}
```
