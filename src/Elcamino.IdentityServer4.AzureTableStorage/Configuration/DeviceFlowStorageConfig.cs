// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ElCamino.Duende.IdentityServer.AzureStorage.Configuration
{
    [JsonObject("deviceFlowStorageConfig")]

    public class DeviceFlowStorageConfig
    {
        [JsonProperty("storageConnectionString")]
        public string StorageConnectionString { get; set; }

        [JsonProperty("blobUserContainerName")]
        public string BlobUserContainerName { get; set; } = "deviceflowusercodes";

        [JsonProperty("blobDeviceContainerName")]
        public string BlobDeviceContainerName { get; set; } = "deviceflowdevicecodes";


    }
}
