// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.
// Based on work from Brock Allen & Dominick Baier, https://github.com/IdentityServer/IdentityServer4



using AutoMapper;
using System.Collections.Generic;
using Entities = ElCamino.IdentityServer4.AzureStorage.Entities;
using Models = IdentityServer4.Models;

namespace ElCamino.IdentityServer4.AzureStorage.Mappers
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
                    .ReverseMap();

                cfg.CreateMap<Entities.ApiResourceClaim, string>()
                    .ConstructUsing(x => x.Type)
                    .ReverseMap()
                    .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src));

                cfg.CreateMap<Entities.ApiSecret, Models.Secret>(MemberList.Destination)
                    .ForMember(dest => dest.Type, opt => opt.Condition(srs => srs != null))
                    .ReverseMap();

                cfg.CreateMap<Entities.ApiScope, Models.Scope>(MemberList.Destination)
                    .ConstructUsing(src => new Models.Scope())
                    .ReverseMap();

                cfg.CreateMap<Entities.ApiScopeClaim, string>()
                   .ConstructUsing(x => x.Type)
                   .ReverseMap()
                   .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src));
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