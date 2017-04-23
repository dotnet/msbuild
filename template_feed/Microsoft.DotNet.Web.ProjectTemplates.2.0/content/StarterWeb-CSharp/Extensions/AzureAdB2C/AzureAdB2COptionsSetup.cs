using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Authentication.Extensions
{
    public class AzureAdB2COptionsSetup : ConfigureOptions<AzureAdB2COptions>
    {
        public AzureAdB2COptionsSetup(IConfiguration config) : 
            base (options => config.GetSection("Authentication:AzureAdB2C").Bind(options))
        { }
    }
}
