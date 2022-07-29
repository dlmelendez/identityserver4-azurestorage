// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.
// Based on work from Brock Allen & Dominick Baier, https://github.com/IdentityServer/Duende.IdentityServer


using ElCamino.IdentityServer.AzureStorage.Configuration;
using ElCamino.IdentityServer.AzureStorage.Hosted;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DependencyInjection
{
    internal class ClientCacheRefreshHost : IHostedService
    {
        private readonly ClientCacheRefresh _cacheRefresh;
        private readonly ClientStorageConfig _config;

        public ClientCacheRefreshHost(ClientCacheRefresh cacheRefresh, IOptions<ClientStorageConfig> config)
        {
            _cacheRefresh = cacheRefresh;
            _config = config.Value;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (_config.EnableCacheRefresh)
            {
                _cacheRefresh.Start(cancellationToken);
            }
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            if (_config.EnableCacheRefresh)
            {
                _cacheRefresh.Stop();
            }
            return Task.CompletedTask;
        }
    }
}
