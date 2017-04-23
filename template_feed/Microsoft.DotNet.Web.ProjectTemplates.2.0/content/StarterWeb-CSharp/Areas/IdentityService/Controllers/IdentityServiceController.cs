using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity.Service;
using Microsoft.AspNetCore.Identity.Service.Extensions;
using Microsoft.AspNetCore.Identity.Service.IntegratedWebClient;
using Microsoft.AspNetCore.Identity.Service.Mvc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Company.WebApplication1.Identity.Models;

namespace Company.WebApplication1.Identity.Controllers
{
    [Area("IdentityService")]
    [IdentityServiceRoute(IdentityServiceConstants.DefaultPolicy + "/oauth2/v" + IdentityServiceConstants.Version + "/[action]")]
    public class IdentityServiceController : Controller
    {
        private readonly IOptions<IdentityServiceOptions> _options;
        private readonly ITokenManager _tokenManager;
        private readonly SessionManager<ApplicationUser, IdentityServiceApplication> _sessionManager;
        private readonly IAuthorizationResponseFactory _authorizationResponseFactory;
        private readonly ITokenResponseFactory _tokenResponseFactory;

        public IdentityServiceController(
            IOptions<IdentityServiceOptions> options,
            ITokenManager tokenManager,
            SessionManager<ApplicationUser, IdentityServiceApplication> sessionManager,
            IAuthorizationResponseFactory authorizationResponseFactory,
            ITokenResponseFactory tokenResponseFactory)
        {
            _options = options;
            _tokenManager = tokenManager;
            _sessionManager = sessionManager;
            _authorizationResponseFactory = authorizationResponseFactory;
            _tokenResponseFactory = tokenResponseFactory;
        }

        [AcceptVerbs("GET", "POST")]
        public async Task<IActionResult> Authorize(
            [EnableIntegratedWebClient, ModelBinder(typeof(AuthorizationRequestModelBinder))] AuthorizationRequest authorization)
        {
            if (!authorization.IsValid)
            {
                return this.InvalidAuthorization(authorization.Error);
            }

            var authorizationResult = await _sessionManager.IsAuthorizedAsync(authorization);
            if (authorizationResult.Status == AuthorizationStatus.Forbidden)
            {
                return this.InvalidAuthorization(authorizationResult.Error);
            }

            if (authorizationResult.Status == AuthorizationStatus.LoginRequired)
            {
                return RedirectToLogin(nameof(AccountController.Login), "Account", authorization.Message);
            }

            var context = authorization.CreateTokenGeneratingContext(
                authorizationResult.User,
                authorizationResult.Application);

            AddAmbientClaims(context);

            await _tokenManager.IssueTokensAsync(context);
            var response = await _authorizationResponseFactory.CreateAuthorizationResponseAsync(context);

            await _sessionManager.StartSessionAsync(authorizationResult.User, authorizationResult.Application);

            return this.ValidAuthorization(response);
        }

        [HttpPost]
        [Produces("application/json")]
        public async Task<IActionResult> Token(
            [ModelBinder(typeof(TokenRequestModelBinder))] TokenRequest request)
        {
            if (!request.IsValid)
            {
                return BadRequest(request.Error.Parameters);
            }

            var session = await _sessionManager.CreateSessionAsync(request.UserId, request.ClientId);

            var context = request.CreateTokenGeneratingContext(session.User, session.Application);

            AddAmbientClaims(context);

            await _tokenManager.IssueTokensAsync(context);
            var response = await _tokenResponseFactory.CreateTokenResponseAsync(context);
            return Ok(response.Parameters);
        }

        [HttpGet]
        public async Task<IActionResult> Logout(
            [EnableIntegratedWebClient, ModelBinder(typeof(LogoutRequestModelBinder))] LogoutRequest request)
        {
            if (!request.IsValid)
            {
                return View("InvalidLogoutRedirect", request.Message);
            }

            var endSessionResult = await _sessionManager.EndSessionAsync(request);
            if (endSessionResult.Status == LogoutStatus.RedirectToLogoutUri)
            {
                return Redirect(endSessionResult.LogoutRedirect);
            }
            else
            {
                return View("LoggedOut", request);
            }
        }

        private IActionResult RedirectToLogin(string action, string controller, OpenIdConnectMessage message)
        {
            var messageCopy = message.Clone();
            messageCopy.Prompt = null;

            var parameters = new
            {
                ReturnUrl = Url.Action("Authorize", "IdentityService", messageCopy.Parameters)
            };

            return RedirectToAction(action, controller, parameters);
        }

        private void AddAmbientClaims(TokenGeneratingContext context)
        {
            context.AddExtensionsAmbientClaims(
                policy: IdentityServiceConstants.DefaultPolicy,
                version: IdentityServiceConstants.Version,
                tenantId: IdentityServiceConstants.TenantId
            );
        }
    }
}
