# identityserver4-azurestorage
Uses Azure Blob and Table Storage services as an alternative to Entity Framework/SQL data access for IdentityServer4.

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
                .CreateResourceStorage(); //Can be removed after first run.

	    // Adds IdentityServer
            services.AddIdentityServer()
            .AddSigningCredential(credential)            
            .AddResourceStore<ResourceStore>()
            .AddClientStore<ClientStore>()
            .AddCorsPolicyService<StorageCorsPolicyService>()
            .AddPersistedGrantStore<PersistedGrantStore>()
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
      "blobContainerName": "idsrv4clientconfig"
    },
    "resourceStorageConfig": {
      "storageConnectionString": "UseDevelopmentStorage=true;",
      "apiTableName": "idsrv4apiscopeindex",
      "apiBlobContainerName": "idsrv4apiresources",
      "identityBlobContainerName": "idsrv4identityresources"
    }
  }
}
```