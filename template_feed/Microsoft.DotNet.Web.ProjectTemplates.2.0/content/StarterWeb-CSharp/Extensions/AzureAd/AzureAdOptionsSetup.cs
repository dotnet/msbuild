using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Authentication.Extensions
{
    public class AzureAdOptionsSetup : ConfigureOptions<AzureAdOptions>
    {
        public AzureAdOptionsSetup(IConfiguration config) : 
            base (options => config.GetSection("Authentication:AzureAd").Bind(options))
        { }
    }
}
