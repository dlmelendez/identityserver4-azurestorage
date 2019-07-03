// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using IdentityServer4.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ElCamino.IdentityServer4.AzureStorage.Interfaces
{
    public interface IClientStorageStore
    {
        Task<IEnumerable<Client>> GetAllClients();
    }
}
