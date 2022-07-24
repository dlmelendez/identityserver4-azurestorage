// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using ElCamino.Duende.IdentityServer.AzureStorage.Configuration;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

namespace ElCamino.Duende.IdentityServer.AzureStorage.Contexts
{
    public class SigningKeyStorageContext : StorageContext
    {
        private SigningKeyStorageConfig _config = null;
        private string BlobContainerName = string.Empty;
        private string BlobCacheContainerName = string.Empty;

        public BlobServiceClient BlobClient { get; private set; }

        public BlobContainerClient SigningKeyBlobContainer { get; private set; }

        public override JsonSerializerOptions JsonSerializerDefaultOptions => new JsonSerializerOptions() { IncludeFields = true };

        public SigningKeyStorageContext(IOptions<SigningKeyStorageConfig> config) : this(config.Value)
        {
        }


        public SigningKeyStorageContext(SigningKeyStorageConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }
            Initialize(config);
        }

        protected virtual void Initialize(SigningKeyStorageConfig config)
        {
            _config = config;


            BlobClient = new BlobServiceClient(_config.StorageConnectionString);
            BlobContainerName = config.BlobContainerName;
            if (string.IsNullOrWhiteSpace(BlobContainerName))
            {
                throw new ArgumentException($"BlobContainerName cannot be null or empty, check your configuration.", nameof(config.BlobContainerName));
            }
            SigningKeyBlobContainer = BlobClient.GetBlobContainerClient(BlobContainerName);

        }

        public async Task<bool> CreateStorageIfNotExists()
        {
            var tasks = new Task[] { SigningKeyBlobContainer.CreateIfNotExistsAsync() };
            await Task.WhenAll(tasks).ConfigureAwait(false);
            return tasks.All(a => a.IsCompleted);       
        }
    }
}
