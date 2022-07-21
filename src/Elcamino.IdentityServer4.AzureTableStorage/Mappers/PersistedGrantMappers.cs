// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.
// Based on work from Brock Allen & Dominick Baier, https://github.com/IdentityServer/Duende.IdentityServer

using AutoMapper;
using ElCamino.Duende.IdentityServer.AzureStorage.Helpers;
using ElCamino.Duende.IdentityServer.AzureStorage.Entities;
using Duende.IdentityServer.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace ElCamino.Duende.IdentityServer.AzureStorage.Mappers
{
    public static class PersistedGrantMappers
    {
        private static readonly MapperConfiguration KeyMapperConfiguration;

        public static IMapper KeyMapper { get; private set; }

        private static readonly MapperConfiguration SubjectMapperConfiguration;

        public static IMapper SubjectMapper { get; private set; }

        static PersistedGrantMappers()
        {
            KeyMapperConfiguration = new MapperConfiguration((cfg) =>
            {
                cfg.CreateMap<PersistedGrant, PersistedGrantTblEntity>()                
                 .ForMember(dest => dest.RowKey,
                     opt => {
                         opt.MapFrom(src => KeyGeneratorHelper.GenerateHashValue(src.Key));
                     })
                .ForMember(dest => dest.PartitionKey,
                     opt => {
                         opt.MapFrom(src => KeyGeneratorHelper.GenerateHashValue(src.Key));
                     })                
                 .ForMember(dest => dest.Timestamp, opt => opt.Ignore())
                 .ForMember(dest => dest.ETag,
                     opt => {
                         opt.MapFrom(src => KeyGeneratorHelper.ETagWildCard);
                     }).ConstructUsing((n) => new PersistedGrantTblEntity());
                cfg.CreateMap<PersistedGrantTblEntity, PersistedGrant>()                                 
                 .ConstructUsing((n) => new PersistedGrant());
            });

            KeyMapperConfiguration.CompileMappings();

            KeyMapper = KeyMapperConfiguration.CreateMapper();
            KeyMapper.ConfigurationProvider.AssertConfigurationIsValid();

            SubjectMapperConfiguration = new MapperConfiguration((cfg) =>
            {
                cfg.CreateMap<PersistedGrant, PersistedGrantTblEntity>()
                 .ForMember(dest => dest.RowKey,
                     opt => {
                         opt.MapFrom(src => KeyGeneratorHelper.GenerateHashValue(src.Key));
                     })
                .ForMember(dest => dest.PartitionKey,
                     opt => {
                         opt.MapFrom(src => KeyGeneratorHelper.GenerateHashValue(src.SubjectId));
                     })
                 .ForMember(dest => dest.Timestamp, opt => opt.Ignore())
                 .ForMember(dest => dest.ETag,
                     opt => {
                         opt.MapFrom(src => KeyGeneratorHelper.ETagWildCard);
                     }).ConstructUsing((n) => new PersistedGrantTblEntity());
                cfg.CreateMap<PersistedGrantTblEntity, PersistedGrant>()
                 .ConstructUsing((n) => new PersistedGrant());
            });

            SubjectMapperConfiguration.CompileMappings();

            SubjectMapper = SubjectMapperConfiguration.CreateMapper();
            SubjectMapper.ConfigurationProvider.AssertConfigurationIsValid();
        }

        public static (PersistedGrantTblEntity keyGrant, PersistedGrantTblEntity subjectGrant) ToEntities(this PersistedGrant persistedGrant)
        {
            return (KeyMapper.Map<PersistedGrantTblEntity>(persistedGrant), SubjectMapper.Map<PersistedGrantTblEntity>(persistedGrant));
        }

        public static PersistedGrant ToModel(this PersistedGrantTblEntity entity)
        {
            return KeyMapper.Map<PersistedGrant>(entity);
        }
    }
}
