using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Identity.Service.IntegratedWebClient.Extensions
{
    public class IntegratedWebClientOptionsSetup : ConfigureOptions<IntegratedWebClientOptions>
    {
        public IntegratedWebClientOptionsSetup(IConfiguration config) :
            base(options => config.GetSection("Authentication:IdentityService").Bind(options))
        { }
    }
}
