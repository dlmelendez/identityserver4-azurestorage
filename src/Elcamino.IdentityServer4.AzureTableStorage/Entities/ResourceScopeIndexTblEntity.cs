// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using Microsoft.Azure.Cosmos.Table;
using System;
using System.Collections.Generic;
using System.Text;

namespace ElCamino.IdentityServer4.AzureStorage.Entities
{
    public class ResourceScopeIndexTblEntity : TableEntity
    {
        public string ResourceName { get; set; }
        
        public string ScopeName { get; set; }
    }
}
