// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using ElCamino.IdentityServer.AzureStorage.Contexts;
using ElCamino.IdentityServer.AzureStorage.Hosted;
using ElCamino.IdentityServer.AzureStorage.Stores;
using Duende.IdentityServer.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Token = Duende.IdentityServer.Models.Token;

namespace ElCamino.IdentityServer.AzureStorage.UnitTests
{
    [TestClass]
    public class PersistedGrantCleanupTests : BaseTests
    {
        private ILogger<PersistedGrantStore> _logger;
        private IServiceProvider _serviceProvider;

        [TestInitialize]
        public void Initialize()
        {
            _serviceProvider = Services.BuildServiceProvider();
            var loggerFactory = _serviceProvider.GetService<ILoggerFactory>();
            _logger = loggerFactory.CreateLogger<PersistedGrantStore>();
        }

        private static PersistedGrant CreateTestGrant(JsonSerializerOptions serializerOptions, DateTime expiration, string key = null)
        {
            DateTime dateTime = DateTime.UtcNow;
            return new PersistedGrant
            {
                Key = key ?? Guid.NewGuid().ToString(),
                Type = "refresh_token",
                ClientId = Guid.NewGuid().ToString(),
                SubjectId = Guid.NewGuid().ToString(),
                SessionId = Guid.NewGuid().ToString(),
                CreationTime = dateTime.AddHours(-1),
                Expiration = expiration,
                Data = JsonSerializer.Serialize(new Token(), serializerOptions),
                Description = "Cleanup race test grant"
            };
        }

        [TestMethod]
        public async Task PersistedGrantCleanup_RenewedGrantSurvivesConcurrentCleanupTest()
        {
            var storageContext = _serviceProvider.GetService<PersistedGrantStorageContext>();
            var store = new PersistedGrantStore(storageContext, _logger);
            var cleanup = _serviceProvider.GetService<PersistedGrantCleanup>();
            Assert.IsNotNull(cleanup);

            const int iterations = 200;
            var lostDetails = new List<string>();

            for (int i = 0; i < iterations; i++)
            {
                var grant = CreateTestGrant(storageContext.JsonSerializerDefaultOptions, DateTime.UtcNow.AddSeconds(-1));
                await store.StoreAsync(grant);

                var renewed = CreateTestGrant(storageContext.JsonSerializerDefaultOptions, DateTime.UtcNow.AddHours(1), grant.Key);
                renewed.ClientId = grant.ClientId;
                renewed.SubjectId = grant.SubjectId;
                renewed.SessionId = grant.SessionId;

                await Task.WhenAll(
                    store.StoreAsync(renewed),
                    cleanup.RemoveExpiredGrantsAsync());

                var after = await store.GetAsync(grant.Key);
                if (after == null)
                {
                    lostDetails.Add($"iteration {i}: renewed grant deleted");
                }
                else if (string.IsNullOrEmpty(after.Data))
                {
                    lostDetails.Add($"iteration {i}: renewed grant row survived but token data blob was deleted");
                }
                else
                {
                    await store.RemoveAsync(grant.Key);
                }
            }

            Assert.AreEqual(0, lostDetails.Count,
                $"a concurrently-renewed grant was destroyed by cleanup in {lostDetails.Count} of {iterations} iterations: {string.Join("; ", lostDetails)}");
        }

        [TestMethod]
        public async Task PersistedGrantCleanup_MultiInstanceRemovesExpiredExactlyTest()
        {
            var storageContext = _serviceProvider.GetService<PersistedGrantStorageContext>();
            var store = new PersistedGrantStore(storageContext, _logger);
            var cleanupA = _serviceProvider.GetService<PersistedGrantCleanup>();
            var cleanupB = _serviceProvider.GetService<PersistedGrantCleanup>();
            Assert.AreNotSame(cleanupA, cleanupB);

            var expired = Enumerable.Range(0, 15)
                .Select(_ => CreateTestGrant(storageContext.JsonSerializerDefaultOptions, DateTime.UtcNow.AddMinutes(-5)))
                .ToList();
            var active = Enumerable.Range(0, 10)
                .Select(_ => CreateTestGrant(storageContext.JsonSerializerDefaultOptions, DateTime.UtcNow.AddHours(1)))
                .ToList();

            foreach (var grant in expired.Concat(active))
            {
                await store.StoreAsync(grant);
            }

            await Task.WhenAll(
                cleanupA.RemoveExpiredGrantsAsync(),
                cleanupB.RemoveExpiredGrantsAsync());

            foreach (var grant in expired)
            {
                var after = await store.GetAsync(grant.Key);
                Assert.IsNull(after, $"expired grant {grant.Key} was not removed");
            }

            foreach (var grant in active)
            {
                var after = await store.GetAsync(grant.Key);
                Assert.IsNotNull(after, $"active grant {grant.Key} was removed by concurrent cleanup");
                Assert.IsFalse(string.IsNullOrEmpty(after.Data), $"active grant {grant.Key} lost its token data blob");
                await store.RemoveAsync(grant.Key);
            }
        }

        [TestMethod]
        public async Task PersistedGrantStore_RowWithoutBlobTreatedAsMissingTest()
        {
            var storageContext = _serviceProvider.GetService<PersistedGrantStorageContext>();
            var store = new PersistedGrantStore(storageContext, _logger);

            var grant = CreateTestGrant(storageContext.JsonSerializerDefaultOptions, DateTime.UtcNow.AddHours(1));
            await store.StoreAsync(grant);

            await storageContext.DeleteBlobAsync(grant.Key, storageContext.PersistedGrantBlobContainer);

            var after = await store.GetAsync(grant.Key);

            // A grant whose token data is gone is unusable; surfacing it with empty Data makes
            // Duende fail deserialization silently and invalidate the token without a trace.
            Assert.IsNull(after,
                $"grant with missing data blob must be treated as missing, got Data='{after?.Data}'");
        }
    }
}
