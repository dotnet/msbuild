using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Identity.Service.IntegratedWebClient.Extensions
{ 
    public static class IntegratedWebClientServiceCollectionExtensions
    {
        public static IServiceCollection AddIntegratedWebClient(this IServiceCollection services)
        {
            services.AddSingleton<IConfigureOptions<IntegratedWebClientOptions>, IntegratedWebClientOptionsSetup>();
            services.AddIntegratedWebClient(_ => { });
            return services;
        }
    }
}
