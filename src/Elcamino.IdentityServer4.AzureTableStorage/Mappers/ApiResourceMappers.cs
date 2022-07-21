﻿// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.
// Based on work from Brock Allen & Dominick Baier, https://github.com/IdentityServer/Duende.IdentityServer



using AutoMapper;
using ElCamino.Duende.IdentityServer.AzureStorage.Entities;
using System.Collections.Generic;
using System.Linq;
using Entities = ElCamino.Duende.IdentityServer.AzureStorage.Entities;
using Models = Duende.IdentityServer.Models;

namespace ElCamino.Duende.IdentityServer.AzureStorage.Mappers
{
    /// <summary>
    /// Extension methods to map to/from entity/model for API resources.
    /// </summary>
    public static class ApiResourceMappers
    {
        static ApiResourceMappers()
        {
            Mapper = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Entities.ApiResourceProperty, KeyValuePair<string, string>>()
                .ReverseMap();

                cfg.CreateMap<Entities.ApiResource, Models.ApiResource>(MemberList.Destination)
                    .ConstructUsing(src => new Models.ApiResource())
                    .ForMember(x => x.ApiSecrets, opts => opts.MapFrom(x => x.Secrets))
                    .ForMember(x => x.Scopes, opts => opts.MapFrom(x => x.Scopes.Select(m => m.Name).ToList()))
                    .ForMember(x => x.AllowedAccessTokenSigningAlgorithms, opts => opts.ConvertUsing(AllowedSigningAlgorithmsConverter.Converter, x => x.AllowedAccessTokenSigningAlgorithms))
                    .ReverseMap()
                    .ForMember(x => x.AllowedAccessTokenSigningAlgorithms, opts => opts.ConvertUsing(AllowedSigningAlgorithmsConverter.Converter, x => x.AllowedAccessTokenSigningAlgorithms))
                    .ForMember(x => x.Scopes, opts => opts.MapFrom(x => x.Scopes.Select(m => new ApiResourceScope() { Name = m }).ToList()));

                cfg.CreateMap<Entities.ApiResourceClaim, string>()
                    .ConstructUsing(x => x.Type)
                    .ReverseMap()
                    .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src));

                cfg.CreateMap<Entities.ApiResourceSecret, Models.Secret>(MemberList.Destination)
                    .ForMember(dest => dest.Type, opt => opt.Condition(srs => srs != null))
                    .ReverseMap();

                cfg.CreateMap<Entities.ApiResourceScope, string>()
                    .ConstructUsing(x => x.Name)
                    .ReverseMap()
                    .ForMember(dest => dest.Name, opt => opt.MapFrom(src => new Entities.ApiResourceScope() { Name = src }));
            })
                .CreateMapper();
        }

        internal static IMapper Mapper { get; }

        /// <summary>
        /// Maps an entity to a model.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <returns></returns>
        public static Models.ApiResource ToModel(this Entities.ApiResource entity)
        {
            return entity == null ? null : Mapper.Map<Models.ApiResource>(entity);
        }

        /// <summary>
        /// Maps a model to an entity.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns></returns>
        public static Entities.ApiResource ToEntity(this Models.ApiResource model)
        {
            return model == null ? null : Mapper.Map<Entities.ApiResource>(model);
        }
    }
}