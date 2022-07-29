
// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.
// Based on work from Brock Allen & Dominick Baier, https://github.com/IdentityServer/Duende.IdentityServer




namespace ElCamino.IdentityServer.AzureStorage.Entities
{
    public class ApiResourceProperty : Property
    {
        public int ApiResourceId { get; set; }
        public ApiResource ApiResource { get; set; }
    }
}