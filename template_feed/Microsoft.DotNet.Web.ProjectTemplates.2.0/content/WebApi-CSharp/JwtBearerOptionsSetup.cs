using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Company.WebApplication1
{
    public class JwtBearerOptionsSetup : IConfigureOptions<JwtBearerOptions>
    {
#if (IndividualB2CAuth)
        public JwtBearerOptionsSetup(IOptions<AzureAdB2COptions> options)
#else
        public JwtBearerOptionsSetup(IOptions<AzureAdOptions> options)
#endif
        {
            Options = options.Value;
        }

#if (IndividualB2CAuth)
        public AzureAdB2COptions Options { get; set; }
#else
        public AzureAdOptions Options { get; set; }
#endif
        
        public void Configure(JwtBearerOptions jwtOptions)
        {
            jwtOptions.Audience = Options.Audience;
            jwtOptions.Authority = Options.Authority;
        }
    }
}
