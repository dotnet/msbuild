using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.Service;
using Microsoft.AspNetCore.Identity.Service.IntegratedWebClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Identity.Service.Extensions
{
    public class IntegratedWebApiJwtBearerOptionsSetup : IConfigureOptions<JwtBearerOptions>
    {
        private readonly IConfiguration _config;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IKeySetMetadataProvider _keySetMetadataProvider;

        public IntegratedWebApiJwtBearerOptionsSetup(
            IConfiguration config,
            IHttpContextAccessor httpContextAccessor,
            IKeySetMetadataProvider keysProvider)
        {
            _config = config;
            _httpContextAccessor = httpContextAccessor;
            _keySetMetadataProvider = keysProvider;
        }

        public void Configure(JwtBearerOptions options)
        {
            options.Audience = _config["Authentication:IdentityService:Audience"];
            options.ConfigurationManager = new WebApplicationConfiguration(null, _httpContextAccessor);
            var keys = _keySetMetadataProvider.GetKeysAsync().GetAwaiter().GetResult().Keys;
            options.TokenValidationParameters.NameClaimType = "name";
            options.TokenValidationParameters.IssuerSigningKeys = keys;
        }
    }
}
