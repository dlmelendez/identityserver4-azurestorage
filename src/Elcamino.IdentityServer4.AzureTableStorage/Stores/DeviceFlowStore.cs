// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.
// Based on work from Brock Allen & Dominick Baier, https://github.com/IdentityServer/IdentityServer4

using ElCamino.IdentityServer4.AzureStorage.Contexts;
using ElCamino.IdentityServer4.AzureStorage.Helpers;
using ElCamino.IdentityServer4.AzureStorage.Mappers;
using ElCamino.IdentityServer4.AzureStorage.Entities;
using IdentityServer4.Models;
using IdentityServer4.Stores;
using Azure.Storage.Blobs;
using Microsoft.Azure.Cosmos.Table;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using IdentityModel;
using IdentityServer4.Stores.Serialization;
using Newtonsoft.Json;

namespace ElCamino.IdentityServer4.AzureStorage.Stores
{
    /// <summary>
    /// Implementation of IDeviceFlowStore thats uses EF.
    /// </summary>
    /// <seealso cref="IdentityServer4.Stores.IDeviceFlowStore" />
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
            string entityJson = JsonConvert.SerializeObject(ToEntity(data, deviceCode, userCode));
            await Task.WhenAll(Context.SaveBlobWithHashedKeyAsync(deviceCode, entityJson, Context.DeviceCodeBlobContainer),
                               Context.SaveBlobWithHashedKeyAsync(userCode, entityJson, Context.UserCodeBlobContainer));
        }

        /// <summary>
        /// Finds device authorization by user code.
        /// </summary>
        /// <param name="userCode">The user code.</param>
        /// <returns></returns>
        public virtual async Task<DeviceCode> FindByUserCodeAsync(string userCode)
        {
            DeviceFlowCodes deviceFlowCodes = await Context.GetEntityBlobAsync<DeviceFlowCodes>(userCode, Context.UserCodeBlobContainer);
            DeviceCode model = ToModel(deviceFlowCodes?.Data);

            Logger.LogDebug("{userCode} found in blob storage: {userCodeFound}", userCode, model != null);

            return model;
        }

        /// <summary>
        /// Finds device authorization by device code.
        /// </summary>
        /// <param name="deviceCode">The device code.</param>
        /// <returns></returns>
        public virtual async Task<DeviceCode> FindByDeviceCodeAsync(string deviceCode)
        {
            DeviceFlowCodes deviceFlowCodes = await Context.GetEntityBlobAsync<DeviceFlowCodes>(deviceCode, Context.DeviceCodeBlobContainer);
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
        public virtual async Task UpdateByUserCodeAsync(string userCode, DeviceCode data)
        {
            DeviceFlowCodes existingUserCode = await Context.GetEntityBlobAsync<DeviceFlowCodes>(userCode, Context.UserCodeBlobContainer);
            if (existingUserCode == null)
            {
                Logger.LogError("{userCode} not found in blob storage", userCode);
                throw new InvalidOperationException("Could not update device code");
            }

            string deviceCode = existingUserCode.DeviceCode;
            DeviceFlowCodes existingDeviceCode = await Context.GetEntityBlobAsync<DeviceFlowCodes>(deviceCode, Context.DeviceCodeBlobContainer);
            if (existingDeviceCode == null)
            {
                Logger.LogError("{deviceCode} not found in blob storage", deviceCode);
                throw new InvalidOperationException("Could not update device code");
            }

            var entity = ToEntity(data, deviceCode, userCode);
            Logger.LogDebug("{userCode} found in blob storage", userCode);

            existingUserCode.SubjectId = data.Subject?.FindFirst(JwtClaimTypes.Subject).Value;
            existingUserCode.Data = entity.Data;

            string entityJson = JsonConvert.SerializeObject(existingUserCode);
            await Task.WhenAll(Context.SaveBlobWithHashedKeyAsync(deviceCode, entityJson, Context.DeviceCodeBlobContainer),
                               Context.SaveBlobWithHashedKeyAsync(userCode, entityJson, Context.UserCodeBlobContainer));

        }

        /// <summary>
        /// Removes the device authorization, searching by device code.
        /// </summary>
        /// <param name="deviceCode">The device code.</param>
        /// <returns></returns>
        public virtual async Task RemoveByDeviceCodeAsync(string deviceCode)
        {
            DeviceFlowCodes deviceFlowCodes = await Context.GetEntityBlobAsync<DeviceFlowCodes>(deviceCode, Context.DeviceCodeBlobContainer);

            if (deviceFlowCodes != null)
            {
                Logger.LogDebug("removing {deviceCode} device code from blob storage", deviceCode);

                await Task.WhenAll(Context.DeleteBlobAsync(deviceCode, Context.DeviceCodeBlobContainer),
                                   Context.DeleteBlobAsync(deviceFlowCodes.UserCode, Context.UserCodeBlobContainer));
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
