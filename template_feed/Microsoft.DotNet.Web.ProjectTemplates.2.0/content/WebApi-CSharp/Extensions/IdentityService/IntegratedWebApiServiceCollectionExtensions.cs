using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.Identity.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Identity.Service.Extensions
{ 
    public static class IntegratedWebApiServiceCollectionExtensions
    {
        public static IServiceCollection AddIntegratedWebApi(this IServiceCollection services)
        {
            services.AddSingleton<IConfigureOptions<JwtBearerOptions>, IntegratedWebApiJwtBearerOptionsSetup>();
            services.Configure<DeveloperCertificateOptions>(options => options.ListeningEndpoint = "/");
            return services;
        }
    }
}
