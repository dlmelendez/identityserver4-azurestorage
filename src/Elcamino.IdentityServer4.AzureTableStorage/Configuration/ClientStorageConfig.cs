// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;

namespace ElCamino.IdentityServer.AzureStorage.Configuration
{
    public class ClientStorageConfig
    {
        public string StorageConnectionString { get; set; }

        public string BlobContainerName { get; set; }

        public string BlobCacheContainerName { get; set; }

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
