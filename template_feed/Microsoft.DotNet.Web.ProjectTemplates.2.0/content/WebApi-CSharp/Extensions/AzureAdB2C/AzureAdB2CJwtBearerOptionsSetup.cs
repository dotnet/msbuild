using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer; 
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Authentication.Extensions
{
    public class AzureAdB2CJwtBearerOptionsSetup : IConfigureOptions<JwtBearerOptions>
    {
        private readonly AzureAdB2COptions _b2cOptions;

        public AzureAdB2CJwtBearerOptionsSetup(IOptions<AzureAdB2COptions> b2cOptions)
        {
            _b2cOptions = b2cOptions.Value;
        }
        
        public void Configure(JwtBearerOptions jwtOptions)
        {
            jwtOptions.Audience = _b2cOptions.Audience;
            jwtOptions.Authority = _b2cOptions.Authority;
        }
    }
}
