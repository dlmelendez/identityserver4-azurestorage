// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.
// Based on work from Brock Allen & Dominick Baier, https://github.com/IdentityServer/IdentityServer4

#pragma warning disable 1591

namespace ElCamino.IdentityServer4.AzureStorage.Entities
{
    public class ApiResourceScope
    {
        public int Id { get; set; }
        /// <summary>
        /// IdentityServer calls this Scope, but calling Name to eliminate data conversion.
        /// </summary>
        public string Name { get; set; }

        public int ApiResourceId { get; set; }
        public ApiResource ApiResource { get; set; }
    }
}