
// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.
// Based on work from Brock Allen & Dominick Baier, https://github.com/IdentityServer/IdentityServer4




namespace ElCamino.IdentityServer4.AzureStorage.Entities
{
    public class ClientScope
    {
        public int Id { get; set; }
        public string Scope { get; set; }

        public int ClientId { get; set; }
        public Client Client { get; set; }
    }
}