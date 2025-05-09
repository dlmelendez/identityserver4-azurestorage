﻿// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.
// Based on work from Brock Allen & Dominick Baier, https://github.com/IdentityServer/Duende.IdentityServer

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Serialization;
using ElCamino.IdentityServer.AzureStorage.Contexts;
using ElCamino.IdentityServer.AzureStorage.Entities;
using Microsoft.Extensions.Logging;

namespace ElCamino.IdentityServer.AzureStorage.Stores
{
    /// <summary>
    /// Implementation of IDeviceFlowStore thats uses Azure Storage.
    /// </summary>
    /// <seealso cref="Duende.IdentityServer.Stores.IDeviceFlowStore" />
    public class DeviceFlowStore : IDeviceFlowStore
    {
        /// <summary>
        /// The Storage Context.
        /// </summary>
        protected readonly DeviceFlowStorageContext Context;

        /// <summary>
        ///  The serializer.
        /// </summary>
        protected readonly IPersistentGrantSerializer Serializer;

        /// <summary>
        /// The logger.
        /// </summary>
        protected readonly ILogger Logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceFlowStore"/> class.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="serializer">The serializer</param>
        /// <param name="logger">The logger.</param>
        public DeviceFlowStore(
            DeviceFlowStorageContext context,
            IPersistentGrantSerializer serializer,
            ILogger<DeviceFlowStore> logger)
        {
            Context = context;
            Serializer = serializer;
            Logger = logger;
        }

        /// <summary>
        /// Stores the device authorization request.
        /// Saves 2 blobs, by the userCode and deviceCode
        /// </summary>
        /// <param name="deviceCode">The device code.</param>
        /// <param name="userCode">The user code.</param>
        /// <param name="data">The data.</param>
        /// <returns></returns>
        public virtual async Task StoreDeviceAuthorizationAsync(string deviceCode, string userCode, DeviceCode data)
        {
            string entityJson = JsonSerializer.Serialize(ToEntity(data, deviceCode, userCode), Context.JsonSerializerDefaultOptions);
            await Task.WhenAll(Context.SaveBlobWithHashedKeyAsync(deviceCode, entityJson, Context.DeviceCodeBlobContainer),
                               Context.SaveBlobWithHashedKeyAsync(userCode, entityJson, Context.UserCodeBlobContainer))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Finds device authorization by user code.
        /// </summary>
        /// <param name="userCode">The user code.</param>
        /// <returns></returns>
        public virtual Task<DeviceCode> FindByUserCodeAsync(string userCode) 
        {
            return FindByUserCodeAsync(userCode, default);
        }

        public virtual async Task<DeviceCode> FindByUserCodeAsync(string userCode, CancellationToken cancellationToken = default)
        {
            DeviceFlowCodes deviceFlowCodes = await Context.GetEntityBlobAsync<DeviceFlowCodes>(userCode, Context.UserCodeBlobContainer, cancellationToken)
                .ConfigureAwait(false);
            DeviceCode model = ToModel(deviceFlowCodes?.Data);

            Logger.LogDebug("{userCode} found in blob storage: {userCodeFound}", userCode, model != null);

            return model;
        }

        /// <summary>
        /// Finds device authorization by device code.
        /// </summary>
        /// <param name="deviceCode">The device code.</param>
        /// <returns></returns>
        public virtual Task<DeviceCode> FindByDeviceCodeAsync(string deviceCode)
        {
            return FindByDeviceCodeAsync(deviceCode, default);
        }

        public virtual async Task<DeviceCode> FindByDeviceCodeAsync(string deviceCode, CancellationToken cancellationToken = default)
        {
            DeviceFlowCodes deviceFlowCodes = await Context.GetEntityBlobAsync<DeviceFlowCodes>(deviceCode, Context.DeviceCodeBlobContainer, cancellationToken)
                .ConfigureAwait(false);
            DeviceCode model = ToModel(deviceFlowCodes?.Data);

            Logger.LogDebug("{deviceCode} found in blob storage: {deviceCodeFound}", deviceCode, model != null);

            return model;
        }

        /// <summary>
        /// Updates device authorization, searching by user code.
        /// </summary>
        /// <param name="userCode">The user code.</param>
        /// <param name="data">The data.</param>
        /// <returns></returns>
        public virtual Task UpdateByUserCodeAsync(string userCode, DeviceCode data)
        {
            return UpdateByUserCodeAsync(userCode, data, default);
        }

        public virtual async Task UpdateByUserCodeAsync(string userCode, DeviceCode data, CancellationToken cancellationToken = default)
        {
            DeviceFlowCodes existingUserCode = await Context.GetEntityBlobAsync<DeviceFlowCodes>(userCode, Context.UserCodeBlobContainer, cancellationToken)
                .ConfigureAwait(false);
            if (existingUserCode == null)
            {
                Logger.LogError("{userCode} not found in blob storage", userCode);
                throw new InvalidOperationException("Could not update device code");
            }

            string deviceCode = existingUserCode.DeviceCode;
            DeviceFlowCodes existingDeviceCode = await Context.GetEntityBlobAsync<DeviceFlowCodes>(deviceCode, Context.DeviceCodeBlobContainer, cancellationToken)
                .ConfigureAwait(false);
            if (existingDeviceCode == null)
            {
                Logger.LogError("{deviceCode} not found in blob storage", deviceCode);
                throw new InvalidOperationException("Could not update device code");
            }

            var entity = ToEntity(data, deviceCode, userCode);
            Logger.LogDebug("{userCode} found in blob storage", userCode);

            existingUserCode.SubjectId = data.Subject?.FindFirst(JwtClaimTypes.Subject).Value;
            existingUserCode.Data = entity.Data;

            string entityJson = JsonSerializer.Serialize(existingUserCode, Context.JsonSerializerDefaultOptions);
            await Task.WhenAll(Context.SaveBlobWithHashedKeyAsync(deviceCode, entityJson, Context.DeviceCodeBlobContainer, cancellationToken),
                               Context.SaveBlobWithHashedKeyAsync(userCode, entityJson, Context.UserCodeBlobContainer, cancellationToken))
                .ConfigureAwait(false);

        }

        /// <summary>
        /// Removes the device authorization, searching by device code.
        /// </summary>
        /// <param name="deviceCode">The device code.</param>
        /// <returns></returns>
        public virtual Task RemoveByDeviceCodeAsync(string deviceCode)
        {
            return RemoveByDeviceCodeAsync(deviceCode, default);
        }

        public virtual async Task RemoveByDeviceCodeAsync(string deviceCode, CancellationToken cancellationToken = default)
        {
            DeviceFlowCodes deviceFlowCodes = await Context.GetEntityBlobAsync<DeviceFlowCodes>(deviceCode, Context.DeviceCodeBlobContainer, cancellationToken)
                .ConfigureAwait(false);

            if (deviceFlowCodes != null)
            {
                Logger.LogDebug("removing {deviceCode} device code from blob storage", deviceCode);

                await Task.WhenAll(Context.DeleteBlobAsync(deviceCode, Context.DeviceCodeBlobContainer, cancellationToken),
                                   Context.DeleteBlobAsync(deviceFlowCodes.UserCode, Context.UserCodeBlobContainer, cancellationToken))
                    .ConfigureAwait(false);
            }
            else
            {
                Logger.LogDebug("no {deviceCode} device code found in blob storage", deviceCode);
            }

        }

        /// <summary>
        /// Converts a model to an entity.
        /// </summary>
        /// <param name="model"></param>
        /// <param name="deviceCode"></param>
        /// <param name="userCode"></param>
        /// <returns></returns>
        protected DeviceFlowCodes ToEntity(DeviceCode model, string deviceCode, string userCode)
        {
            if (model == null || deviceCode == null || userCode == null) return null;

            return new DeviceFlowCodes
            {
                DeviceCode = deviceCode,
                UserCode = userCode,
                ClientId = model.ClientId,
                SubjectId = model.Subject?.FindFirst(JwtClaimTypes.Subject).Value,
                CreationTime = model.CreationTime,
                Expiration = model.CreationTime.AddSeconds(model.Lifetime),
                Data = Serializer.Serialize(model)
            };
        }

        /// <summary>
        /// Converts a serialized DeviceCode to a model.
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        protected DeviceCode ToModel(string entity)
        {
            if (entity == null) return null;

            return Serializer.Deserialize<DeviceCode>(entity);
        }
    }
}
