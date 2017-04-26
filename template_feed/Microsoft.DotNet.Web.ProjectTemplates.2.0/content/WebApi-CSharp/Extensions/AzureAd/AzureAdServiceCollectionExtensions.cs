using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Authentication.Extensions
{
    public static class AzureAdServiceCollectionExtensions
    {
        public static IServiceCollection AddAzureAdWebApiAuthentication(this IServiceCollection services)
        {
            services.AddWebApiAuthentication();
            services.AddSingleton<IConfigureOptions<AzureAdOptions>, AzureAdOptionsSetup>();
            services.AddSingleton<IConfigureOptions<JwtBearerOptions>, AzureAdJwtBearerOptionsSetup>();
            return services;
        }
    }
}
