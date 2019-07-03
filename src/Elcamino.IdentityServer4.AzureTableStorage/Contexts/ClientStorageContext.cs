// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using ElCamino.IdentityServer4.AzureStorage.Configuration;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ElCamino.IdentityServer4.AzureStorage.Contexts
{
    public class ClientStorageContext : StorageContext
    {
        private ClientStorageConfig _config = null;
        private string BlobContainerName = string.Empty;

        public CloudBlobClient BlobClient { get; private set; }

        public CloudBlobContainer ClientBlobContainer { get; private set; }


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


            BlobClient = Microsoft.Azure.Storage.CloudStorageAccount.Parse(_config.StorageConnectionString).CreateCloudBlobClient();
            BlobContainerName = config.BlobContainerName;
            if (string.IsNullOrWhiteSpace(BlobContainerName))
            {
                throw new ArgumentException($"BlobContainerName cannot be null or empty, check your configuration.", nameof(config.BlobContainerName));
            }
            ClientBlobContainer = BlobClient.GetContainerReference(BlobContainerName);

        }

        public async Task<bool> CreateStorageIfNotExists()
        {
            return await ClientBlobContainer.CreateIfNotExistsAsync();
           
        }
    }
}
