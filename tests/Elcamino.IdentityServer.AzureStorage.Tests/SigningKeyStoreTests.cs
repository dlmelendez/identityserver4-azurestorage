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
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Services.KeyManagement;

namespace ElCamino.IdentityServer.AzureStorage.UnitTests
{
    [TestClass]
    public class SigningKeyStoreTests : BaseTests
    {
        private ILogger<SigningKeyStore> _logger;

        private KeyContainer CreateKey(TimeSpan? age = null, string alg = "RS256", bool x509 = false)
        {
            var options = Services.BuildServiceProvider().GetService<IdentityServerOptions>();

            var key = CryptoHelper.CreateRsaSecurityKey(options.KeyManagement.RsaKeySize);

            var date = DateTime.UtcNow;
            if (age.HasValue) date = date.Subtract(age.Value);

            var container = x509 ?
                new X509KeyContainer(key, alg, date, (options.KeyManagement.RotationInterval + options.KeyManagement.RetentionDuration)) :
                (KeyContainer)new RsaKeyContainer(key, alg, date);

            return container;
        }

        private Model.SerializedKey CreateTestObject()
        {
            var keyContainer = CreateKey();
            var protector = Services.BuildServiceProvider().GetService<ISigningKeyProtector>();

            return protector.Protect(keyContainer);
        }

        [TestInitialize]
        public void Initialize()
        {
            var loggerFactory = Services.BuildServiceProvider().GetService<ILoggerFactory>();
            _logger = loggerFactory.CreateLogger<SigningKeyStore>();

        }

        [TestMethod]
        public void SigningKeyStore_CtorsTest()
        {
            var storageContext = Services.BuildServiceProvider().GetService<SigningKeyStorageContext>();
            Assert.IsNotNull(storageContext);


            var store = new SigningKeyStore(storageContext, _logger);
            Assert.IsNotNull(store);
        }

        [TestMethod]
        public async Task SigningKeyStore_SaveGetByClientIdTest()
        {
            Stopwatch stopwatch = new Stopwatch();

            var storageContext = Services.BuildServiceProvider().GetService<SigningKeyStorageContext>();
            Assert.IsNotNull(storageContext);

            var store = new SigningKeyStore(storageContext, _logger);
            Assert.IsNotNull(store);

            var serializedKey = CreateTestObject();
            Console.WriteLine($"KeyId: {serializedKey.Id}");

            stopwatch.Start();
            await store.StoreKeyAsync(serializedKey);
            stopwatch.Stop();
            Console.WriteLine($"SigningKeyStore.StoreAsync({serializedKey.Id}): {stopwatch.ElapsedMilliseconds} ms");

            stopwatch.Reset();
            stopwatch.Start();
            var clients = await store.LoadKeysAsync();
            int count = clients.Count();
            stopwatch.Stop();
            Console.WriteLine($"SigningKeyStore.LoadKeysAsync() Count: {count} : {stopwatch.ElapsedMilliseconds} ms");
            Assert.IsTrue(count > 0);

            stopwatch.Reset();
            stopwatch.Start();
            var getClient = (await store.LoadKeysAsync()).FirstOrDefault(f => f.Id == serializedKey.Id);
            stopwatch.Stop();
            Console.WriteLine($"SigningKeyStore.LoadKeysAsync(): {stopwatch.ElapsedMilliseconds} ms");
            Assert.IsNotNull(getClient);


        }

        [TestMethod]
        public async Task SigningKeyStore_RemoveByClientIdTest()
        {
            var storageContext = Services.BuildServiceProvider().GetService<SigningKeyStorageContext>();
            Assert.IsNotNull(storageContext);

            var store = new SigningKeyStore(storageContext, _logger);
            Assert.IsNotNull(store);

            var serializedKey = CreateTestObject();

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            await store.StoreKeyAsync(serializedKey);
            stopwatch.Stop();
            Console.WriteLine($"{nameof(SigningKeyStore)}.{nameof(SigningKeyStore.StoreKeyAsync)}({serializedKey.Id}): {stopwatch.ElapsedMilliseconds} ms");
            stopwatch.Reset();

            stopwatch.Start();
            await store.DeleteKeyAsync(serializedKey.Id);
            stopwatch.Stop();
            Console.WriteLine($"{nameof(SigningKeyStore)}.{nameof(SigningKeyStore.DeleteKeyAsync)}({serializedKey.Id}): {stopwatch.ElapsedMilliseconds} ms");

            stopwatch.Reset();
            stopwatch.Start();
            var getClient = (await store.LoadKeysAsync()).FirstOrDefault(f => f.Id == serializedKey.Id);
            stopwatch.Stop();
            Console.WriteLine($"{nameof(SigningKeyStore)}.{nameof(SigningKeyStore.LoadKeysAsync)}({serializedKey.Id}): {stopwatch.ElapsedMilliseconds} ms");
            Assert.IsNull(getClient);

        }


    }
}
