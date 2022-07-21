// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace ElCamino.Duende.IdentityServer.AzureStorage.Configuration
{
    public class ResourceStorageConfig
    {
        public string ApiTableName { get; set; }

        public string StorageConnectionString { get; set; }

        public string ApiBlobContainerName { get; set; }

        public string ApiScopeBlobContainerName { get; set; }

        public string IdentityBlobContainerName { get; set; }

        public string ApiBlobCacheContainerName { get; set; }

        public string ApiScopeBlobCacheContainerName { get; set; }

        public string IdentityBlobCacheContainerName { get; set; }


        /// <summary>
        /// Gets or sets a value indicating whether blob cache will be refreshed on a schedule.
        /// This is implemented by periodically connecting to blob storage(according to the CacheRefreshInterval) from the hosting application.
        /// Defaults to false.
        /// </summary>
        /// <value>
        ///   <c>true</c> if [enable cache refresh]; otherwise, <c>false</c>.
        /// </value>
        public bool EnableCacheRefresh { get; set; } = false;

        /// <summary>
        /// Gets or sets the cach refresh interval (in seconds). The default is 1800 (.5 hour).
        /// </summary>
        /// <value>
        /// The cache refresh interval.
        /// </value>
        public int CacheRefreshInterval { get; set; } = 1800;
    }
}
