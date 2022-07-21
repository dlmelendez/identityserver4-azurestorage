// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using ElCamino.Duende.IdentityServer.AzureStorage.Contexts;
using ElCamino.Duende.IdentityServer.AzureStorage.Stores;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Model = Duende.IdentityServer.Models;

namespace ElCamino.Duende.IdentityServer.AzureStorage.UnitTests
{
    [TestClass]
    public class ResourceStoreTests : BaseTests
    {
        private ILogger<ResourceStore> _logger;

        private static Model.ApiResource CreateApiTestObject(string name = null)
        {
            return new Model.ApiResource
            {
                Name = !string.IsNullOrWhiteSpace(name) ? name : "api1",
                Description = "My API",
                Scopes = new List<string>
                    {
                       "api1Scope"
                    },
                UserClaims = GetAllAvailableClaimTypes().ToList(),
                ApiSecrets =
                    {
                        new Secret("YouShouldNotUseThisKeyInProduction".Sha256())
                    },
            };
        }

        private static Entities.ApiResourceV3 CreateApiV3EntityTestObject(string apiResourceName, string scopeName)
        {
            return new Entities.ApiResourceV3
            {
                Name = apiResourceName,
                Description = "My API",
                Scopes = new List<Entities.ApiScope>
                    {
                       new Entities.ApiScope
                        {
                            Name = scopeName,
                            DisplayName = "Scope for the dataEventRecords ApiResource",
                            UserClaims = GetAllAvailableClaimTypes().Select(s => new Entities.ApiScopeClaim() { Type = s }).ToList()
                        }
                    },
                UserClaims = GetAllAvailableClaimTypes().Select(s => new Entities.ApiResourceClaim() { Type = s }).ToList(),
                Secrets = new List<Entities.ApiResourceSecret>()
                    {
                        new Entities.ApiResourceSecret() { Value= "SuperSecretAPiKey1".Sha256(), Type = "SharedSecret" }
                    },
            };
        }


        public static IEnumerable<IdentityResource> GetIdentityResources()
        {
            return new List<IdentityResource>
            {
                new IdentityResources.OpenId(),
                new IdentityResources.Profile(),
                new IdentityResources.Email(),
                new IdentityResource("Application", "Application Claims", GetAllAvailableClaimTypes()),
            };
        }

        public static string[] GetAllAvailableClaimTypes()
        {
            return new string[] {
                 "Owner".ToLower(),
                "Moderator".ToLower(),
                "Contributor".ToLower(),
                "Reader".ToLower(),
                "Admin".ToLower()
            };
        }


        [TestInitialize]
        public void Initialize()
        {
            var loggerFactory = Services.BuildServiceProvider().GetService<ILoggerFactory>();
            _logger = loggerFactory.CreateLogger<ResourceStore>();

        }

        [TestMethod]
        public void ResourceStore_CtorsTest()
        {
            var storageContext = Services.BuildServiceProvider().GetService<ResourceStorageContext>();
            Assert.IsNotNull(storageContext);


            var store = new ResourceStore(storageContext, _logger);
            Assert.IsNotNull(store);
        }

        [TestMethod]
        public async Task ResourceStore_MigrateV3Test()
        {
            Stopwatch stopwatch = new Stopwatch();
            Services.AddScoped<IResourceStore, ResourceStore>();
            var storageContext = Services.BuildServiceProvider().GetService<ResourceStorageContext>();
            Assert.IsNotNull(storageContext);

            var store = new ResourceStore(storageContext, _logger);
            Assert.IsNotNull(store);

            string apiResourceName = "aipResourceName-" + Guid.NewGuid().ToString();
            Console.WriteLine($"api resource name: {apiResourceName}");
            string scopeName = "scopeName-" + Guid.NewGuid().ToString();
            Console.WriteLine($"api scope name: {scopeName}");

            var resource = CreateApiV3EntityTestObject(apiResourceName, scopeName);
            await storageContext.SaveBlobWithHashedKeyAsync(apiResourceName, JsonConvert.SerializeObject(resource),
                storageContext.ApiResourceBlobContainer);
            
            // Run Migration
            stopwatch.Start();
            Services.MigrateResourceV3Storage();
            stopwatch.Stop();
            Console.WriteLine($"MigrateResourceV3Storage-api: {stopwatch.ElapsedMilliseconds} ms");

            stopwatch.Reset();
            stopwatch.Start();
            string[] findScopes = new string[] { scopeName, Guid.NewGuid().ToString() };
            var findApiScopes = await store.FindApiScopesByNameAsync(findScopes);
            stopwatch.Stop();
            Console.WriteLine($"ResourceStore.FindApiScopesByNameAsync({string.Join(",", findScopes)})-api: {stopwatch.ElapsedMilliseconds} ms");
            Assert.AreEqual<string>(scopeName, findApiScopes.Single()?.Name);
            Assert.IsTrue(findApiScopes.Single().UserClaims.AsEnumerable().Any(a => a == resource.Scopes.First().UserClaims.First().Type));
        }

        [TestMethod]
        public async Task ResourceStore_Api_SaveGetTest()
        {
            Stopwatch stopwatch = new Stopwatch();

            var storageContext = Services.BuildServiceProvider().GetService<ResourceStorageContext>();
            Assert.IsNotNull(storageContext);

            var store = new ResourceStore(storageContext, _logger);
            Assert.IsNotNull(store);

            var resource = CreateApiTestObject();
            Console.WriteLine(JsonConvert.SerializeObject(resource));

            stopwatch.Start();
            await store.StoreAsync(resource);
            stopwatch.Stop();
            Console.WriteLine($"ResourceStore.StoreAsync({resource.Name})-api: {stopwatch.ElapsedMilliseconds} ms");

            stopwatch.Reset();
            stopwatch.Start();
            var findResource = (await store.FindApiResourcesByNameAsync(new string[] { resource.Name })).FirstOrDefault();
            stopwatch.Stop();
            Console.WriteLine($"{nameof(ResourceStore.FindApiResourcesByNameAsync)}({resource.Name})-api: {stopwatch.ElapsedMilliseconds} ms");
            Assert.AreEqual<string>(resource.Name, findResource.Name);

            stopwatch.Reset();
            stopwatch.Start();
            string[] findScopes = new string[] { "api1Scope", Guid.NewGuid().ToString() };
            var findScopesResources = await store.FindApiResourcesByScopeNameAsync(findScopes);
            stopwatch.Stop();
            Console.WriteLine($"ResourceStore.FindApiResourcesByScopeAsync({string.Join(",", findScopes)})-api: {stopwatch.ElapsedMilliseconds} ms");
            Assert.AreEqual<string>(resource.Name, findScopesResources.Single()?.Name);

            stopwatch.Reset();
            stopwatch.Start();
            var resources = await store.GetAllResourcesAsync();
            int count = resources.ApiResources.Count();
            stopwatch.Stop();
            Console.WriteLine($"ResourceStore.GetAllResourcesAsync().ApiResources.Count: {count} : {stopwatch.ElapsedMilliseconds} ms");
            Assert.IsTrue(count > 0);


            stopwatch.Reset();
            stopwatch.Start();
            string findScope = findScopes[0];
            var apiScopes = await store.FindApiScopesByNameAsync(findScopes);
            stopwatch.Stop();
            Console.WriteLine($"ResourceStore.FindApiScopesByNameAsync({findScope})-api: {stopwatch.ElapsedMilliseconds} ms");
            Assert.AreEqual<int>(1, apiScopes.Where(w => w.Name == findScope).Count());


        }

        [TestMethod]
        public async Task ResourceStore_Api_RemoveGetTest()
        {
            Stopwatch stopwatch = new Stopwatch();

            var storageContext = Services.BuildServiceProvider().GetService<ResourceStorageContext>();
            Assert.IsNotNull(storageContext);

            var store = new ResourceStore(storageContext, _logger);
            Assert.IsNotNull(store);

            string name = Guid.NewGuid().ToString("n");
            var resource = CreateApiTestObject(name);
            Console.WriteLine(JsonConvert.SerializeObject(resource));

            stopwatch.Start();
            await store.StoreAsync(resource);
            stopwatch.Stop();
            Console.WriteLine($"ResourceStore.StoreAsync({resource.Name})-api: {stopwatch.ElapsedMilliseconds} ms");

            stopwatch.Reset();
            stopwatch.Start();
            var resources = await store.GetAllResourcesAsync();
            int count = resources.ApiResources.Count();
            stopwatch.Stop();
            Console.WriteLine($"ResourceStore.GetAllResourcesAsync().ApiResources.Count: {count} : {stopwatch.ElapsedMilliseconds} ms");
            Assert.IsNotNull(resources.ApiResources.FirstOrDefault(f=> f.Name == name));

            //Remove
            stopwatch.Reset();
            stopwatch.Start();
            await store.RemoveApiResourceAsync(resource.Name);
            stopwatch.Stop();
            Console.WriteLine($"ResourceStore.StoreAsync({resource.Name})-api: {stopwatch.ElapsedMilliseconds} ms");


            stopwatch.Reset();
            stopwatch.Start();
            var findResource = (await store.FindApiResourcesByNameAsync(new string[] { resource.Name })).FirstOrDefault();
            stopwatch.Stop();
            Console.WriteLine($"ResourceStore.FindResourceByIdAsync({resource.Name})-api: {stopwatch.ElapsedMilliseconds} ms");
            Assert.IsNull(findResource);

        }

        [TestMethod]
        public async Task ResourceStore_Identity_SaveGetTest()
        {
            Stopwatch stopwatch = new Stopwatch();

            var storageContext = Services.BuildServiceProvider().GetService<ResourceStorageContext>();
            Assert.IsNotNull(storageContext);

            var store = new ResourceStore(storageContext, _logger);
            Assert.IsNotNull(store);

            foreach (Model.IdentityResource resource in GetIdentityResources())
            {
                Console.WriteLine(JsonConvert.SerializeObject(resource));

                stopwatch.Start();
                await store.StoreAsync(resource);
                stopwatch.Stop();
                Console.WriteLine($"ResourceStore.StoreAsync({resource.Name})-identity: {stopwatch.ElapsedMilliseconds} ms");

                stopwatch.Reset();
                stopwatch.Start();

                string[] findScopes = new string[] { resource.Name, Guid.NewGuid().ToString() };
                var findScopesResources = await store.FindIdentityResourcesByScopeNameAsync(findScopes);
                stopwatch.Stop();
                Console.WriteLine($"ResourceStore.FindIdentityResourcesByScopeAsync({resource.Name})-identity: {stopwatch.ElapsedMilliseconds} ms");
                Assert.AreEqual<string>(resource.Name, findScopesResources.Single()?.Name);
               
            }
            stopwatch.Reset();
            stopwatch.Start();
            var resources = await store.GetAllResourcesAsync();
            int count = resources.IdentityResources.Count();
            stopwatch.Stop();
            Console.WriteLine($"ResourceStore.GetAllResourcesAsync().IdentityResources.Count: {count} : {stopwatch.ElapsedMilliseconds} ms");
            Assert.AreEqual<int>(GetIdentityResources().Count(), count) ;
        }

        [TestMethod]
        public async Task ResourceStore_Identity_RemoveGetTest()
        {
            Stopwatch stopwatch = new Stopwatch();

            var storageContext = Services.BuildServiceProvider().GetService<ResourceStorageContext>();
            Assert.IsNotNull(storageContext);

            var store = new ResourceStore(storageContext, _logger);
            Assert.IsNotNull(store);

            var resource = new IdentityResources.Address();
            Console.WriteLine(JsonConvert.SerializeObject(resource));

            stopwatch.Start();
            await store.StoreAsync(resource);
            stopwatch.Stop();
            Console.WriteLine($"ResourceStore.StoreAsync({resource.Name})-identity: {stopwatch.ElapsedMilliseconds} ms");

            
            stopwatch.Reset();
            stopwatch.Start();
            var resources = await store.GetAllResourcesAsync();
            int count = resources.IdentityResources.Count();
            stopwatch.Stop();
            Console.WriteLine($"ResourceStore.GetAllResourcesAsync().IdentityResources.Count: {count} : {stopwatch.ElapsedMilliseconds} ms");
            Assert.IsTrue(count > 0);

            stopwatch.Reset();
            //Remove
            stopwatch.Start();

            await store.RemoveIdentityResourceAsync(resource.Name);
            stopwatch.Stop();
            Console.WriteLine($"ResourceStore.RemoveIdentityResourceAsync({resource.Name})-identity: {stopwatch.ElapsedMilliseconds} ms");

            stopwatch.Reset();
            stopwatch.Start();
            resources = await store.GetAllResourcesAsync();
            stopwatch.Stop();
            Console.WriteLine($"ResourceStore.GetAllResourcesAsync().IdentityResources.Count: {count} : {stopwatch.ElapsedMilliseconds} ms");
            Assert.IsNull(resources.IdentityResources.FirstOrDefault(f=> f.Name == resource.Name));

        }
    }
}
