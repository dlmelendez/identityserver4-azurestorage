// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;

namespace ElCamino.Duende.IdentityServer.AzureStorage.Configuration
{
    public class SigningKeyStorageConfig
    {
        public string StorageConnectionString { get; set; }

        public string BlobContainerName { get; set; }    

    }
}
