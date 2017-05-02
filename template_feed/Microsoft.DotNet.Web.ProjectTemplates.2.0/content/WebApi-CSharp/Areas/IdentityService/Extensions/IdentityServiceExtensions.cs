using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Identity.Service.Extensions
{
    public static class IdentityServiceExtensions
    {
        public static IIdentityServiceBuilder AddIdentityService<TUser, TApplication>(this IServiceCollection services, IConfiguration config)
            where TUser : class
            where TApplication : class
        {
            services.Configure<IdentityServiceOptions>(config.GetSection("IdentityService"));
            return services.AddIdentityService<TUser, TApplication>(_ => { })
                .AddSigningCertificates(() => new CertificateLoader(config.GetSection("Certificates")).Load(config.GetSection("IdentityService:SigningCertificates")));
        }

        public static IIdentityServiceBuilder AddIdentityServiceExtensions(this IIdentityServiceBuilder builder)
        {
            builder.Services.AddSingleton<IAuthorizationResponseParameterProvider, ClientInfoProvider>();
            builder.Services.AddSingleton<ITokenResponseParameterProvider, ClientInfoProvider>();
            builder.Services.Configure<IdentityServiceOptions>(options =>
            {
                AddContextClaims(options.IdTokenOptions.ContextClaims);
                AddContextClaims(options.AccessTokenOptions.ContextClaims);
            });
            return builder;
        }

        private static void AddContextClaims(TokenMapping tokenMapping)
        {
            tokenMapping.AddSingle(IdentityServiceExtensionsClaimTypes.TrustFrameworkPolicy, IdentityServiceExtensionsAmbientClaimTypes.Policy);
            tokenMapping.AddSingle(IdentityServiceExtensionsClaimTypes.Version, IdentityServiceExtensionsAmbientClaimTypes.Version);
        }

        public static void AddExtensionsAmbientClaims(
            this TokenGeneratingContext context, string policy, string version, string tenantId)
        {
            context.AmbientClaims.Add(new Claim(IdentityServiceExtensionsAmbientClaimTypes.Policy, policy));
            context.AmbientClaims.Add(new Claim(IdentityServiceExtensionsAmbientClaimTypes.Version, version));
            context.AmbientClaims.Add(new Claim(IdentityServiceExtensionsAmbientClaimTypes.TenantId, tenantId));
        }
    }
}
