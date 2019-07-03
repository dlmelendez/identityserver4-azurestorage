// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.
// Based on work from Brock Allen & Dominick Baier, https://github.com/IdentityServer/IdentityServer4

#pragma warning disable 1591

using System;
using System.Collections.Generic;

namespace ElCamino.IdentityServer4.AzureStorage.Entities
{
    public class ApiResource
    {
        public int Id { get; set; }
        public bool Enabled { get; set; } = true;
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public List<ApiSecret> Secrets { get; set; } = new List<ApiSecret>();
        public List<ApiScope> Scopes { get; set; } = new List<ApiScope>();
        public List<ApiResourceClaim> UserClaims { get; set; } = new List<ApiResourceClaim>();
        public List<ApiResourceProperty> Properties { get; set; } = new List<ApiResourceProperty>();
        public DateTime Created { get; set; } = DateTime.UtcNow;
        public DateTime? Updated { get; set; }
        public DateTime? LastAccessed { get; set; }
        public bool NonEditable { get; set; }
    }
}
