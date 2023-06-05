﻿namespace Ocelot.Authentication.Middleware
{
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.AspNetCore.Http;
    using Ocelot.Configuration;
    using Ocelot.Logging;
    using Ocelot.Middleware;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public sealed class AuthenticationMiddleware : OcelotMiddleware
    {
        private readonly RequestDelegate _next;

        public AuthenticationMiddleware(RequestDelegate next, IOcelotLoggerFactory loggerFactory)
            : base(loggerFactory.CreateLogger<AuthenticationMiddleware>())
        {
            _next = next;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            var downstreamRoute = httpContext.Items.DownstreamRoute();

            if (httpContext.Request.Method.ToUpper() != "OPTIONS" && IsAuthenticatedRoute(downstreamRoute))
            {
                Logger.LogInformation($"{httpContext.Request.Path} is an authenticated route. {MiddlewareName} checking if client is authenticated");

                var result = await AuthenticateAsync(httpContext, downstreamRoute);

                httpContext.User = result.Principal;

                if (httpContext.User.Identity.IsAuthenticated)
                {
                    Logger.LogInformation($"Client has been authenticated for {httpContext.Request.Path}");
                    await _next.Invoke(httpContext);
                }
                else
                {
                    var error = new UnauthenticatedError(
                        $"Request for authenticated route {httpContext.Request.Path} by {httpContext.User.Identity.Name} was unauthenticated");

                    Logger.LogWarning($"Client has NOT been authenticated for {httpContext.Request.Path} and pipeline error set. {error}");

                    httpContext.Items.SetError(error);
                }
            }
            else
            {
                Logger.LogInformation($"No authentication needed for {httpContext.Request.Path}");

                await _next.Invoke(httpContext);
            }
        }

        private async Task<AuthenticateResult> AuthenticateAsync(HttpContext httpContext, DownstreamRoute route)
        {
            AuthenticateResult result = null;

            if (!string.IsNullOrWhiteSpace(route.AuthenticationOptions.AuthenticationProviderKey))
            {
                result = await httpContext.AuthenticateAsync(route.AuthenticationOptions.AuthenticationProviderKey);
                if (result.Succeeded)
                {
                    return result;
                }
            }

            IEnumerable<string> authenticationProviderKeys =
                route
                .AuthenticationOptions
                .AuthenticationProviderKeys
                ?.Where(apk => !string.IsNullOrWhiteSpace(apk))
                ?? Array.Empty<string>();

            foreach (var authenticationProviderKey in authenticationProviderKeys)
            {
                result = await httpContext.AuthenticateAsync(authenticationProviderKey);
                if (result.Succeeded)
                {
                    break;
                }
            }

            return result;
        }

        private static bool IsAuthenticatedRoute(DownstreamRoute route)
        {
            return route.IsAuthenticated;
        }
    }
}
