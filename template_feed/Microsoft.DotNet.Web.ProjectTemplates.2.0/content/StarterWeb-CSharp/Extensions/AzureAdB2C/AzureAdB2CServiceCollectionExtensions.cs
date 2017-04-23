using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Authentication.Extensions
{
    public static class AzureAdB2CServiceCollectionExtensions
    {
        public static IServiceCollection AddAzureAdB2CAuthentication(this IServiceCollection services)
        {
            services.AddWebApplicationAuthentication();
            services.AddSingleton<IConfigureOptions<AzureAdB2COptions>, AzureAdB2COptionsSetup>();
            services.AddSingleton<IConfigureOptions<OpenIdConnectOptions>, AzureAdB2COpenIdConnectOptionsSetup>();
            return services;
        }
    }
}
