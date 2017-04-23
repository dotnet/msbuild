using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.AspNetCore.Authentication.Extensions
{
    public class AzureAdOpenIdConnectOptionsSetup : IConfigureOptions<OpenIdConnectOptions>
    {
        private readonly AzureAdOptions _aadOptions;

        public AzureAdOpenIdConnectOptionsSetup(IOptions<AzureAdOptions> aadOptions)
        {
            _aadOptions = aadOptions.Value;
        }

        public void Configure(OpenIdConnectOptions oidcOptions)
        {
            oidcOptions.ClientId = _aadOptions.ClientId;
            oidcOptions.Authority = _aadOptions.Authority;
            oidcOptions.UseTokenLifetime = true;
            oidcOptions.CallbackPath = _aadOptions.CallbackPath;
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
    }
}

