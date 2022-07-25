// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ElCamino.Duende.IdentityServer.AzureStorage;
using ElCamino.Duende.IdentityServer.AzureStorage.Stores;
using Duende.IdentityServer.Stores.Serialization;

namespace ElCamino.Duende.IdentityServer.AzureStorage.UnitTests
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
                .AddKeyManagement();
            Services.AddLogging();

        }
    }
}
