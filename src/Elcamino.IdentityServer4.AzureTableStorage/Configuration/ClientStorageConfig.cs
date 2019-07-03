// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ElCamino.IdentityServer4.AzureStorage.Configuration
{
    [JsonObject("clientStorageConfig")]
    public class ClientStorageConfig
    {
        [JsonProperty("storageConnectionString")]
        public string StorageConnectionString { get; set; }

        [JsonProperty("blobContainerName")]
        public string BlobContainerName { get; set; }
    }
}
