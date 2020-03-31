// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.
// Based on work from Brock Allen & Dominick Baier, https://github.com/IdentityServer/IdentityServer4


using ElCamino.IdentityServer4.AzureStorage.Configuration;
using ElCamino.IdentityServer4.AzureStorage.Contexts;
using ElCamino.IdentityServer4.AzureStorage.Entities;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ElCamino.IdentityServer4.AzureStorage.Hosted
{
    /// <summary>
    /// Helper to periodically refresh client cache
    /// </summary>
    public class ResourceCacheRefresh
    {
        private readonly ILogger<ResourceCacheRefresh> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly ResourceStorageConfig _config;

        private CancellationTokenSource _source;

        private TimeSpan CleanupInterval => TimeSpan.FromSeconds(_config.CacheRefreshInterval);

        /// <summary>
        /// Constructor for cache refresh.
        /// </summary>
        /// <param name="serviceProvider"></param>
        /// <param name="logger"></param>
        /// <param name="options"></param>
        public ResourceCacheRefresh(IServiceProvider serviceProvider, ILogger<ResourceCacheRefresh> logger, IOptions<ResourceStorageConfig> config)
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

            _logger.LogDebug("Starting Resource cache refresh");

            _source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            Task.Factory.StartNew(() => StartInternalAsync(_source.Token));
        }

        /// <summary>
        /// Stops the cache refresh polling.
        /// </summary>
        public void Stop()
        {
            if (_source == null) throw new InvalidOperationException("Not started. Call Start first.");

            _logger.LogDebug("Stopping Resource cache refresh");

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
                    _logger.LogError("Task.Delay exception: {0}. Exiting.", ex.Message);
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
                _logger.LogTrace($"Querying for {nameof(ResourceStorageContext)} cache refresh");
                var context = _serviceProvider.CreateScope().ServiceProvider.GetService<ResourceStorageContext>();
                await Task.WhenAll(RefreshApiCacheAsync(context), RefreshIdentityCacheAsync(context));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception refreshing resource blob cache: {exception}", ex.Message);
            }
        }

        private async Task RefreshApiCacheAsync(ResourceStorageContext context)
        {
            var apiEntities = await context.GetAllBlobEntitiesAsync<Entities.ApiResource>(context.ApiResourceBlobContainer, _logger);
            string blobName = await context.UpdateBlobCacheFileAsync<Entities.ApiResource>(apiEntities, context.ApiResourceBlobCacheContainer);
            _logger.LogInformation($"{nameof(RefreshCacheAsync)} api resource count {apiEntities.Count()} saved in blob storage: {blobName}");
            await context.DeleteBlobCacheFilesAsync(blobName, context.ApiResourceBlobCacheContainer, _logger);
        }

        private async Task RefreshIdentityCacheAsync(ResourceStorageContext context)
        {
            var identityEntities = await context.GetAllBlobEntitiesAsync<Entities.IdentityResource>(context.IdentityResourceBlobContainer, _logger);
            string blobName = await context.UpdateBlobCacheFileAsync<Entities.IdentityResource>(identityEntities, context.IdentityResourceBlobCacheContainer);
            _logger.LogInformation($"{nameof(RefreshCacheAsync)} identity resource count {identityEntities.Count()} saved in blob storage: {blobName}");
            await context.DeleteBlobCacheFilesAsync(blobName, context.IdentityResourceBlobCacheContainer, _logger);
        }

    }
}