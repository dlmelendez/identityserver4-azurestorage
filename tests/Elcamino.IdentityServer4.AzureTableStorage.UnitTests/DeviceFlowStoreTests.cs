// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using ElCamino.IdentityServer4.AzureStorage.Contexts;
using ElCamino.IdentityServer4.AzureStorage.Mappers;
using ElCamino.IdentityServer4.AzureStorage.Stores;
using IdentityServer4.Models;
using IdentityServer4.Stores.Serialization;
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
using DeviceCode = IdentityServer4.Models.DeviceCode;
using System.Security.Claims;
using IdentityModel;

namespace ElCamino.IdentityServer4.AzureStorage.UnitTests
{
    [TestClass]
    public class DeviceFlowStoreTests : BaseTests
    {
        private ILogger<DeviceFlowStore> _logger;
        private IPersistentGrantSerializer _persistentGrantSerializer;

        private static DeviceCode CreateTestObject(
            string clientId = null,
            bool? isAuthorized = null,
            bool? isOpenId = null,
            int? lifetime = null,
            string type = null)
        {
            const string Issuer = "https://identityserver.com";
            var claims = new List<Claim> 
            {
                new Claim(JwtClaimTypes.Subject, "My Subject", ClaimValueTypes.String, Issuer),
            };
            var userIdentity = new ClaimsIdentity(claims, "Passport");
            var userPrincipal = new ClaimsPrincipal(userIdentity); 
            
            return new DeviceCode
            {
                AuthorizedScopes = new List<string>() { "scope0", "scope1" },
                IsAuthorized = isAuthorized ?? true,
                IsOpenId = isOpenId ?? true,
                Lifetime = lifetime?? (int)TimeSpan.FromDays(1.0).TotalSeconds,
                RequestedScopes = new List<string>() { "scope0" },
                Subject = userPrincipal,
                ClientId = clientId ?? Guid.NewGuid().ToString(),
                CreationTime = DateTime.UtcNow,
            };
        }

        [TestInitialize]
        public void Initialize()
        {
            var loggerFactory = Services.BuildServiceProvider().GetService<ILoggerFactory>();
            _logger = loggerFactory.CreateLogger<DeviceFlowStore>();
            _persistentGrantSerializer = Services.BuildServiceProvider().GetService<IPersistentGrantSerializer>();
        }

        [TestMethod]
        public void DeviceFlowStore_CtorsTest()
        {
            var storageContext = Services.BuildServiceProvider().GetService<DeviceFlowStorageContext>();
            Assert.IsNotNull(storageContext);


            var store = new DeviceFlowStore(storageContext, _persistentGrantSerializer, _logger);
            Assert.IsNotNull(store);
        }

        [TestMethod]
        public async Task DeviceFlowStore_SaveGetByCodesTest()
        {
            var storageContext = Services.BuildServiceProvider().GetService<DeviceFlowStorageContext>();
            Assert.IsNotNull(storageContext);

            var store = new DeviceFlowStore(storageContext, _persistentGrantSerializer, _logger);
            Assert.IsNotNull(store);
            string userCode = Guid.NewGuid().ToString("n");
            string deviceCode = Guid.NewGuid().ToString("n");
            Console.WriteLine($"userCode: {userCode}");
            Console.WriteLine($"deviceCode: {deviceCode}");

            DeviceCode deviceCodeData = CreateTestObject();

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            await store.StoreDeviceAuthorizationAsync(deviceCode, userCode, deviceCodeData);
            stopwatch.Stop();
            Console.WriteLine($"DeviceFlowStore.StoreDeviceAuthorizationAsync({deviceCode},{userCode}): {stopwatch.ElapsedMilliseconds} ms");

            stopwatch.Reset();
            
            stopwatch.Start();
            var findByDeviceCode = await store.FindByDeviceCodeAsync(deviceCode);
            stopwatch.Stop();
            Console.WriteLine($"DeviceFlowStore.FindByDeviceCodeAsync({deviceCode}): {stopwatch.ElapsedMilliseconds} ms");
            AssertDeviceCodesEqual(deviceCodeData, findByDeviceCode);

            stopwatch.Reset();

            stopwatch.Start();
            var findByUserCode = await store.FindByUserCodeAsync(userCode);
            stopwatch.Stop();
            Console.WriteLine($"DeviceFlowStore.FindByUserCodeAsync({userCode}): {stopwatch.ElapsedMilliseconds} ms");
            AssertDeviceCodesEqual(deviceCodeData, findByUserCode);

            AssertDeviceCodesEqual(findByDeviceCode, findByUserCode);


        }

        [TestMethod]
        public async Task DeviceFlowStore_SaveUpdateGetByCodesTest()
        {
            var storageContext = Services.BuildServiceProvider().GetService<DeviceFlowStorageContext>();
            Assert.IsNotNull(storageContext);

            var store = new DeviceFlowStore(storageContext, _persistentGrantSerializer, _logger);
            Assert.IsNotNull(store);
            string userCode = Guid.NewGuid().ToString("n");
            string deviceCode = Guid.NewGuid().ToString("n");
            Console.WriteLine($"userCode: {userCode}");
            Console.WriteLine($"deviceCode: {deviceCode}");

            DeviceCode deviceCodeData = CreateTestObject();

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            await store.StoreDeviceAuthorizationAsync(deviceCode, userCode, deviceCodeData);
            stopwatch.Stop();
            Console.WriteLine($"DeviceFlowStore.StoreDeviceAuthorizationAsync({deviceCode},{userCode}): {stopwatch.ElapsedMilliseconds} ms");

            stopwatch.Reset();
            stopwatch.Start();
            string oldClientId = deviceCodeData.ClientId;
            string newClientId = Guid.NewGuid().ToString("n");
            deviceCodeData.ClientId = newClientId;

            await store.UpdateByUserCodeAsync(userCode, deviceCodeData);
            stopwatch.Stop();
            Console.WriteLine($"DeviceFlowStore.UpdateByUserCodeAsync({userCode}): {stopwatch.ElapsedMilliseconds} ms");

            stopwatch.Reset();
            stopwatch.Start();
            var findByDeviceCode = await store.FindByDeviceCodeAsync(deviceCode);
            stopwatch.Stop();
            Console.WriteLine($"DeviceFlowStore.FindByDeviceCodeAsync({deviceCode}): {stopwatch.ElapsedMilliseconds} ms");
            AssertDeviceCodesEqual(deviceCodeData, findByDeviceCode);
            Assert.AreEqual<string>(newClientId, findByDeviceCode.ClientId);
            Assert.AreNotEqual<string>(oldClientId, findByDeviceCode.ClientId);

            stopwatch.Reset();

            stopwatch.Start();
            var findByUserCode = await store.FindByUserCodeAsync(userCode);
            stopwatch.Stop();
            Console.WriteLine($"DeviceFlowStore.FindByUserCodeAsync({userCode}): {stopwatch.ElapsedMilliseconds} ms");
            AssertDeviceCodesEqual(deviceCodeData, findByUserCode);
            Assert.AreEqual<string>(newClientId, findByUserCode.ClientId);
            Assert.AreNotEqual<string>(oldClientId, findByUserCode.ClientId);

            AssertDeviceCodesEqual(findByDeviceCode, findByUserCode);


        }

        [TestMethod]
        public async Task DeviceFlowStore_SaveRemoveByCodesTest()
        {
            var storageContext = Services.BuildServiceProvider().GetService<DeviceFlowStorageContext>();
            Assert.IsNotNull(storageContext);

            var store = new DeviceFlowStore(storageContext, _persistentGrantSerializer, _logger);
            Assert.IsNotNull(store);
            string userCode = Guid.NewGuid().ToString("n");
            string deviceCode = Guid.NewGuid().ToString("n");
            Console.WriteLine($"userCode: {userCode}");
            Console.WriteLine($"deviceCode: {deviceCode}");

            DeviceCode deviceCodeData = CreateTestObject();

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            await store.StoreDeviceAuthorizationAsync(deviceCode, userCode, deviceCodeData);
            stopwatch.Stop();
            Console.WriteLine($"DeviceFlowStore.StoreDeviceAuthorizationAsync({deviceCode},{userCode}): {stopwatch.ElapsedMilliseconds} ms");

            stopwatch.Reset();
            stopwatch.Start();

            await store.RemoveByDeviceCodeAsync(deviceCode);
            stopwatch.Stop();
            Console.WriteLine($"DeviceFlowStore.RemoveByDeviceCodeAsync({deviceCode}): {stopwatch.ElapsedMilliseconds} ms");

            stopwatch.Reset();

            stopwatch.Start();
            var findByDeviceCode = await store.FindByDeviceCodeAsync(deviceCode);
            stopwatch.Stop();
            Console.WriteLine($"DeviceFlowStore.FindByDeviceCodeAsync({deviceCode}): {stopwatch.ElapsedMilliseconds} ms");
            Assert.IsNull(findByDeviceCode);

            stopwatch.Reset();

            stopwatch.Start();
            var findByUserCode = await store.FindByUserCodeAsync(userCode);
            stopwatch.Stop();
            Console.WriteLine($"DeviceFlowStore.FindByUserCodeAsync({userCode}): {stopwatch.ElapsedMilliseconds} ms");
            Assert.IsNull(findByUserCode);

        }

        [TestMethod]
        public async Task DeviceFlowStore_GetByCodesNegativeTest()
        {
            var storageContext = Services.BuildServiceProvider().GetService<DeviceFlowStorageContext>();
            Assert.IsNotNull(storageContext);

            var store = new DeviceFlowStore(storageContext, _persistentGrantSerializer, _logger);
            Assert.IsNotNull(store);
            string userCode = Guid.NewGuid().ToString("n");
            string deviceCode = Guid.NewGuid().ToString("n");
            Console.WriteLine($"userCode: {userCode}");
            Console.WriteLine($"deviceCode: {deviceCode}");

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            var findByDeviceCode = await store.FindByDeviceCodeAsync(deviceCode);
            stopwatch.Stop();
            Console.WriteLine($"DeviceFlowStore.FindByDeviceCodeAsync({deviceCode}): {stopwatch.ElapsedMilliseconds} ms");
            Assert.IsNull(findByDeviceCode);

            stopwatch.Reset();

            stopwatch.Start();
            var findByUserCode = await store.FindByUserCodeAsync(userCode);
            stopwatch.Stop();
            Console.WriteLine($"DeviceFlowStore.FindByUserCodeAsync({userCode}): {stopwatch.ElapsedMilliseconds} ms");
            Assert.IsNull(findByUserCode);


        }

        [TestMethod]
        public async Task DeviceFlowStore_RemoveByKeyNegativeTest()
        {
            var storageContext = Services.BuildServiceProvider().GetService<DeviceFlowStorageContext>();
            Assert.IsNotNull(storageContext);

            var store = new DeviceFlowStore(storageContext, _persistentGrantSerializer, _logger);
            Assert.IsNotNull(store);
            string userCode = Guid.NewGuid().ToString("n");
            string deviceCode = Guid.NewGuid().ToString("n");
            Console.WriteLine($"userCode: {userCode}");
            Console.WriteLine($"deviceCode: {deviceCode}");

            DeviceCode deviceCodeData = CreateTestObject();

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            await store.RemoveByDeviceCodeAsync(deviceCode);
            stopwatch.Stop();
            Console.WriteLine($"DeviceFlowStore.RemoveByDeviceCodeAsync({deviceCode}): {stopwatch.ElapsedMilliseconds} ms");

        }

        public void AssertDeviceCodesEqual(DeviceCode expected, DeviceCode actual)
        {
            Assert.IsNotNull(expected);
            Assert.IsNotNull(actual);

            Assert.AreEqual<string>(_persistentGrantSerializer.Serialize(expected),
                _persistentGrantSerializer.Serialize(actual));

        }
    }
}
