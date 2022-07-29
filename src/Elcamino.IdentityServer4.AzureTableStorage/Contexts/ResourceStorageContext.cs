﻿// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using ElCamino.IdentityServer4.AzureStorage.Configuration;
using Microsoft.Extensions.Options;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
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
        private string ApiScopeBlobContainerName = string.Empty;
        private string IdentityBlobContainerName = string.Empty;

        private string ApiBlobCacheContainerName = string.Empty;
        private string ApiScopeBlobCacheContainerName = string.Empty;
        private string IdentityBlobCacheContainerName = string.Empty;

        public const string DefaultApiBlobCacheContainerName = "resourceapiblobcache";
        public const string DefaultApiScopeBlobCacheContainerName = "resourceapiscopeblobcache";
        public const string DefaultIdentityBlobCacheContainerName = "resourceidentityblobcache";

        public string ApiResourceTableName { get; private set; }

        public TableClient ApiResourceTable { get; private set; }


        public BlobContainerClient ApiResourceBlobContainer { get; private set; }

        public BlobContainerClient ApiResourceBlobCacheContainer { get; private set; }

        public BlobContainerClient ApiScopeBlobContainer { get; private set; }

        public BlobContainerClient ApiScopeBlobCacheContainer { get; private set; }

        public BlobContainerClient IdentityResourceBlobContainer { get; private set; }

        public BlobContainerClient IdentityResourceBlobCacheContainer { get; private set; }

        public TableServiceClient TableClient { get; private set; }

        public BlobServiceClient BlobClient { get; private set; }

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
            var tasks = new List<Task>() {
                ApiResourceTable.CreateIfNotExistsAsync(),
                ApiResourceBlobContainer.CreateIfNotExistsAsync(),
                ApiScopeBlobContainer.CreateIfNotExistsAsync(),
                IdentityResourceBlobContainer.CreateIfNotExistsAsync(),
                ApiResourceBlobCacheContainer.CreateIfNotExistsAsync(),
                ApiScopeBlobCacheContainer.CreateIfNotExistsAsync(),
                IdentityResourceBlobCacheContainer.CreateIfNotExistsAsync()};
            await Task.WhenAll(tasks).ConfigureAwait(false);
            return tasks.Select(t => t.IsCompleted).All(a => a);
        }

        protected virtual void Initialize(ResourceStorageConfig config)
        {
            _config = config;
            TableClient = new TableServiceClient(_config.StorageConnectionString);

            //ApiResourceTableName
            ApiResourceTableName = config.ApiTableName;

            if (string.IsNullOrWhiteSpace(ApiResourceTableName))
            {
                throw new ArgumentException($"{nameof(config.ApiTableName)} cannot be null or empty, check your configuration.", nameof(config));
            }

            ApiResourceTable = TableClient.GetTableClient(ApiResourceTableName);

            BlobClient = new BlobServiceClient(_config.StorageConnectionString);

            // ApiResource blob config
            ApiBlobContainerName = config.ApiBlobContainerName;
            if (string.IsNullOrWhiteSpace(ApiBlobContainerName))
            {
                throw new ArgumentException($"{nameof(config.ApiBlobContainerName)} cannot be null or empty, check your configuration.", nameof(config));
            }
            ApiResourceBlobContainer = BlobClient.GetBlobContainerClient(ApiBlobContainerName);

            ApiBlobCacheContainerName = !string.IsNullOrWhiteSpace(config.ApiBlobCacheContainerName) ? config.ApiBlobCacheContainerName : DefaultApiBlobCacheContainerName;
            ApiResourceBlobCacheContainer = BlobClient.GetBlobContainerClient(ApiBlobCacheContainerName);

            // ApiScope blob config
            ApiScopeBlobContainerName = config.ApiScopeBlobContainerName;
            if (string.IsNullOrWhiteSpace(ApiScopeBlobContainerName))
            {
                throw new ArgumentException($"{nameof(config.ApiScopeBlobContainerName)} cannot be null or empty, check your configuration.", nameof(config));
            }
            ApiScopeBlobContainer = BlobClient.GetBlobContainerClient(ApiScopeBlobContainerName);

            ApiScopeBlobCacheContainerName = !string.IsNullOrWhiteSpace(config.ApiScopeBlobCacheContainerName) ? config.ApiScopeBlobCacheContainerName : DefaultApiScopeBlobCacheContainerName;
            ApiScopeBlobCacheContainer = BlobClient.GetBlobContainerClient(ApiScopeBlobCacheContainerName);

            //IdentityResource blob config
            IdentityBlobContainerName = config.IdentityBlobContainerName;
            if (string.IsNullOrWhiteSpace(IdentityBlobContainerName))
            {
                throw new ArgumentException($"{nameof(config.IdentityBlobContainerName)} cannot be null or empty, check your configuration.", nameof(config));
            }
            IdentityResourceBlobContainer = BlobClient.GetBlobContainerClient(IdentityBlobContainerName);

            IdentityBlobCacheContainerName = !string.IsNullOrWhiteSpace(config.IdentityBlobCacheContainerName) ? config.IdentityBlobCacheContainerName : DefaultIdentityBlobCacheContainerName;
            IdentityResourceBlobCacheContainer = BlobClient.GetBlobContainerClient(IdentityBlobCacheContainerName);

        }
    }
}
