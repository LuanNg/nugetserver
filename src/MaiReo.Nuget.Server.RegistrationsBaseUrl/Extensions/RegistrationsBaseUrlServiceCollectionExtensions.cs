﻿using MaiReo.Nuget.Server;
using MaiReo.Nuget.Server.Middlewares;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class RegistrationsBaseUrlServiceCollectionExtensions
    {
        public static IServiceCollection
        AddNugetServerRegistrationsBaseUrl(
            this IServiceCollection services,
            string url = null)
        {
            services
            .AddNugetServerCore(
                opt => opt
                .Resources
                .Add(NugetServerResourceType.RegistrationsBaseUrl_3_4_0,
                url ?? "/registration3-gz"))
            .TryAddTransient<RegistrationsBaseUrlMiddleware>();

            return services;
        }
    }
}