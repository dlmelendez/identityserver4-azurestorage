// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Duende.IdentityServer.Stores.Serialization;
using ElCamino.IdentityServer.AzureStorage.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ElCamino.IdentityServer.AzureStorage.UnitTests
{
    public class BaseTests
    {
        public ServiceCollection Services { get; private set; }

        public IConfigurationRoot Configuration { get; private set; }


        protected BaseTests()
        {
            var builder = new ConfigurationBuilder()
           .AddJsonFile("appsettings.json");

            Configuration = builder.Build();

            Services = new ServiceCollection();
            Services.AddIdentityServerTableServiceClient(() => new TableServiceClient(Configuration.GetSection("IdentityServer:storageConnectionString").Value));
            Services.AddIdentityServerBlobServiceClient(() => new BlobServiceClient(Configuration.GetSection("IdentityServer:storageConnectionString").Value));
            Services.AddPersistedGrantContext(Configuration.GetSection("IdentityServer:persistedGrantStorageConfig"))
                .CreatePersistedGrantStorage();
            Services.AddClientContext(Configuration.GetSection("IdentityServer:clientStorageConfig"))
               .CreateClientStorage();
            Services.AddResourceContext(Configuration.GetSection("IdentityServer:resourceStorageConfig"))
               .CreateResourceStorage();
            Services.AddTransient<IPersistentGrantSerializer>((f) =>  new PersistentGrantSerializer());
            Services.AddDeviceFlowContext(Configuration.GetSection("IdentityServer:deviceFlowStorageConfig"))
               .CreateDeviceFlowStorage();
            Services.AddSigningKeyContext(Configuration.GetSection("IdentityServer:signingKeyStorageConfig"))
               .CreateSigningKeyStorage();
            Services.AddIdentityServer()
                .AddKeyManagement()
                .AddCorsPolicyService<StorageCorsPolicyService>();
            Services.AddLogging();

        }
    }
}
