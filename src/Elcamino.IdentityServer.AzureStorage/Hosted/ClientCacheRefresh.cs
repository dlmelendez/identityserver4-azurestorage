// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.
// Based on work from Brock Allen & Dominick Baier, https://github.com/IdentityServer/Duende.IdentityServer


using ElCamino.IdentityServer.AzureStorage.Configuration;
using ElCamino.IdentityServer.AzureStorage.Contexts;
using ElCamino.IdentityServer.AzureStorage.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ElCamino.IdentityServer.AzureStorage.Hosted
{
    /// <summary>
    /// Helper to periodically refresh client cache
    /// </summary>
    public class ClientCacheRefresh
    {
        private readonly ILogger<ClientCacheRefresh> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly ClientStorageConfig _config;

        private CancellationTokenSource _source;

        private TimeSpan CleanupInterval => TimeSpan.FromSeconds(_config.CacheRefreshInterval);

        /// <summary>
        /// Constructor for cache refresh.
        /// </summary>
        /// <param name="serviceProvider"></param>
        /// <param name="logger"></param>
        /// <param name="options"></param>
        public ClientCacheRefresh(IServiceProvider serviceProvider, ILogger<ClientCacheRefresh> logger, IOptions<ClientStorageConfig> config)
        {
            _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
            if (_config.CacheRefreshInterval < 1) throw new ArgumentException("Cache refresh interval must be at least 1 second");

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <summary>
        /// Starts the cache refresh polling.
        /// </summary>
        public void Start()
        {
            Start(CancellationToken.None);
        }

        /// <summary>
        /// Starts the cache refresh polling.
        /// </summary>
        public void Start(CancellationToken cancellationToken)
        {
            if (_source != null) throw new InvalidOperationException("Already started. Call Stop first.");

            _logger.LogDebug("Starting Client cache refresh");

            _source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            Task.Factory.StartNew(() => StartInternalAsync(_source.Token));
        }

        /// <summary>
        /// Stops the cache refresh polling.
        /// </summary>
        public void Stop()
        {
            if (_source == null) throw new InvalidOperationException("Not started. Call Start first.");

            _logger.LogDebug("Stopping Client cache refresh");

            _source.Cancel();
            _source = null;
        }

        private async Task StartInternalAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogDebug("CancellationRequested. Exiting.");
                    break;
                }

                try
                {
                    await Task.Delay(CleanupInterval, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    _logger.LogDebug("TaskCanceledException. Exiting.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Task.Delay exception: {message}. Exiting.", ex.Message);
                    break;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogDebug("CancellationRequested. Exiting.");
                    break;
                }

                await RefreshCacheAsync();
            }
        }

        /// <summary>
        /// Method to refresh cache
        /// </summary>
        /// <returns></returns>
        public async Task RefreshCacheAsync()
        {
            try
            {
                _logger.LogTrace($"Querying for {nameof(ClientStorageContext)} cache refresh");
                ClientStorageContext context = _serviceProvider.CreateScope().ServiceProvider.GetService<ClientStorageContext>();
                await RefreshCacheAsync(context);
            }
            catch (Exception ex)
            {
                _logger.LogError("Exception refreshing client blob cache: {exception}", ex.Message);
            }
        }

        private async Task RefreshCacheAsync(ClientStorageContext context)
        {
            IAsyncEnumerable<Client> entities = context.GetAllBlobEntitiesAsync<Entities.Client>(context.ClientBlobContainer, _logger);
            (string blobName, int count) = await context.UpdateBlobCacheFileAsync<Entities.Client>(entities, context.ClientCacheBlobContainer);
            _logger.LogInformation("{RefreshCacheAsync} client count {count} saved in blob storage: {blobName}", nameof(RefreshCacheAsync), count, blobName);
            await context.DeleteBlobCacheFilesAsync(blobName, context.ClientCacheBlobContainer, _logger);
        }

    }
}
