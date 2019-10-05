// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ElCamino.IdentityServer4.AzureStorage.Configuration
{
    [JsonObject("resourceStorageConfig")]
    public class ResourceStorageConfig
    {
        [JsonProperty("apiTableName")]
        public string ApiTableName { get; set; }

        [JsonProperty("storageConnectionString")]
        public string StorageConnectionString { get; set; }

        [JsonProperty("apiBlobContainerName")]
        public string ApiBlobContainerName { get; set; }

        [JsonProperty("identityBlobContainerName")]
        public string IdentityBlobContainerName { get; set; }

        [JsonProperty("apiBlobCacheContainerName")]
        public string ApiBlobCacheContainerName { get; set; }

        [JsonProperty("identityBlobContainerName")]
        public string IdentityBlobCacheContainerName { get; set; }
    }
}
