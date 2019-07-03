// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.
// Based on work from Brock Allen & Dominick Baier, https://github.com/IdentityServer/IdentityServer4



#pragma warning disable 1591

using System;

namespace ElCamino.IdentityServer4.AzureStorage.Entities
{
    public abstract class Secret
    {
        public int Id { get; set; }
        public string Description { get; set; }
        public string Value { get; set; }
        public DateTime? Expiration { get; set; }
        public string Type { get; set; } = "SharedSecret";
        public DateTime Created { get; set; } = DateTime.UtcNow;
    }
}