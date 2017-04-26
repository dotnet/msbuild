using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer; 
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Authentication.Extensions
{
    public class AzureAdJwtBearerOptionsSetup : IConfigureOptions<JwtBearerOptions>
    {
        private readonly AzureAdOptions _adOptions;

        public AzureAdJwtBearerOptionsSetup(IOptions<AzureAdOptions> adOptions)
        {
            _adOptions = adOptions.Value;
        }
        
        public void Configure(JwtBearerOptions jwtOptions)
        {
            jwtOptions.Audience = _adOptions.Audience;
            jwtOptions.Authority = _adOptions.Authority;
        }
    }
}
