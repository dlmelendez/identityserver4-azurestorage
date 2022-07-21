// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ElCamino.Duende.IdentityServer.AzureStorage.Configuration
{
    [JsonObject("persistedGrantStorageConfig")]
    public class PersistedGrantStorageConfig
    {
        [JsonProperty("storageConnectionString")]
        public string StorageConnectionString { get; set; }

        [JsonProperty("blobContainerName")]
        public string BlobContainerName { get; set; }

        [JsonProperty("persistedGrantTableName")]
        public string PersistedGrantTableName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether stale entries will be automatically cleaned up from the database.
        /// This is implemented by periodically connecting to the database (according to the TokenCleanupInterval) from the hosting application.
        /// Defaults to false.
        /// </summary>
        /// <value>
        ///   <c>true</c> if [enable token cleanup]; otherwise, <c>false</c>.
        /// </value>
        [JsonProperty("enableTokenCleanup")]
        public bool EnableTokenCleanup { get; set; } = false;

        /// <summary>
        /// Gets or sets the token cleanup interval (in seconds). The default is 3600 (1 hour).
        /// </summary>
        /// <value>
        /// The token cleanup interval.
        /// </value>
        [JsonProperty("tokenCleanupInterval")]
        public int TokenCleanupInterval { get; set; } = 3600;

        /// <summary>
        /// Gets or sets the number of records to remove at a time. Defaults to 100.
        /// </summary>
        /// <value>
        /// The size of the token cleanup batch.
        /// </value>
        [JsonProperty("tokenCleanupBatchSize")]
        public int TokenCleanupBatchSize { get; set; } = 100;
    }
}
