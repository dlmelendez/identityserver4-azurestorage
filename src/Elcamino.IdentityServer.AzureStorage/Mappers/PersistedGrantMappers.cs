// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.
// Based on work from Brock Allen & Dominick Baier, https://github.com/IdentityServer/Duende.IdentityServer

using ElCamino.IdentityServer.AzureStorage.Entities;
using ElCamino.IdentityServer.AzureStorage.Helpers;
using Models = Duende.IdentityServer.Models;

namespace ElCamino.IdentityServer.AzureStorage.Mappers
{
    public static class PersistedGrantMappers
    {
        public static (PersistedGrantTblEntity keyGrant, PersistedGrantTblEntity subjectGrant) ToEntities(this Models.PersistedGrant persistedGrant)
        {
            return (persistedGrant.ToEntityKey(), persistedGrant.ToEntitySubject());
        }

        /// <summary>
        /// Maps an entity to a model.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <returns></returns>
        public static Models.PersistedGrant ToModel(this PersistedGrantTblEntity entity) => entity == null ? null :
                new Models.PersistedGrant
                {
                    Key = entity.Key,
                    Type = entity.Type,
                    SubjectId = entity.SubjectId,
                    SessionId = entity.SessionId,
                    ClientId = entity.ClientId,
                    Description = entity.Description,
                    CreationTime = entity.CreationTime,
                    Expiration = entity.Expiration,
                    ConsumedTime = entity.ConsumedTime,
                    Data = entity.Data
                };

        /// <summary>
        /// Maps a model to a key entity.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns></returns>
        public static PersistedGrantTblEntity ToEntityKey(this Models.PersistedGrant model) => model == null ? null :
                new PersistedGrantTblEntity
                {
                    RowKey = KeyGeneratorHelper.GenerateHashValue(model.Key).ToString(),
                    PartitionKey = KeyGeneratorHelper.GenerateHashValue(model.Key).ToString(),
                    ETag = KeyGeneratorHelper.ETagWildCard,
                    Key = model.Key,
                    Type = model.Type,
                    SubjectId = model.SubjectId,
                    SessionId = model.SessionId,
                    ClientId = model.ClientId,
                    Description = model.Description,
                    CreationTime = model.CreationTime,
                    Expiration = model.Expiration,
                    ConsumedTime = model.ConsumedTime,
                    Data = model.Data
                };

        /// <summary>
        /// Maps a model to a subject entity.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns></returns>
        public static PersistedGrantTblEntity ToEntitySubject(this Models.PersistedGrant model) => model == null ? null :
                new PersistedGrantTblEntity
                {
                    RowKey = KeyGeneratorHelper.GenerateHashValue(model.Key).ToString(),
                    PartitionKey = KeyGeneratorHelper.GenerateHashValue(model.SubjectId).ToString(),
                    ETag = KeyGeneratorHelper.ETagWildCard,
                    Key = model.Key,
                    Type = model.Type,
                    SubjectId = model.SubjectId,
                    SessionId = model.SessionId,
                    ClientId = model.ClientId,
                    Description = model.Description,
                    CreationTime = model.CreationTime,
                    Expiration = model.Expiration,
                    ConsumedTime = model.ConsumedTime,
                    Data = model.Data
                };


    }
}
