using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Authentication.Extensions
{
    public static class AzureAdServiceCollectionExtensions
    {
        public static IServiceCollection AddAzureAdAuthentication(this IServiceCollection services)
        {
            services.AddWebApplicationAuthentication();
            services.AddSingleton<IConfigureOptions<AzureAdOptions>, AzureAdOptionsSetup>();
            services.AddSingleton<IConfigureOptions<OpenIdConnectOptions>, AzureAdOpenIdConnectOptionsSetup>();
            return services;
        }
    }
}
