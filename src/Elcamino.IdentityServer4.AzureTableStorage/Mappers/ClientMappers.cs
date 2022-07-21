// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.
// Based on work from Brock Allen & Dominick Baier, https://github.com/IdentityServer/Duende.IdentityServer

using AutoMapper;
using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Claims;
using Models = Duende.IdentityServer.Models;

namespace ElCamino.Duende.IdentityServer.AzureStorage.Mappers
{
    public static class ClientMappers
    {
        static ClientMappers()
        {
            Mapper = new MapperConfiguration(cfg => {
                cfg.CreateMap<Entities.ClientProperty, KeyValuePair<string, string>>()
               .ReverseMap();

                cfg.CreateMap<Entities.Client, Models.Client>()
                    .ForMember(dest => dest.ProtocolType, opt => opt.Condition(srs => srs != null))
                    .ReverseMap();

                cfg.CreateMap<Entities.ClientCorsOrigin, string>()
                    .ConstructUsing(src => src.Origin)
                    .ReverseMap()
                    .ForMember(dest => dest.Origin, opt => opt.MapFrom(src => src));

                cfg.CreateMap<Entities.ClientIdPRestriction, string>()
                    .ConstructUsing(src => src.Provider)
                    .ReverseMap()
                    .ForMember(dest => dest.Provider, opt => opt.MapFrom(src => src));

                cfg.CreateMap<Entities.ClientClaim, Claim>(MemberList.None)
                    .ConstructUsing(src => new Claim(src.Type, src.Value))
                    .ReverseMap();

                cfg.CreateMap<Entities.ClientScope, string>()
                    .ConstructUsing(src => src.Scope)
                    .ReverseMap()
                    .ForMember(dest => dest.Scope, opt => opt.MapFrom(src => src));

                cfg.CreateMap<Entities.ClientPostLogoutRedirectUri, string>()
                    .ConstructUsing(src => src.PostLogoutRedirectUri)
                    .ReverseMap()
                    .ForMember(dest => dest.PostLogoutRedirectUri, opt => opt.MapFrom(src => src));

                cfg.CreateMap<Entities.ClientRedirectUri, string>()
                    .ConstructUsing(src => src.RedirectUri)
                    .ReverseMap()
                    .ForMember(dest => dest.RedirectUri, opt => opt.MapFrom(src => src));

                cfg.CreateMap<Entities.ClientGrantType, string>()
                    .ConstructUsing(src => src.GrantType)
                    .ReverseMap()
                    .ForMember(dest => dest.GrantType, opt => opt.MapFrom(src => src));

                cfg.CreateMap<Entities.ClientSecret, Models.Secret>(MemberList.Destination)
                    .ForMember(dest => dest.Type, opt => opt.Condition(srs => srs != null))
                    .ReverseMap();
            }
            )
                .CreateMapper();
        }

        internal static IMapper Mapper { get; }

        /// <summary>
        /// Maps an entity to a model.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <returns></returns>
        public static Models.Client ToModel(this Entities.Client entity)
        {
            return Mapper.Map<Models.Client>(entity);
        }

        /// <summary>
        /// Maps a model to an entity.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns></returns>
        public static Entities.Client ToEntity(this Models.Client model)
        {
            return Mapper.Map<Entities.Client>(model);
        }
    }
}
