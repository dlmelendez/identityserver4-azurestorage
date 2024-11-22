// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using ElCamino.IdentityServer.AzureStorage.Configuration;
using Microsoft.Extensions.Options;

namespace ElCamino.IdentityServer.AzureStorage.Contexts
{
    public class ClientStorageContext : StorageContext
    {
        private string BlobContainerName = string.Empty;
        private string BlobCacheContainerName = string.Empty;

        public BlobServiceClient BlobClient { get; private set; }

        public BlobContainerClient ClientBlobContainer { get; private set; }

        public BlobContainerClient ClientCacheBlobContainer { get; private set; }

        public const string DefaultClientCacheBlobContainer = "clientstorecache";

        public ClientStorageContext(IOptions<ClientStorageConfig> config,
            BlobServiceClient blobClient) : this(config.Value, blobClient)
        {
        }

        public ClientStorageContext(ClientStorageConfig config,
            BlobServiceClient blobClient)
        {
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(blobClient);

            BlobClient = blobClient;
            Initialize(config);
        }

        protected virtual void Initialize(ClientStorageConfig config)
        {
            BlobContainerName = config.BlobContainerName;
            if (string.IsNullOrWhiteSpace(BlobContainerName))
            {
                throw new ArgumentException($"{nameof(config.BlobContainerName)} cannot be null or empty, check your configuration.", nameof(config));
            }
            ClientBlobContainer = BlobClient.GetBlobContainerClient(BlobContainerName);
            BlobCacheContainerName = !string.IsNullOrWhiteSpace(config.BlobCacheContainerName) ? config.BlobCacheContainerName : DefaultClientCacheBlobContainer;
            ClientCacheBlobContainer = BlobClient.GetBlobContainerClient(BlobCacheContainerName);
        }

        public async Task<bool> CreateStorageIfNotExists()
        {
            Task[] tasks = [ ClientBlobContainer.CreateIfNotExistsAsync(), ClientCacheBlobContainer.CreateIfNotExistsAsync() ];
            await Task.WhenAll(tasks).ConfigureAwait(false);
            return tasks.All(a => a.IsCompleted);       
        }
    }
}
