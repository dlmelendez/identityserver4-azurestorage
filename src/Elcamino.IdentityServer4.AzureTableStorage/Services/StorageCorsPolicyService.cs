// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.
// Based on work from Brock Allen & Dominick Baier, https://github.com/IdentityServer/IdentityServer4

using ElCamino.IdentityServer4.AzureStorage.Stores;
using IdentityServer4.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ElCamino.IdentityServer4.AzureStorage.Helpers;
using ElCamino.IdentityServer4.AzureStorage.Interfaces;

namespace ElCamino.IdentityServer4.AzureStorage.Services
{
    public class StorageCorsPolicyService : ICorsPolicyService
    {
        /// <summary>
        /// Logger
        /// </summary>
        protected readonly ILogger Logger;
        /// <summary>
        /// Clients applications list
        /// </summary>
        protected IClientStorageStore _clientStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryCorsPolicyService"/> class.
        /// </summary>
        /// <param name="logger">The logger</param>
        /// <param name="clients">The clients.</param>
        public StorageCorsPolicyService(ILogger<StorageCorsPolicyService> logger, IClientStorageStore clientStore)
        {
            Logger = logger;
            _clientStore = clientStore;
        }

        /// <summary>
        /// Determines whether origin is allowed.
        /// </summary>
        /// <param name="origin">The origin.</param>
        /// <returns></returns>
        public virtual async Task<bool> IsOriginAllowedAsync(string origin)
        {
            var clients = await _clientStore.GetAllClients();
            var query =
                from client in clients
                from url in client.AllowedCorsOrigins
                select url.GetOrigin();

            var result = query.Contains(origin, StringComparer.OrdinalIgnoreCase);

            if (result)
            {
                Logger.LogDebug("Client list checked and origin: {0} is allowed", origin);
            }
            else
            {
                Logger.LogDebug("Client list checked and origin: {0} is not allowed", origin);
            }

            return result;
        }
    }
}