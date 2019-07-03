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
    /// Extension methods to map to/from entity/model for identity resources.
    /// </summary>
    public static class IdentityResourceMappers
    {
        static IdentityResourceMappers()
        {
            Mapper = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Entities.IdentityResourceProperty, KeyValuePair<string, string>>()
               .ReverseMap();

                cfg.CreateMap<Entities.IdentityResource, Models.IdentityResource>(MemberList.Destination)
                    .ConstructUsing(src => new Models.IdentityResource())
                    .ReverseMap();

                cfg.CreateMap<Entities.IdentityClaim, string>()
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
        public static Models.IdentityResource ToModel(this Entities.IdentityResource entity)
        {
            return entity == null ? null : Mapper.Map<Models.IdentityResource>(entity);
        }

        /// <summary>
        /// Maps a model to an entity.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns></returns>
        public static Entities.IdentityResource ToEntity(this Models.IdentityResource model)
        {
            return model == null ? null : Mapper.Map<Entities.IdentityResource>(model);
        }
    }
}