// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using ElCamino.Duende.IdentityServer.AzureStorage.Configuration;
using Microsoft.Extensions.Options;
using Azure.Storage.Blobs;
using Microsoft.Azure.Cosmos.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ElCamino.Duende.IdentityServer.AzureStorage.Entities;
using ElCamino.Duende.IdentityServer.AzureStorage.Helpers;
using ElCamino.Duende.IdentityServer.AzureStorage.Mappers;

namespace ElCamino.Duende.IdentityServer.AzureStorage.Contexts
{
    public class DeviceFlowStorageContext : StorageContext
    {

        public BlobServiceClient BlobClient { get; private set; }

        public BlobContainerClient UserCodeBlobContainer { get; private set; }

        public BlobContainerClient DeviceCodeBlobContainer { get; private set; }

        public DeviceFlowStorageContext(IOptions<DeviceFlowStorageConfig> config) : this(config.Value)
        {
        }


        public DeviceFlowStorageContext(DeviceFlowStorageConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }
            Initialize(config);
        }

        protected virtual void Initialize(DeviceFlowStorageConfig config)
        {

            BlobClient = new BlobServiceClient(config.StorageConnectionString);
            if (string.IsNullOrWhiteSpace(config.BlobUserContainerName))
            {
                throw new ArgumentException($"{nameof(config.BlobUserContainerName)} cannot be null or empty, check your configuration.", nameof(config.BlobUserContainerName));
            }
            UserCodeBlobContainer = BlobClient.GetBlobContainerClient(config.BlobUserContainerName);

            if (string.IsNullOrWhiteSpace(config.BlobDeviceContainerName))
            {
                throw new ArgumentException($"{nameof(config.BlobDeviceContainerName)} cannot be null or empty, check your configuration.", nameof(config.BlobDeviceContainerName));
            }
            DeviceCodeBlobContainer = BlobClient.GetBlobContainerClient(config.BlobDeviceContainerName);

        }

        public async Task<bool> CreateStorageIfNotExists()
        {
            var tasks = new Task[] { UserCodeBlobContainer.CreateIfNotExistsAsync(),
                DeviceCodeBlobContainer.CreateIfNotExistsAsync()};
            await Task.WhenAll(tasks).ConfigureAwait(false);
            return tasks.Select(t => t.IsCompleted).All(a => a);
        }

    }
}
