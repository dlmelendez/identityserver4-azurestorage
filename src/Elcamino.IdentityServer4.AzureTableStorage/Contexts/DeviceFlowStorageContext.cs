// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using ElCamino.IdentityServer4.AzureStorage.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.Cosmos.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ElCamino.IdentityServer4.AzureStorage.Entities;
using ElCamino.IdentityServer4.AzureStorage.Helpers;
using ElCamino.IdentityServer4.AzureStorage.Mappers;

namespace ElCamino.IdentityServer4.AzureStorage.Contexts
{
    public class DeviceFlowStorageContext : StorageContext
    {

        public CloudBlobClient BlobClient { get; private set; }

        public CloudBlobContainer UserCodeBlobContainer { get; private set; }

        public CloudBlobContainer DeviceCodeBlobContainer { get; private set; }

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

            BlobClient = Microsoft.Azure.Storage.CloudStorageAccount.Parse(config.StorageConnectionString).CreateCloudBlobClient();
            if (string.IsNullOrWhiteSpace(config.BlobUserContainerName))
            {
                throw new ArgumentException($"{nameof(config.BlobUserContainerName)} cannot be null or empty, check your configuration.", nameof(config.BlobUserContainerName));
            }
            UserCodeBlobContainer = BlobClient.GetContainerReference(config.BlobUserContainerName);

            if (string.IsNullOrWhiteSpace(config.BlobDeviceContainerName))
            {
                throw new ArgumentException($"{nameof(config.BlobDeviceContainerName)} cannot be null or empty, check your configuration.", nameof(config.BlobDeviceContainerName));
            }
            DeviceCodeBlobContainer = BlobClient.GetContainerReference(config.BlobDeviceContainerName);

        }

        public async Task<bool> CreateStorageIfNotExists()
        {
            var tasks = new Task<bool>[] { UserCodeBlobContainer.CreateIfNotExistsAsync(),
                DeviceCodeBlobContainer.CreateIfNotExistsAsync()};
            await Task.WhenAll(tasks).ConfigureAwait(false);
            return tasks.Select(t => t.Result).All(a => a);
        }

    }
}
