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

namespace ElCamino.IdentityServer.AzureStorage.UnitTests
{
    [TestClass]
    public class ClientStoreTests : BaseTests
    {
        private ILogger<ClientStore> _logger;
        public const string DefaultAllowedCors = "https://localhost:44328";

    public static Model.Client CreateTestObject(string clientid = null, 
            string allowedCors = DefaultAllowedCors)
        {
            return new Model.Client
            {

                ClientId = !string.IsNullOrWhiteSpace(clientid)? clientid :"samplejs",
                ClientName = "JavaScript Client",
                AccessTokenType = AccessTokenType.Reference,
                AccessTokenLifetime = 330,// 330 seconds, default 60 minutes
                IdentityTokenLifetime = 300,
                AllowedGrantTypes = GrantTypes.Implicit,
                AllowAccessTokensViaBrowser = true,
                RequireConsent = false,
                RedirectUris = { "https://localhost:44328/" },
                PostLogoutRedirectUris = {
                        "https://localhost:44328/unauthorized/",
                        "https://localhost:44328/"
                    },
                AllowedCorsOrigins = { allowedCors },
                AllowedScopes =
                    {
                        IdentityServerConstants.StandardScopes.OpenId,
                        IdentityServerConstants.StandardScopes.Profile,
                        IdentityServerConstants.StandardScopes.Email,
                        "Application",
                        "api1Scope"
                    }

            };
        }

        [TestInitialize]
        public void Initialize()
        {
            var loggerFactory = Services.BuildServiceProvider().GetService<ILoggerFactory>();
            _logger = loggerFactory.CreateLogger<ClientStore>();

        }

        [TestMethod]
        public void ClientStore_CtorsTest()
        {
            var storageContext = Services.BuildServiceProvider().GetService<ClientStorageContext>();
            Assert.IsNotNull(storageContext);


            var store = new ClientStore(storageContext, _logger);
            Assert.IsNotNull(store);
        }

        [TestMethod]
        public async Task ClientStore_SaveGetByClientIdTest()
        {
            Stopwatch stopwatch = new Stopwatch();

            var storageContext = Services.BuildServiceProvider().GetService<ClientStorageContext>();
            Assert.IsNotNull(storageContext);

            var store = new ClientStore(storageContext, _logger);
            Assert.IsNotNull(store);

            var client = CreateTestObject();
            Console.WriteLine(JsonSerializer.Serialize(client, storageContext.JsonSerializerDefaultOptions));

            stopwatch.Start();
            await store.StoreAsync(client);
            stopwatch.Stop();
            Console.WriteLine($"ClientStore.StoreAsync({client.ClientId}): {stopwatch.ElapsedMilliseconds} ms");

            stopwatch.Reset();
            stopwatch.Start();
            var findClient = await store.FindClientByIdAsync(client.ClientId);
            stopwatch.Stop();
            Console.WriteLine($"ClientStore.FindClientByIdAsync({client.ClientId}): {stopwatch.ElapsedMilliseconds} ms");
            Assert.AreEqual<string>(client.ClientId, findClient.ClientId);

            stopwatch.Reset();
            stopwatch.Start();
            var clients = await store.GetAllClients();
            int count = clients.Count();
            stopwatch.Stop();
            Console.WriteLine($"ClientStore.GetAllClients() Count: {count} : {stopwatch.ElapsedMilliseconds} ms");
            Assert.IsTrue(count > 0);

            stopwatch.Reset();
            stopwatch.Start();
            var getClient = (await store.GetAllClients()).FirstOrDefault(f => f.ClientId == client.ClientId);
            stopwatch.Stop();
            Console.WriteLine($"ClientStore.GetAllClients(): {stopwatch.ElapsedMilliseconds} ms");
            Assert.IsNotNull(getClient);


        }

        [TestMethod]
        public async Task ClientStore_RemoveByClientIdTest()
        {
            var storageContext = Services.BuildServiceProvider().GetService<ClientStorageContext>();
            Assert.IsNotNull(storageContext);

            var store = new ClientStore(storageContext, _logger);
            Assert.IsNotNull(store);

            var client = CreateTestObject();
            client.ClientId = Guid.NewGuid().ToString();
            Console.WriteLine(JsonSerializer.Serialize(client, storageContext.JsonSerializerDefaultOptions));

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            await store.StoreAsync(client);
            stopwatch.Stop();
            Console.WriteLine($"ClientStore.StoreAsync({client.ClientId}): {stopwatch.ElapsedMilliseconds} ms");
            stopwatch.Reset();

            stopwatch.Start();
            await store.RemoveAsync(client.ClientId);
            stopwatch.Stop();
            Console.WriteLine($"ClientStore.RemoveAsync({client.ClientId}): {stopwatch.ElapsedMilliseconds} ms");

            stopwatch.Reset();
            stopwatch.Start();
            var findClient = await store.FindClientByIdAsync(client.ClientId);
            stopwatch.Stop();
            Console.WriteLine($"ClientStore.FindClientByIdAsync({client.ClientId}): {stopwatch.ElapsedMilliseconds} ms");
            Assert.IsNull(findClient);

            stopwatch.Reset();
            stopwatch.Start();
            var getClient = (await store.GetAllClients()).FirstOrDefault(f => f.ClientId == client.ClientId);
            stopwatch.Stop();
            Console.WriteLine($"ClientStore.GetAllClients(): {stopwatch.ElapsedMilliseconds} ms");
            Assert.IsNull(getClient);

        }


    }
}
