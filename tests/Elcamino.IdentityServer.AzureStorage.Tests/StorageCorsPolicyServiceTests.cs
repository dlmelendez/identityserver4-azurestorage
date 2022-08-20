// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using ElCamino.IdentityServer.AzureStorage.Contexts;
using ElCamino.IdentityServer.AzureStorage.Stores;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Model = Duende.IdentityServer.Models;
using System.Text.Json;
using Duende.IdentityServer.Services;
using ElCamino.IdentityServer.AzureStorage.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;

namespace ElCamino.IdentityServer.AzureStorage.UnitTests
{
    [TestClass]
    public class StorageCorsPolicyServiceTests : BaseTests
    {
        private ILogger<ClientStore> _logger;

        [TestInitialize]
        public void Initialize()
        {
            var loggerFactory = Services.BuildServiceProvider().GetService<ILoggerFactory>();
            _logger = loggerFactory.CreateLogger<ClientStore>();

        }

        [TestMethod]
        public async Task IsOriginAllowedTest()
        {
            Stopwatch stopwatch = new Stopwatch();

            var storageContext = Services.BuildServiceProvider().GetService<ClientStorageContext>();
            Assert.IsNotNull(storageContext);

            var store = new ClientStore(storageContext, _logger);
            Assert.IsNotNull(store);

            var client = ClientStoreTests.CreateTestObject(
                clientid: "00IsOriginAllowedTest", 
                allowedCors: ClientStoreTests.DefaultAllowedCors);

            stopwatch.Start();
            await store.StoreAsync(client);
            stopwatch.Stop();
            Console.WriteLine($"ClientStore.StoreAsync({client.ClientId}): {stopwatch.ElapsedMilliseconds} ms");
            var corsPolicyService = Services.BuildServiceProvider().GetService<ICorsPolicyService>();
            Assert.IsNotNull(corsPolicyService);

            stopwatch.Reset();
            stopwatch.Start();
            bool allowed = await corsPolicyService.IsOriginAllowedAsync(ClientStoreTests.DefaultAllowedCors).ConfigureAwait(false);
            stopwatch.Stop();
            Console.WriteLine($"StorageCorsPolicyService.IsOriginAllowedAsync({ClientStoreTests.DefaultAllowedCors}): output:{allowed} :{stopwatch.ElapsedMilliseconds} ms");

            Assert.IsTrue(allowed);
        }

    }
}
