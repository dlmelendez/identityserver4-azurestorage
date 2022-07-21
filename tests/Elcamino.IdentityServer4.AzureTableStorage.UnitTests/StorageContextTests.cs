// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using ElCamino.Duende.IdentityServer.AzureStorage.Contexts;
using ElCamino.Duende.IdentityServer.AzureStorage.Stores;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
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
    public class StorageContextTests : BaseTests
    {

        [TestMethod]
        public void StorageContext_CtorsTest()
        {
            var storageContext = Services.BuildServiceProvider().GetService<PersistedGrantStorageContext>();
            Assert.IsNotNull(storageContext);
        }

        [TestMethod]
        public async Task StorageContext_SuppressBlobNotFoundErrorTest()
        {
            var storageContext = Services.BuildServiceProvider().GetService<PersistedGrantStorageContext>();
            string strBlob = await storageContext.GetBlobContentAsync(Guid.NewGuid().ToString(), storageContext.PersistedGrantBlobContainer);
            Assert.AreEqual<string>(string.Empty, strBlob);
        }


    }

}
