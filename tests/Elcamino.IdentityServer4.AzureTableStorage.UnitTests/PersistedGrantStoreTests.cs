// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using ElCamino.IdentityServer4.AzureStorage.Contexts;
using ElCamino.IdentityServer4.AzureStorage.Mappers;
using ElCamino.IdentityServer4.AzureStorage.Stores;
using IdentityServer4.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Token = IdentityServer4.Models.Token;

namespace ElCamino.IdentityServer4.AzureStorage.UnitTests
{
    [TestClass]
    public class PersistedGrantStoreTests : BaseTests
    {
        private ILogger<PersistedGrantStore> _logger;

        private static PersistedGrant CreateTestObject(
            string key = null,
            string subjectId = null, 
            string clientId = null, 
            string type = null)
        {
            return new PersistedGrant
            {
                Key = key??Guid.NewGuid().ToString(),
                Type = type??"authorization_code",
                ClientId = clientId??Guid.NewGuid().ToString(),
                SubjectId = subjectId??Guid.NewGuid().ToString(),
                CreationTime = new DateTime(2016, 08, 01),
                Expiration = new DateTime(2016, 08, 31),
                Data = JsonConvert.SerializeObject(new Token())
            };
        }

        [TestInitialize]
        public void Initialize()
        {
            var loggerFactory = Services.BuildServiceProvider().GetService<ILoggerFactory>();
            _logger = loggerFactory.CreateLogger<PersistedGrantStore>();

        }

        [TestMethod]
        public void PersistedGrantStore_CtorsTest()
        {
            var storageContext = Services.BuildServiceProvider().GetService<PersistedGrantStorageContext>();
            Assert.IsNotNull(storageContext);


            var store = new PersistedGrantStore(storageContext, _logger);
            Assert.IsNotNull(store);
        }       

        [TestMethod]
        public async Task PersistedGrantStore_SaveGetByKeyTest()
        {
            var storageContext = Services.BuildServiceProvider().GetService<PersistedGrantStorageContext>();
            Assert.IsNotNull(storageContext);

            var store = new PersistedGrantStore(storageContext, _logger);
            Assert.IsNotNull(store);

            for (int iCounter = 0; iCounter < 10; iCounter++)
            {
                var grant = CreateTestObject();
                Console.WriteLine(JsonConvert.SerializeObject(grant));

                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                await store.StoreAsync(grant);
                stopwatch.Stop();
                Console.WriteLine($"PersistedGrantStore.StoreAsync({grant.Key}): {stopwatch.ElapsedMilliseconds} ms");

                stopwatch.Reset();
                //Failed Key:"78854990-6e1e-46ea-a5e0-5f26dd6c085a"
                //Failed Key:"1d7e16e2-daac-4ad4-8e7d-cffa59c8d79f"
                stopwatch.Start();
                var returnGrant = await store.GetAsync(grant.Key);
                stopwatch.Stop();
                Console.WriteLine($"PersistedGrantStore.GetAsync({grant.Key}): {stopwatch.ElapsedMilliseconds} ms");
                AssertGrantsEqual(grant, returnGrant);
            }

        }

        [TestMethod]
        public async Task PersistedGrantStore_ExpiredGetTest()
        {
            var storageContext = Services.BuildServiceProvider().GetService<PersistedGrantStorageContext>();
            Assert.IsNotNull(storageContext);

            var store = new PersistedGrantStore(storageContext, _logger);
            Assert.IsNotNull(store);

            var grant = CreateTestObject();
            grant.Expiration = DateTime.UtcNow.AddHours(-1);
            Console.WriteLine(JsonConvert.SerializeObject(grant));

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            await store.StoreAsync(grant);
            stopwatch.Stop();
            Console.WriteLine($"PersistedGrantStore.StoreAsync({grant.Key}): {stopwatch.ElapsedMilliseconds} ms");

            stopwatch.Reset();
            stopwatch.Start();
            var returnGrants = await store.StorageContext.GetExpiredAsync(1000);
            stopwatch.Stop();
            Console.WriteLine($"PersistedGrantStorageContext.GetExpiredAsync(): {stopwatch.ElapsedMilliseconds} ms");
            AssertGrantsEqual(grant, returnGrants.FirstOrDefault(f => f.Key == grant.Key).ToModel(), false);
            Assert.AreEqual<int>(2, returnGrants.Where(f => f.Key == grant.Key).Count());

        }

        [TestMethod]
        public async Task PersistedGrantStore_GetByKeyNegativeTest()
        {
            var storageContext = Services.BuildServiceProvider().GetService<PersistedGrantStorageContext>();
            Assert.IsNotNull(storageContext);

            var store = new PersistedGrantStore(storageContext, _logger);
            Assert.IsNotNull(store);

            string key = Guid.NewGuid().ToString();
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            var result = await store.GetAsync(key);
            stopwatch.Stop();
            Assert.IsNull(result);
            Console.WriteLine($"PersistedGrantStore.StoreAsync({key}): {stopwatch.ElapsedMilliseconds} ms");


        }

        [TestMethod]
        public async Task PersistedGrantStore_RemoveByKeyNegativeTest()
        {
            var storageContext = Services.BuildServiceProvider().GetService<PersistedGrantStorageContext>();
            Assert.IsNotNull(storageContext);

            var store = new PersistedGrantStore(storageContext, _logger);
            Assert.IsNotNull(store);

            string key = Guid.NewGuid().ToString();
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            await store.RemoveAsync(key);
            stopwatch.Stop();
            Console.WriteLine($"PersistedGrantStore.RemoveAsync({key}): {stopwatch.ElapsedMilliseconds} ms");


        }


        [TestMethod]
        public async Task PersistedGrantStore_RemoveByKeyTest()
        {
            var storageContext = Services.BuildServiceProvider().GetService<PersistedGrantStorageContext>();
            Assert.IsNotNull(storageContext);

            var store = new PersistedGrantStore(storageContext, _logger);
            Assert.IsNotNull(store);

            var grant = CreateTestObject();
            Console.WriteLine(JsonConvert.SerializeObject(grant));

            await store.StoreAsync(grant);

            //Failed Key:"78854990-6e1e-46ea-a5e0-5f26dd6c085a"
            //Failed Key:"1d7e16e2-daac-4ad4-8e7d-cffa59c8d79f"
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            var returnGrant = await store.GetAsync(grant.Key);
            stopwatch.Stop();
            Console.WriteLine($"PersistedGrantStore.GetAsync({grant.Key}): {stopwatch.ElapsedMilliseconds} ms");
            AssertGrantsEqual(grant, returnGrant);

            stopwatch.Reset();
            stopwatch.Start();
            await store.RemoveAsync(grant.Key);
            stopwatch.Stop();
            Console.WriteLine($"PersistedGrantStore.RemoveAsync({grant.Key}): {stopwatch.ElapsedMilliseconds} ms");
            Assert.IsNull((await store.GetAsync(grant.Key)));

        }

        [TestMethod]
        public async Task PersistedGrantStore_SaveGetBySubjectTest()
        {
            var storageContext = Services.BuildServiceProvider().GetService<PersistedGrantStorageContext>();
            Assert.IsNotNull(storageContext);

            var store = new PersistedGrantStore(storageContext, _logger);
            Assert.IsNotNull(store);

            string subject = Guid.NewGuid().ToString();
            List<PersistedGrant> grants = new List<PersistedGrant>();
            for (int iCounter = 0; iCounter < 10; iCounter++)
            {
                var grant = CreateTestObject(subjectId: subject);
                Console.WriteLine(JsonConvert.SerializeObject(grant));

                await store.StoreAsync(grant);
                grants.Add(grant);
            }
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            var returnGrants = (await store.GetAllAsync(subject)).ToList();
            stopwatch.Stop();
            Console.WriteLine($"PersistedGrantStore.GetAllAsync({subject}): {stopwatch.ElapsedMilliseconds} ms");
            Assert.AreEqual<int>(grants.Count, returnGrants.Count);
            grants.ForEach(g => AssertGrantsEqual(g, returnGrants.FirstOrDefault(f => f.Key == g.Key)));
        }

        [TestMethod]
        public async Task PersistedGrantStore_RemoveSubjectClientTest()
        {
            var storageContext = Services.BuildServiceProvider().GetService<PersistedGrantStorageContext>();
            Assert.IsNotNull(storageContext);

            var store = new PersistedGrantStore(storageContext, _logger);
            Assert.IsNotNull(store);

            string subject = Guid.NewGuid().ToString();
            string client = Guid.NewGuid().ToString();

            List<PersistedGrant> grants = new List<PersistedGrant>();
            for (int iCounter = 0; iCounter < 10; iCounter++)
            {
                var grant = CreateTestObject(subjectId: subject, clientId: client);
                Console.WriteLine(JsonConvert.SerializeObject(grant));

                await store.StoreAsync(grant);
                grants.Add(grant);
            }
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            var returnGrants = (await store.GetAllAsync(subject)).Where(w => w.ClientId == client).ToList();
            stopwatch.Stop();
            Console.WriteLine($"PersistedGrantStore.GetAllAsync({subject}): {stopwatch.ElapsedMilliseconds} ms");
            Assert.AreEqual<int>(grants.Count, returnGrants.Count);
            grants.ForEach(g => AssertGrantsEqual(g, returnGrants.FirstOrDefault(f => f.Key == g.Key)));

            stopwatch.Reset();
            stopwatch.Start();
            await store.RemoveAllAsync(subject, client);
            stopwatch.Stop();
            Console.WriteLine($"PersistedGrantStore.RemoveAllAsync({subject}, {client}): {stopwatch.ElapsedMilliseconds} ms");
            returnGrants = (await store.GetAllAsync(subject)).Where(w => w.ClientId == client).ToList();
            Assert.AreEqual<int>(0, returnGrants.Count);

        }

        [TestMethod]
        public async Task PersistedGrantStore_RemoveSubjectClientTypeTest()
        {
            var storageContext = Services.BuildServiceProvider().GetService<PersistedGrantStorageContext>();
            Assert.IsNotNull(storageContext);

            var store = new PersistedGrantStore(storageContext, _logger);
            Assert.IsNotNull(store);

            string subject = Guid.NewGuid().ToString();
            string client = Guid.NewGuid().ToString();
            string type = Guid.NewGuid().ToString();

            List<PersistedGrant> grants = new List<PersistedGrant>();
            for (int iCounter = 0; iCounter < 10; iCounter++)
            {
                var grant = CreateTestObject(subjectId: subject, clientId: client, type: type);
                Console.WriteLine(JsonConvert.SerializeObject(grant));

                await store.StoreAsync(grant);
                grants.Add(grant);
            }
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            var returnGrants = (await store.GetAllAsync(subject)).Where(w => w.ClientId == client && w.Type == type).ToList();
            stopwatch.Stop();
            Console.WriteLine($"PersistedGrantStore.GetAllAsync({subject}): {stopwatch.ElapsedMilliseconds} ms");
            Assert.AreEqual<int>(grants.Count, returnGrants.Count);
            grants.ForEach(g => AssertGrantsEqual(g, returnGrants.FirstOrDefault(f => f.Key == g.Key)));

            stopwatch.Reset();
            stopwatch.Start();
            await store.RemoveAllAsync(subject, client, type);
            stopwatch.Stop();
            Console.WriteLine($"PersistedGrantStore.RemoveAllAsync({subject}, {client}, {type}): {stopwatch.ElapsedMilliseconds} ms");
            returnGrants = (await store.GetAllAsync(subject)).Where(w => w.ClientId == client && w.Type == type).ToList();
            Assert.AreEqual<int>(0, returnGrants.Count);

        }

        public void AssertGrantsEqual(PersistedGrant expected, PersistedGrant actual, bool checkData = true)
        {
            Assert.IsNotNull(expected);
            Assert.IsNotNull(actual);

            Assert.AreEqual<string>(expected.Key, actual.Key);
            Assert.AreEqual<string>(expected.SubjectId, actual.SubjectId);
            Assert.AreEqual<string>(expected.ClientId, actual.ClientId);
            Assert.AreEqual<string>(expected.Type, actual.Type);
            if (checkData)
            {
                Assert.AreEqual<string>(expected.Data, actual.Data);
            }

        }
    }
}
