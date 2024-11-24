// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using ElCamino.IdentityServer.AzureStorage.Contexts;
using ElCamino.IdentityServer.AzureStorage.Mappers;
using ElCamino.IdentityServer.AzureStorage.Stores;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Token = Duende.IdentityServer.Models.Token;
using System.Text.Json;

namespace ElCamino.IdentityServer.AzureStorage.UnitTests
{
    [TestClass]
    public class PersistedGrantStoreTests : BaseTests
    {
        private ILogger<PersistedGrantStore> _logger;

        private static PersistedGrant CreateTestObject(JsonSerializerOptions serializerOptions,
            string key = null,
            string subjectId = null, 
            string clientId = null, 
            string type = null,
            string session = null)
        {
            DateTime dateTime = DateTime.UtcNow;
            return new PersistedGrant
            {
                Key = key ?? Guid.NewGuid().ToString(),
                Type = type ?? "authorization_code",
                ClientId = clientId ?? Guid.NewGuid().ToString(),
                SubjectId = subjectId ?? Guid.NewGuid().ToString(),
                CreationTime = dateTime.AddDays(-31),
                Expiration = dateTime.AddDays(31),
                SessionId = session ?? Guid.NewGuid().ToString(),
                Data = JsonSerializer.Serialize(new Token(), serializerOptions),
                ConsumedTime = dateTime.AddMinutes(-6),
                Description = "Test Grant"
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
                var grant = CreateTestObject(serializerOptions: storageContext.JsonSerializerDefaultOptions);
                Console.WriteLine(JsonSerializer.Serialize(grant, storageContext.JsonSerializerDefaultOptions));

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

            var grant = CreateTestObject(serializerOptions: storageContext.JsonSerializerDefaultOptions);
            grant.Expiration = DateTime.UtcNow.AddHours(-1);
            Console.WriteLine(JsonSerializer.Serialize(grant, storageContext.JsonSerializerDefaultOptions));

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            await store.StoreAsync(grant);
            stopwatch.Stop();
            Console.WriteLine($"PersistedGrantStore.StoreAsync({grant.Key}): {stopwatch.ElapsedMilliseconds} ms");

            stopwatch.Reset();
            stopwatch.Start();
            var returnGrants = await store.StorageContext.GetExpiredAsync(1000, default);
            stopwatch.Stop();
            Console.WriteLine($"PersistedGrantStorageContext.GetExpiredAsync(): {stopwatch.ElapsedMilliseconds} ms");
            
            try
            {
                AssertGrantsEqual(grant, returnGrants.FirstOrDefault(f => f.Key == grant.Key).ToModel(), false);
                Assert.AreEqual<int>(2, returnGrants.Where(f => f.Key == grant.Key).Count());
            }
            finally
            {
                //Clean out expired grants
                foreach (var expiredGrant in returnGrants.Where(f => f.Key != grant.Key))
                {
                    await store.RemoveAsync(expiredGrant.Key);
                }
            }

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

            var grant = CreateTestObject(serializerOptions: storageContext.JsonSerializerDefaultOptions);
            Console.WriteLine(JsonSerializer.Serialize(grant, storageContext.JsonSerializerDefaultOptions));

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
                var grant = CreateTestObject(serializerOptions: storageContext.JsonSerializerDefaultOptions, subjectId: subject);
                Console.WriteLine(JsonSerializer.Serialize(grant, storageContext.JsonSerializerDefaultOptions));

                await store.StoreAsync(grant);
                grants.Add(grant);
            }
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            var returnGrants = (await store.GetAllAsync(new PersistedGrantFilter() { SubjectId = subject })).ToList();
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
                var grant = CreateTestObject(serializerOptions: storageContext.JsonSerializerDefaultOptions, subjectId: subject, clientId: client);
                Console.WriteLine(JsonSerializer.Serialize(grant, storageContext.JsonSerializerDefaultOptions));

                await store.StoreAsync(grant);
                grants.Add(grant);
            }
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            var returnGrants = (await store.GetAllAsync(new PersistedGrantFilter() { SubjectId = subject })).Where(w => w.ClientId == client).ToList();
            stopwatch.Stop();
            Console.WriteLine($"PersistedGrantStore.GetAllAsync({subject}): {stopwatch.ElapsedMilliseconds} ms");
            Assert.AreEqual<int>(grants.Count, returnGrants.Count);
            grants.ForEach(g => AssertGrantsEqual(g, returnGrants.FirstOrDefault(f => f.Key == g.Key)));

            stopwatch.Reset();
            stopwatch.Start();
            await store.RemoveAllAsync(new PersistedGrantFilter() { SubjectId = subject, ClientId = client });
            stopwatch.Stop();
            Console.WriteLine($"PersistedGrantStore.RemoveAllAsync({subject}, {client}): {stopwatch.ElapsedMilliseconds} ms");
            returnGrants = (await store.GetAllAsync(new PersistedGrantFilter() { SubjectId = subject })).Where(w => w.ClientId == client).ToList();
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
                var grant = CreateTestObject(serializerOptions: storageContext.JsonSerializerDefaultOptions, subjectId: subject, clientId: client, type: type);
                Console.WriteLine(JsonSerializer.Serialize(grant, storageContext.JsonSerializerDefaultOptions));

                await store.StoreAsync(grant);
                grants.Add(grant);
            }
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            var returnGrants = (await store.GetAllAsync(new PersistedGrantFilter() { SubjectId = subject, ClientId = client, Type= type })).ToList();
            stopwatch.Stop();
            Console.WriteLine($"PersistedGrantStore.GetAllAsync({subject}, {client}, {type}): {stopwatch.ElapsedMilliseconds} ms");
            Assert.AreEqual<int>(grants.Count, returnGrants.Count);
            grants.ForEach(g => AssertGrantsEqual(g, returnGrants.FirstOrDefault(f => f.Key == g.Key)));

            stopwatch.Reset();
            stopwatch.Start();
            await store.RemoveAllAsync(new PersistedGrantFilter() { SubjectId = subject, ClientId = client, Type = type});
            stopwatch.Stop();
            Console.WriteLine($"PersistedGrantStore.RemoveAllAsync({subject}, {client}, {type}): {stopwatch.ElapsedMilliseconds} ms");
            returnGrants = (await store.GetAllAsync(new PersistedGrantFilter() { SubjectId = subject, ClientId = client, Type = type })).ToList();
            Assert.AreEqual<int>(0, returnGrants.Count);

        }

        [TestMethod]
        public async Task PersistedGrantStore_RemoveSubjectClientTypeSessionTest()
        {
            var storageContext = Services.BuildServiceProvider().GetService<PersistedGrantStorageContext>();
            Assert.IsNotNull(storageContext);

            var store = new PersistedGrantStore(storageContext, _logger);
            Assert.IsNotNull(store);

            string subject = Guid.NewGuid().ToString();
            string client = Guid.NewGuid().ToString();
            string type = Guid.NewGuid().ToString();
            string session = Guid.NewGuid().ToString();

            List<PersistedGrant> grants = new List<PersistedGrant>();
            for (int iCounter = 0; iCounter < 10; iCounter++)
            {                
                var grant = CreateTestObject(serializerOptions: storageContext.JsonSerializerDefaultOptions, subjectId: subject, 
                    clientId: client, 
                    type: type,
                    session: (session + iCounter.ToString()));
                Console.WriteLine(JsonSerializer.Serialize(grant, storageContext.JsonSerializerDefaultOptions));

                await store.StoreAsync(grant);
                grants.Add(grant);
            }
            string sessionTarget = session + "0";
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            var returnGrants = (await store.GetAllAsync(new PersistedGrantFilter() { SubjectId = subject, ClientId = client, Type = type, SessionId = sessionTarget })).ToList();
            stopwatch.Stop();
            Console.WriteLine($"PersistedGrantStore.GetAllAsync({subject}, {client}, {type}, {sessionTarget}): {stopwatch.ElapsedMilliseconds} ms");
            Assert.AreEqual<int>(1, returnGrants.Count);
            returnGrants.ForEach(g => AssertGrantsEqual(g, grants.FirstOrDefault(f => f.Key == g.Key)));

            stopwatch.Reset();
            stopwatch.Start();
            await store.RemoveAllAsync(new PersistedGrantFilter() { SubjectId = subject, ClientId = client, Type = type, SessionId = sessionTarget });
            stopwatch.Stop();
            Console.WriteLine($"PersistedGrantStore.RemoveAllAsync({subject}, {client}, {type}, {sessionTarget}): {stopwatch.ElapsedMilliseconds} ms");
            returnGrants = (await store.GetAllAsync(new PersistedGrantFilter() { SubjectId = subject, ClientId = client, Type = type })).ToList();
            Assert.AreEqual<int>(grants.Count - 1, returnGrants.Count);

        }

        public static void AssertGrantsEqual(PersistedGrant expected, PersistedGrant actual, bool checkData = true)
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
