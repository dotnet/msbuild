using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.AspNetCore.Authentication.Extensions
{
    public static class AzureAdB2CServiceCollectionExtensions
    {
        public static IServiceCollection AddAzureAdB2CAuthentication(this IServiceCollection services)
        {
            // Move to config binding
            services.AddAuthentication(sharedOptions =>
            {
                sharedOptions.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                sharedOptions.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                sharedOptions.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            });

            services.AddSingleton<IConfigureOptions<AzureAdB2COptions>, BindAzureAdB2COptions>();
            services.AddSingleton<IInitializeOptions<OpenIdConnectOptions>, InitializeFromAzureOptions>();
            services.AddOpenIdConnectAuthentication(AzureAdB2CDefaults.SignUpSignInAuthenticationScheme, _ => { });
            services.AddOpenIdConnectAuthentication(AzureAdB2CDefaults.ResetPasswordAuthenticationScheme, _ => { });
            services.AddOpenIdConnectAuthentication(AzureAdB2CDefaults.EditProfileAuthenticationScheme, _ => { });
            services.AddCookieAuthentication();
            return services;
        }

        private class BindAzureAdB2COptions : ConfigureOptions<AzureAdB2COptions>
        {
            public BindAzureAdB2COptions(IConfiguration config) : 
                base(options => config.GetSection("Microsoft:AspNetCore:Authentication:AzureAdB2C").Bind(options))
            { }
        }

        private class InitializeFromAzureOptions: IInitializeOptions<OpenIdConnectOptions>
        {
            private readonly AzureAdB2COptions _azureOptions;

            public InitializeFromAzureOptions(IOptions<AzureAdB2COptions> azureOptions)
            {
                _azureOptions = azureOptions.Value;
            }

            public void Initialize(string name, OpenIdConnectOptions options)
            {
                if (name == AzureAdB2CDefaults.SignUpSignInAuthenticationScheme) 
                {
                    Setup(_azureOptions.SignUpSignInPolicyId, options);
                }
                else if (name == AzureAdB2CDefaults.ResetPasswordAuthenticationScheme) 
                {
                    Setup(_azureOptions.ResetPasswordPolicyId, options);
                }
                else if (name == AzureAdB2CDefaults.EditProfileAuthenticationScheme) 
                {
                    Setup(_azureOptions.EditProfilePolicyId, options);
                }
            }
 
            private void Setup(string policyId, OpenIdConnectOptions options)
            {
                options.ClientId = _azureOptions.ClientId;
                options.Authority = $"{_azureOptions.Instance}/{_azureOptions.Domain}/{policyId}/v2.0";
                options.UseTokenLifetime = true;
                options.CallbackPath = _azureOptions.CallbackPath;

                options.TokenValidationParameters = new TokenValidationParameters() { NameClaimType = "name" };

                options.Events = new OpenIdConnectEvents()
                {
                    OnRemoteFailure = OnRemoteFailure
                };
            }

            public Task OnRemoteFailure(FailureContext context)
            {
                context.HandleResponse();
                // Handle the error code that Azure AD B2C throws when trying to reset a password from the login page 
                // because password reset is not supported by a "sign-up or sign-in policy"
                if (context.Failure is OpenIdConnectProtocolException && context.Failure.Message.Contains("AADB2C90118"))
                {
                    // If the user clicked the reset password link, redirect to the reset password route
                    context.Response.Redirect("/Account/ResetPassword");
                }
                else if (context.Failure is OpenIdConnectProtocolException && context.Failure.Message.Contains("access_denied"))
                {
                    context.Response.Redirect("/");
                }
                else
                {
                    context.Response.Redirect("/Home/Error");
                }
                return Task.FromResult(0);
            }
        }
    }
}
