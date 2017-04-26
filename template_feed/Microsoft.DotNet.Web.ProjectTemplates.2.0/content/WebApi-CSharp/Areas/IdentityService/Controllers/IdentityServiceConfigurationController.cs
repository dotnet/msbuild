using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Service;
using Microsoft.AspNetCore.Identity.Service.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Company.WebApplication1.Identity.Models;

namespace Company.WebApplication1.Identity.Controllers
{
    [Area("IdentityService")]
    [IdentityServiceRoute(IdentityServiceConstants.DefaultPolicy)]
    public class IdentityServiceConfigurationController : Controller
    {
        private readonly IConfigurationManager _configurationProvider;
        private readonly IKeySetMetadataProvider _keySetProvider;

        private static readonly string ConfigurationContextId = $"{IdentityServiceConstants.Tenant}:{IdentityServiceConstants.DefaultPolicy}";

        public IdentityServiceConfigurationController(
            IConfigurationManager configurationProvider,
            IKeySetMetadataProvider keySetProvider)
        {
            _configurationProvider = configurationProvider;
            _keySetProvider = keySetProvider;
        }

        [HttpGet("v" + IdentityServiceConstants.Version + "/.well-known/openid-configuration")]
        [Produces("application/json")]
        public async Task<IActionResult> Metadata()
        {
            var configurationContext = new ConfigurationContext
            {
                Id = ConfigurationContextId,
                HttpContext = HttpContext,
                AuthorizationEndpoint = EndpointLink("Authorize", "IdentityService"),
                TokenEndpoint = EndpointLink("Token", "IdentityService"),
                JwksUriEndpoint = EndpointLink("Keys", "IdentityServiceConfiguration"),
                EndSessionEndpoint = EndpointLink("Logout", "IdentityService"),
            };

            return Ok(await _configurationProvider.GetConfigurationAsync(configurationContext));
        }

        [HttpGet("discovery/v" + IdentityServiceConstants.Version + "/keys")]
        [Produces("application/json")]
        public async Task<IActionResult> Keys()
        {
            return Ok(await _keySetProvider.GetKeysAsync());
        }

        private string EndpointLink(string action, string controller) =>
            Url.Action(action, controller, null, Request.Scheme, Request.Host.Value);
    }
}
