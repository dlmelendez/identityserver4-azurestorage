// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using ElCamino.IdentityServer.AzureStorage.Configuration;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ElCamino.IdentityServer.AzureStorage.Contexts
{
    public class ClientStorageContext : StorageContext
    {
        private ClientStorageConfig _config = null;
        private string BlobContainerName = string.Empty;
        private string BlobCacheContainerName = string.Empty;

        public BlobServiceClient BlobClient { get; private set; }

        public BlobContainerClient ClientBlobContainer { get; private set; }

        public BlobContainerClient ClientCacheBlobContainer { get; private set; }

        public const string DefaultClientCacheBlobContainer = "clientstorecache";

        public ClientStorageContext(IOptions<ClientStorageConfig> config) : this(config.Value)
        {
        }


        public ClientStorageContext(ClientStorageConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }
            Initialize(config);
        }

        protected virtual void Initialize(ClientStorageConfig config)
        {
            _config = config;


            BlobClient = new BlobServiceClient(_config.StorageConnectionString);
            BlobContainerName = config.BlobContainerName;
            if (string.IsNullOrWhiteSpace(BlobContainerName))
            {
                throw new ArgumentException($"BlobContainerName cannot be null or empty, check your configuration.", nameof(config.BlobContainerName));
            }
            ClientBlobContainer = BlobClient.GetBlobContainerClient(BlobContainerName);
            BlobCacheContainerName = !string.IsNullOrWhiteSpace(config.BlobCacheContainerName) ? config.BlobCacheContainerName : DefaultClientCacheBlobContainer;
            ClientCacheBlobContainer = BlobClient.GetBlobContainerClient(BlobCacheContainerName);

        }

        public async Task<bool> CreateStorageIfNotExists()
        {
            var tasks = new Task[] { ClientBlobContainer.CreateIfNotExistsAsync(), ClientCacheBlobContainer.CreateIfNotExistsAsync() };
            await Task.WhenAll(tasks).ConfigureAwait(false);
            return tasks.All(a => a.IsCompleted);       
        }
    }
}
