﻿using MaiReo.Nuget.Server.Core;
using MaiReo.Nuget.Server.Models;
using MaiReo.Nuget.Server.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace MaiReo.Nuget.Server.Extensions
{
    [EditorBrowsable( EditorBrowsableState.Never )]
    public static class PackagePublishNugetServerProviderExtensions
    {
        public static async Task RespondPackagePublishAsync(
            this INugetServerProvider provider,
            HttpContext context )
        {
            var req = provider.GetRequestingModel( context );
            if (!req.IsValid())
            {
                provider.RespondNotFound( context );
                return;
            }
            if (!provider.IsValidApiKey( req ))
            {
                provider.RespondNotFound( context );
                return;
            }
            if (req.IsPush())
            {
                await provider
                    .RespondPutPackageAsync( context, req );
                return;
            }
            if (req.IsDelete())
            {
                await provider
                    .RespondDeletePackageAsync( context, req );
                return;
            }
            provider.RespondNotFound( context );
        }

        private static async Task
        RespondPutPackageAsync(
           this INugetServerProvider provider,
           HttpContext context,
           PackageInputModel request )
        {

            if (!context.Request.HasFormContentType)
            {
                provider.RespondWithStatusCode(
                    context, HttpStatusCode.BadRequest );
                return;
            }


            try
            {
                var form = await context.Request.ReadFormAsync( context.RequestAborted );
                var formFile = form?.Files?.FirstOrDefault();
                if (formFile == null)
                {
                    provider.RespondWithStatusCode(
                    context, HttpStatusCode.BadRequest );
                    return;
                }

                using (var formFileStream = formFile.OpenReadStream())
                using (var inputStream = new MemoryStream())
                {
                    await formFileStream
                        .CopyToAsync( inputStream );
                    inputStream.Seek( 0, SeekOrigin.Begin );

                    var nuspec = await
                        Zip.ReadNuspecFromPackageAsync(
                        inputStream );
                    if (nuspec?.Metadata == null)
                    {
                        provider.RespondWithStatusCode(
                            context, HttpStatusCode.BadRequest );
                        return;
                    }
                    var packages =
                        provider
                        .NuspecProvider
                        .GetAll()
                        .Where( n => n.Metadata != null )
                        .Where( n => string.Equals(
                             n.Metadata.Id, nuspec.Metadata.Id,
                             StringComparison
                             .InvariantCultureIgnoreCase ) )
                        .Where( n =>
                            (NuGetVersionString)n.Metadata.Version
                            == nuspec.Metadata.Version )
                            .ToList();
                    if (packages.Count != 0)
                    {
                        provider.RespondWithStatusCode(
                            context, HttpStatusCode.Conflict );
                        return;
                    }

                    inputStream.Seek( 0, SeekOrigin.Begin );
                    var isAdded = await provider.NupkgProvider.AddAsync(
                        nuspec.Metadata,
                        inputStream, context.RequestAborted );
                    if (isAdded)
                    {
                        provider.RespondWithStatusCode(
                            context, HttpStatusCode.Created );
                        return;
                    }
                }

            }
            catch
            {
            }

            provider.RespondWithStatusCode(
                context,
                HttpStatusCode.InternalServerError );
        }

        private static async Task
        RespondDeletePackageAsync(
           this INugetServerProvider provider,
           HttpContext context,
           PackageInputModel request )
        {
            var packages =
            provider
                .NuspecProvider
                .GetAll()
                .Where( n => n.Metadata != null )
                .Where( n => string.Equals(
                     n.Metadata.Id, request.Id,
                     StringComparison
                     .InvariantCultureIgnoreCase ) )
                .Where( n =>
                    (NuGetVersionString)n.Metadata.Version
                    == request.Version )
                    .ToList();
            if (packages.Count < 1)
            {
                provider.RespondNotFound( context );
                return;
            }

            foreach (var package in packages)
            {
                await provider.NupkgProvider
                    .DeleteAsync( package,
                    context.RequestAborted );
            }
        }

        private static bool IsValidApiKey(
            this INugetServerProvider provider,
            PackageInputModel request )
            => string.Equals(
                provider?.NugetServerOptions?.ApiKey,
                request?.ApiKey );

        private static PackageInputModel
        GetRequestingModel(
            this INugetServerProvider provider,
            HttpContext context )
            => new PackageInputModel(
                context.Request.Method,
                context.Request.GetTypedHeaders(),
                context.Request.Path );


        public static bool IsMatchPackagePublish(
            this INugetServerProvider provider,
            HttpContext context )
        {
            if (!provider.IsMatchVerbs( context,
                HttpMethods.IsPut,
                HttpMethods.IsDelete ))
            {
                return false;
            }

            return provider.IsMatchResource(
                context,
                NugetServerResourceType.PackagePublish
                );
        }

    }
}
