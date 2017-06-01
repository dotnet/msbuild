using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Authentication.Extensions
{
    public static class AzureAdServiceCollectionExtensions
    {
        public static IServiceCollection AddAzureAdBearerAuthentication(this IServiceCollection services)
        {
            // Move to config binding
            services.AddAuthentication(sharedOptions =>
            {
                sharedOptions.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                sharedOptions.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            });

            services.AddSingleton<IConfigureOptions<AzureAdOptions>, BindAzureAdOptions>();
            services.AddSingleton<IInitializeOptions<JwtBearerOptions>, InitializeFromAzureOptions>();
            services.AddJwtBearerAuthentication();
            return services;
        }

        private class BindAzureAdOptions : ConfigureOptions<AzureAdOptions>
        {
            public BindAzureAdOptions(IConfiguration config) :
                base(options => config.GetSection("Microsoft:AspNetCore:Authentication:AzureAd").Bind(options))
            { }
        }

        private class InitializeFromAzureOptions: IInitializeOptions<JwtBearerOptions>
        {
            private readonly AzureAdOptions _azureOptions;

            public InitializeFromAzureOptions(IOptions<AzureAdOptions> azureOptions)
            {
                _azureOptions = azureOptions.Value;
            }

            public void Initialize(string name, JwtBearerOptions options)
            {
                options.Audience = _azureOptions.ClientId;
                options.Authority = $"{_azureOptions.Instance}{_azureOptions.TenantId}";
            }
        }
    }
}
