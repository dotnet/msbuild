using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity.Service.IntegratedWebClient.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Authentication.Extensions
{
    public static class IdentityServiceAuthenticationExtensions
    {
        public static IServiceCollection AddIdentityServiceAuthentication(this IServiceCollection services)
        {
            services.AddWebApplicationAuthentication();
            services.AddIntegratedWebClient();
            return services;
        }
    }
}