using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Company.WebApplication1.Identity
{
    // Values used to register the web API with the identity service.
    // To update the registered client, change the values here and then use the 
    // provided migration to update the identity service.
    public class IdentityServiceWebApiConstants
    {
        // The name of the registered web API
        public const string WebApiName = "Company.WebApplication1.WebApi";

        // The default scope for the web API
        public const string DefaultScope = "user_impersonation";
    }
}
