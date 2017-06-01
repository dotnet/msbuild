using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Authentication.Extensions
{
    public static class AzureAdServiceCollectionExtensions
    {
        public static IServiceCollection AddAzureAdB2CBearerAuthentication(this IServiceCollection services)
        {
            // Move to config binding
            services.AddAuthentication(sharedOptions =>
            {
                sharedOptions.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                sharedOptions.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            });

            services.AddSingleton<IConfigureOptions<AzureAdB2COptions>, BindAzureAdB2COptions>();
            services.AddSingleton<IInitializeOptions<JwtBearerOptions>, InitializeFromAzureOptions>();
            services.AddJwtBearerAuthentication();
            return services;
        }

        private class BindAzureAdB2COptions : ConfigureOptions<AzureAdB2COptions>
        {
            public BindAzureAdB2COptions(IConfiguration config) : 
                base(options => config.GetSection("Microsoft:AspNetCore:Authentication:AzureAdB2C").Bind(options))
            { }
        }

        private class InitializeFromAzureOptions: IInitializeOptions<JwtBearerOptions>
        {
            private readonly AzureAdB2COptions _azureOptions;

            public InitializeFromAzureOptions(IOptions<AzureAdB2COptions> azureOptions)
            {
                _azureOptions = azureOptions.Value;
            }

            public void Initialize(string name, JwtBearerOptions options)
            {
                options.Audience = _azureOptions.ClientId;
                options.Authority = $"{_azureOptions.Instance}/{_azureOptions.Domain}/{_azureOptions.SignUpSignInPolicyId}/v2.0";
            }
        }
    }
}
