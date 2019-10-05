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

namespace ElCamino.IdentityServer4.AzureStorage.Contexts
{
    public class ResourceStorageContext : StorageContext
    {
        private ResourceStorageConfig _config = null;
        private string ApiBlobContainerName = string.Empty;
        private string IdentityBlobContainerName = string.Empty;

        private string ApiBlobCacheContainerName = string.Empty;
        private string IdentityBlobCacheContainerName = string.Empty;

        public const string DefaultApiBlobCacheContainerName = "resourceapiblobcache";
        public const string DefaultIdentityBlobCacheContainerName = "resourceidentityblobcache";

        public string ApiResourceTableName { get; private set; }

        public CloudTable ApiResourceTable { get; private set; }

        public CloudBlobContainer ApiResourceBlobContainer { get; private set; }

        public CloudBlobContainer IdentityResourceBlobContainer { get; private set; }

        public CloudBlobContainer ApiResourceBlobCacheContainer { get; private set; }

        public CloudBlobContainer IdentityResourceBlobCacheContainer { get; private set; }


        public CloudTableClient TableClient { get; private set; }

        public CloudBlobClient BlobClient { get; private set; }


        public ResourceStorageContext(IOptions<ResourceStorageConfig> config) : this(config.Value)
        {
        }


        public ResourceStorageContext(ResourceStorageConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }
            Initialize(config);
        }

        public async Task<bool> CreateStorageIfNotExists()
        {
            var tasks = new List<Task<bool>>() {
                ApiResourceTable.CreateIfNotExistsAsync(),
                ApiResourceBlobContainer.CreateIfNotExistsAsync(),
                IdentityResourceBlobContainer.CreateIfNotExistsAsync(),
                ApiResourceBlobCacheContainer.CreateIfNotExistsAsync(),
                IdentityResourceBlobCacheContainer.CreateIfNotExistsAsync()};
            await Task.WhenAll(tasks).ConfigureAwait(false);
            return tasks.Select(t => t.Result).All(a => a);
        }

        protected virtual void Initialize(ResourceStorageConfig config)
        {
            _config = config;
            TableClient = Microsoft.Azure.Cosmos.Table.CloudStorageAccount.Parse(_config.StorageConnectionString).CreateCloudTableClient();
            TableClient.DefaultRequestOptions.PayloadFormat = TablePayloadFormat.Json;

            ApiResourceTableName = config.ApiTableName;

            if (string.IsNullOrWhiteSpace(ApiResourceTableName))
            {
                throw new ArgumentException($"ApiResourceTableName cannot be null or empty, check your configuration.", nameof(config.ApiTableName));
            }

            ApiResourceTable = TableClient.GetTableReference(ApiResourceTableName);

            BlobClient = Microsoft.Azure.Storage.CloudStorageAccount.Parse(_config.StorageConnectionString).CreateCloudBlobClient();
            ApiBlobContainerName = config.ApiBlobContainerName;
            if (string.IsNullOrWhiteSpace(ApiBlobContainerName))
            {
                throw new ArgumentException($"ApiBlobContainerName cannot be null or empty, check your configuration.", nameof(config.ApiBlobContainerName));
            }
            ApiResourceBlobContainer = BlobClient.GetContainerReference(ApiBlobContainerName);

            ApiBlobCacheContainerName = !string.IsNullOrWhiteSpace(config.ApiBlobCacheContainerName) ? config.ApiBlobCacheContainerName : DefaultApiBlobCacheContainerName;
            ApiResourceBlobCacheContainer = BlobClient.GetContainerReference(ApiBlobCacheContainerName);

            IdentityBlobContainerName = config.IdentityBlobContainerName;
            if (string.IsNullOrWhiteSpace(IdentityBlobContainerName))
            {
                throw new ArgumentException($"IdentityBlobContainerName cannot be null or empty, check your configuration.", nameof(config.IdentityBlobContainerName));
            }
            IdentityResourceBlobContainer = BlobClient.GetContainerReference(IdentityBlobContainerName);

            IdentityBlobCacheContainerName = !string.IsNullOrWhiteSpace(config.IdentityBlobCacheContainerName) ? config.IdentityBlobCacheContainerName : DefaultIdentityBlobCacheContainerName;
            IdentityResourceBlobCacheContainer = BlobClient.GetContainerReference(IdentityBlobCacheContainerName);

        }
    }
}
