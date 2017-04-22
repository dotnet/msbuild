using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;

namespace Company.WebApplication1
{
    public class OpenIdConnectOptionsSetup : IConfigureOptions<OpenIdConnectOptions>
    {
#if (IndividualB2CAuth)
        public OpenIdConnectOptionsSetup(IOptions<AzureAdB2COptions> b2cOptions)
#else
        public OpenIdConnectOptionsSetup(IOptions<AzureAdOptions> b2cOptions)
#endif
        {
            Options = b2cOptions.Value;
        }

#if (IndividualB2CAuth)
        public AzureAdB2COptions Options { get; set; }
#else
        public AzureAdOptions Options { get; set; }
#endif

        public void Configure(OpenIdConnectOptions oidcOptions)
        {
            oidcOptions.ClientId = Options.ClientId;
            oidcOptions.Authority = Options.Authority;
            oidcOptions.UseTokenLifetime = true;
            oidcOptions.CallbackPath = Options.CallbackPath;
#if (OrganizationalAuth)
    #if (OrgReadAccess)
            oidcOptions.ResponseType = OpenIdConnectResponseType.CodeIdToken;
    #endif
    #if (MultiOrgAuth)

            oidcOptions.TokenValidationParameters = new TokenValidationParameters
            {
                // Instead of using the default validation (validating against a single issuer value, as we do in line of business apps),
                // we inject our own multitenant validation logic
                ValidateIssuer = false,

                // If the app is meant to be accessed by entire organizations, add your issuer validation logic here.
                //IssuerValidator = (issuer, securityToken, validationParameters) => {
                //    if (myIssuerValidationLogic(issuer)) return issuer;
                //}
            };

            oidcOptions.Events = new OpenIdConnectEvents
            {
                OnTicketReceived = (context) =>
                {
                    // If your authentication logic is based on users then add your logic here
                    return Task.FromResult(0);
                },
                OnAuthenticationFailed = (context) =>
                {
                    context.Response.Redirect("/Home/Error");
                    context.HandleResponse(); // Suppress the exception
                    return Task.FromResult(0);
                },
                // If your application needs to do authenticate single users, add your user validation below.
                //OnTokenValidated = (context) =>
                //{
                //    return myUserValidationLogic(context.Ticket.Principal);
                //}
            };
    #endif
        }
#else

            oidcOptions.TokenValidationParameters = new TokenValidationParameters() { NameClaimType = "name" };

            oidcOptions.Events = new OpenIdConnectEvents()
            {
                OnRedirectToIdentityProvider = OnRedirectToIdentityProvider,
                OnRemoteFailure = OnRemoteFailure
            };
        }

        public Task OnRedirectToIdentityProvider(RedirectContext context)
        {
            var defaultPolicy = Options.DefaultPolicy;
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
#endif
    }
}

