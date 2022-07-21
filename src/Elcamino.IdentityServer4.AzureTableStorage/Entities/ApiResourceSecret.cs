// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.
// Based on work from Brock Allen & Dominick Baier, https://github.com/IdentityServer/Duende.IdentityServer


#pragma warning disable 1591

namespace ElCamino.Duende.IdentityServer.AzureStorage.Entities
{
    public class ApiResourceSecret : Secret
    {
        public int ApiResourceId { get; set; }
        public ApiResource ApiResource { get; set; }
    }
}