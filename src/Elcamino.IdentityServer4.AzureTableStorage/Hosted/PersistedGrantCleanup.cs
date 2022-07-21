// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.
// Based on work from Brock Allen & Dominick Baier, https://github.com/IdentityServer/Duende.IdentityServer


using ElCamino.Duende.IdentityServer.AzureStorage.Configuration;
using ElCamino.Duende.IdentityServer.AzureStorage.Contexts;
using ElCamino.Duende.IdentityServer.AzureStorage.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ElCamino.Duende.IdentityServer.AzureStorage.Hosted
{
    /// <summary>
    /// Helper to periodically cleanup expired persisted grants.
    /// </summary>
    public class PersistedGrantCleanup
    {
        private readonly ILogger<PersistedGrantCleanup> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly PersistedGrantStorageConfig _config;

        private CancellationTokenSource _source;

        private TimeSpan CleanupInterval => TimeSpan.FromSeconds(_config.TokenCleanupInterval);

        /// <summary>
        /// Constructor for TokenCleanup.
        /// </summary>
        /// <param name="serviceProvider"></param>
        /// <param name="logger"></param>
        /// <param name="options"></param>
        public PersistedGrantCleanup(IServiceProvider serviceProvider, ILogger<PersistedGrantCleanup> logger, IOptions<PersistedGrantStorageConfig> config)
        {
            _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
            if (_config.TokenCleanupInterval < 1) throw new ArgumentException("Token cleanup interval must be at least 1 second");
            if (_config.TokenCleanupBatchSize < 1) throw new ArgumentException("Token cleanup batch size interval must be at least 1");

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <summary>
        /// Starts the token cleanup polling.
        /// </summary>
        public void Start()
        {
            Start(CancellationToken.None);
        }

        /// <summary>
        /// Starts the token cleanup polling.
        /// </summary>
        public void Start(CancellationToken cancellationToken)
        {
            if (_source != null) throw new InvalidOperationException("Already started. Call Stop first.");

            _logger.LogDebug("Starting grant removal");

            _source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            Task.Factory.StartNew(() => StartInternalAsync(_source.Token));
        }

        /// <summary>
        /// Stops the token cleanup polling.
        /// </summary>
        public void Stop()
        {
            if (_source == null) throw new InvalidOperationException("Not started. Call Start first.");

            _logger.LogDebug("Stopping grant removal");

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

                await RemoveExpiredGrantsAsync();
            }
        }

        /// <summary>
        /// Method to clear expired persisted grants.
        /// </summary>
        /// <returns></returns>
        public async Task RemoveExpiredGrantsAsync()
        {
            try
            {
                _logger.LogTrace("Querying for expired grants to remove");
                var context = _serviceProvider.CreateScope().ServiceProvider.GetService<PersistedGrantStorageContext>();
                await RemoveGrants(context);
            }
            catch (Exception ex)
            {
                _logger.LogError("Exception removing expired grants: {exception}", ex.Message);
            }
        }

        private async Task RemoveGrants(PersistedGrantStorageContext context)
        {
            var found = Int32.MaxValue;

            while (found >= _config.TokenCleanupBatchSize)
            {
                var expiredGrants = await context.GetExpiredAsync(_config.TokenCleanupBatchSize);
               
                found = expiredGrants.Count();
                _logger.LogInformation("Removing {grantCount} grants", found);

                if (found > 0)
                {
                    foreach (var expiredGrant in expiredGrants)
                    {
                        try
                        {
                            await context.RemoveAsync(expiredGrant.Key);
                        }
                        catch (Exception ex)
                        {
                            // we get this if/when someone else already deleted the records
                            // we want to essentially ignore this, and keep working
                            _logger.LogDebug("Exception removing expired grants: {exception}", ex.Message);
                        }
                    }
                }
                else
                {
                    _logger.LogDebug("No expired grants found.");
                }
            }
        }

    }
}