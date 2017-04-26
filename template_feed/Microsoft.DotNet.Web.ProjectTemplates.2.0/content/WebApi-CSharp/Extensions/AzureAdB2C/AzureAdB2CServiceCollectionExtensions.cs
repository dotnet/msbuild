using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Authentication.Extensions
{
    public static class AzureAdB2CServiceCollectionExtensions
    {
        public static IServiceCollection AddAzureAdB2CWebApiAuthentication(this IServiceCollection services)
        {
            services.AddWebApiAuthentication();
            services.AddSingleton<IConfigureOptions<AzureAdB2COptions>, AzureAdB2COptionsSetup>();
            services.AddSingleton<IConfigureOptions<JwtBearerOptions>, AzureAdB2CJwtBearerOptionsSetup>();
            return services;
        }
    }
}
