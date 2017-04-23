using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Extensions;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.AspNetCore.Authentication.Extensions
{
    public class AzureAdB2COpenIdConnectOptionsSetup : IConfigureOptions<OpenIdConnectOptions>
    {
        private readonly AzureAdB2COptions _b2cOptions;

        public AzureAdB2COpenIdConnectOptionsSetup(IOptions<AzureAdB2COptions> b2cOptions)
        {
            _b2cOptions = b2cOptions.Value;
        }

        public void Configure(OpenIdConnectOptions oidcOptions)
        {
            oidcOptions.ClientId = _b2cOptions.ClientId;
            oidcOptions.Authority = _b2cOptions.Authority;
            oidcOptions.UseTokenLifetime = true;
            oidcOptions.CallbackPath = _b2cOptions.CallbackPath;

            oidcOptions.TokenValidationParameters = new TokenValidationParameters() { NameClaimType = "name" };

            oidcOptions.Events = new OpenIdConnectEvents()
            {
                OnRedirectToIdentityProvider = OnRedirectToIdentityProvider,
                OnRemoteFailure = OnRemoteFailure
            };
        }

        public Task OnRedirectToIdentityProvider(RedirectContext context)
        {
            var defaultPolicy = _b2cOptions.DefaultPolicy;
            if (context.Properties.Items.TryGetValue(AzureAdB2COptions.PolicyAuthenticationProperty, out var policy) && 
                !policy.Equals(defaultPolicy))
            {
                context.ProtocolMessage.Scope = OpenIdConnectScope.OpenIdProfile;
                context.ProtocolMessage.ResponseType = OpenIdConnectResponseType.IdToken;
                context.ProtocolMessage.IssuerAddress = context.ProtocolMessage.IssuerAddress.Replace(defaultPolicy, policy);
                context.Properties.Items.Remove(AzureAdB2COptions.PolicyAuthenticationProperty);
            }
            return Task.FromResult(0);
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

