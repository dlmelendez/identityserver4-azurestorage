// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using ElCamino.IdentityServer.AzureStorage.Contexts;
using ElCamino.IdentityServer.AzureStorage.Helpers;
using Microsoft.Extensions.Logging;

namespace ElCamino.IdentityServer.AzureStorage.Stores;

/// <summary>
/// Implementation of ISigningKeyStore thats uses Azure Storage.
/// </summary>
/// <seealso cref="ISigningKeyStore" />
public class SigningKeyStore : ISigningKeyStore
{
    const string Use = "signing";

    /// <summary>
    /// The StorageContext.
    /// </summary>
    protected readonly SigningKeyStorageContext StorageContext;

    /// <summary>
    /// The logger.
    /// </summary>
    protected readonly ILogger<SigningKeyStore> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SigningKeyStore"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="cancellationTokenProvider"></param>
    /// <exception cref="ArgumentNullException">context</exception>
    public SigningKeyStore(SigningKeyStorageContext context, ILogger<SigningKeyStore> logger)
    {
        StorageContext = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger;
    }

    /// <summary>
    /// Loads all keys from store.
    /// </summary>
    /// <returns></returns>
    public async Task<IEnumerable<SerializedKey>> LoadKeysAsync()
    {
        var entities = await GetAllSigningKeyEntities().ConfigureAwait(false);

        return entities.Select(key => new SerializedKey
        {
            Id = key.Id,
            Created = key.Created,
            Version = key.Version,
            Algorithm = key.Algorithm,
            Data = key.Data,
            DataProtected = key.DataProtected,
            IsX509Certificate = key.IsX509Certificate
        }).ToArray();
       
    }

    /// <summary>
    /// Persists new key in store.
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    public async Task StoreKeyAsync(SerializedKey model)
    {
        Entities.Key entity = new()
        {
            Id = model.Id,
            Use = Use,
            Created = model.Created,
            Version = model.Version,
            Algorithm = model.Algorithm,
            Data = model.Data,
            DataProtected = model.DataProtected,
            IsX509Certificate = model.IsX509Certificate
        };
        try
        {
            await StorageContext.SaveBlobWithHashedKeyAsync(entity.Id, JsonSerializer.Serialize(entity, StorageContext.JsonSerializerDefaultOptions), StorageContext.SigningKeyBlobContainer)
                .ConfigureAwait(false);
        }
        catch (AggregateException agg)
        {
            _logger.LogError(agg, agg.Message);
            ExceptionHelper.LogStorageExceptions(agg, storageLogger: (rfex) =>
            {
                _logger.LogStorageError(rfex);
                _logger.LogWarning("exception updating {keyId} key in blob storage: {error}", model?.Id, rfex.Message);
            });
            throw;
        }
        catch (RequestFailedException rfex)
        {
            _logger.LogStorageError(rfex);
            _logger.LogWarning("exception updating {keyId} key in blob storage: {error}", model?.Id, rfex.Message);
            throw;
        }
    }

    /// <summary>
    /// Deletes key from storage.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public async Task DeleteKeyAsync(string keyId)
    {
        try
        {
            await StorageContext.DeleteBlobAsync(keyId, StorageContext.SigningKeyBlobContainer)
                .ConfigureAwait(false);
        }
        catch (AggregateException agg)
        {
            _logger.LogError(agg, agg.Message);
            ExceptionHelper.LogStorageExceptions(agg, (rfex) =>
            {
                _logger.LogStorageError(rfex);
                _logger.LogWarning("exception updating {keyId} key in  storage: {error}", keyId, rfex.Message);
            });
            throw;
        }
        catch (RequestFailedException rfex)
        {
            _logger.LogStorageError(rfex);
            _logger.LogWarning("exception updating {keyId} key in  storage: {error}", keyId, rfex.Message);
            throw;
        }
    }

    private async Task<IEnumerable<Entities.Key>> GetAllSigningKeyEntities()
    {
        return await StorageContext.GetAllBlobEntitiesAsync<Entities.Key>(StorageContext.SigningKeyBlobContainer, _logger)
                .ToListAsync()
                .ConfigureAwait(false);
    }

}
