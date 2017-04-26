using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity.Service.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Authentication.Extensions
{
    public static class IdentityServiceAuthenticationExtensions
    {
        public static IServiceCollection AddIdentityServiceAuthentication(this IServiceCollection services)
        {
            services.AddWebApiAuthentication();
            services.AddIntegratedWebApi();
            return services;
        }
    }
}