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
using System.Text.Json;

namespace ElCamino.IdentityServer.AzureStorage.Contexts
{
    public class SigningKeyStorageContext : StorageContext
    {
        private string BlobContainerName = string.Empty;

        public BlobServiceClient BlobClient { get; private set; }

        public BlobContainerClient SigningKeyBlobContainer { get; private set; }

        public override JsonSerializerOptions JsonSerializerDefaultOptions => new () { IncludeFields = true };

        public SigningKeyStorageContext(IOptions<SigningKeyStorageConfig> config, 
            BlobServiceClient blobClient) : this(config.Value, blobClient)
        {
        }

        public SigningKeyStorageContext(SigningKeyStorageConfig config,
            BlobServiceClient blobClient)
        {
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(blobClient);
            BlobClient = blobClient;    
            Initialize(config);
        }

        protected virtual void Initialize(SigningKeyStorageConfig config)
        {
            BlobContainerName = config.BlobContainerName;
            if (string.IsNullOrWhiteSpace(BlobContainerName))
            {
                throw new ArgumentException($"{nameof(config.BlobContainerName)} cannot be null or empty, check your configuration.", nameof(config));
            }
            SigningKeyBlobContainer = BlobClient.GetBlobContainerClient(BlobContainerName);
        }

        public async Task<bool> CreateStorageIfNotExists()
        {
            Task[] tasks = [ SigningKeyBlobContainer.CreateIfNotExistsAsync() ];
            await Task.WhenAll(tasks).ConfigureAwait(false);
            return tasks.All(a => a.IsCompleted);       
        }
    }
}
