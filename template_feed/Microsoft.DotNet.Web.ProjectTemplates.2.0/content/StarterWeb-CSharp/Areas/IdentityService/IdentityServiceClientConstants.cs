using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Company.WebApplication1.Identity
{
    // Values used to register the client application with the identity service
    // To update the registered client, change the values here and then use the 
    // provided migration to update the identity service
    public class IdentityServiceClientConstants
    {
        // Client application name
        public const string ClientName = "Company.WebApplication1";

        // Client redirect URI
        public const string ClientRedirectUri = "urn:self:aspnet:identity:integrated";

        // Client logout redirect URI
        public const string ClientLogoutRedirectUri = ClientRedirectUri;
    }
}
