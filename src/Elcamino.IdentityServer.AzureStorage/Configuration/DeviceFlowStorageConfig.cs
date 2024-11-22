// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace ElCamino.IdentityServer.AzureStorage.Configuration
{

    public class DeviceFlowStorageConfig
    {
        public string BlobUserContainerName { get; set; } = "deviceflowusercodes";

        public string BlobDeviceContainerName { get; set; } = "deviceflowdevicecodes";


    }
}
