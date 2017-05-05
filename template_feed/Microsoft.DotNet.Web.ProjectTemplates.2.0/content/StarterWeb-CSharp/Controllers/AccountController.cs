using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
#if (IndividualB2CAuth)
using Microsoft.AspNetCore.Authentication.Extensions;
#endif
using Microsoft.AspNetCore.Mvc;
#if (IndividualB2CAuth)
using Microsoft.Extensions.Options;
#endif

namespace Company.WebApplication1.Controllers
{
    public class AccountController : Controller
    {
#if (IndividualB2CAuth)
        public AccountController(IOptions<AzureAdB2COptions> b2cOptions)
        {
            Options = b2cOptions.Value;
        }

        public AzureAdB2COptions Options { get; set; }

#endif
        //
        // GET: /Account/SignIn
        [HttpGet]
        public IActionResult SignIn()
        {
            return Challenge(
                new AuthenticationProperties { RedirectUri = "/" }, OpenIdConnectDefaults.AuthenticationScheme);
        }

#if (IndividualLocalAuth)
        [HttpGet]
        public IActionResult Manage()
        {
            return RedirectToAction("Index", "Manage", new { area = "IdentityService" });
        }
#elseif (IndividualB2CAuth)
        [HttpGet]
        public IActionResult ResetPassword()
        {
            var properties = new AuthenticationProperties() { RedirectUri = "/" };
            properties.Items[AzureAdB2COptions.PolicyAuthenticationProperty] = Options.ResetPasswordPolicyId;
            return Challenge(properties, OpenIdConnectDefaults.AuthenticationScheme);
        }

        [HttpGet]
        public async Task EditProfile()
        {
            var properties = new AuthenticationProperties() { RedirectUri = "/" };
            properties.Items[AzureAdB2COptions.PolicyAuthenticationProperty] = Options.EditProfilePolicyId;
            await HttpContext.ChallengeAsync(
                OpenIdConnectDefaults.AuthenticationScheme, properties, ChallengeBehavior.Unauthorized);
        }
        
#endif
        //
        // GET: /Account/SignOut
        [HttpGet]
        public IActionResult SignOut()
        {
            var callbackUrl = Url.Action(nameof(SignedOut), "Account", values: null, protocol: Request.Scheme);
            return SignOut(new AuthenticationProperties { RedirectUri = callbackUrl },
                CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme);
        }

        //
        // GET: /Account/SignedOut
        [HttpGet]
        public IActionResult SignedOut()
        {
            if (HttpContext.User.Identity.IsAuthenticated)
            {
                // Redirect to home page if the user is authenticated.
                return RedirectToAction(nameof(HomeController.Index), "Home");
            }

            return View();
        }

        //
        // GET: /Account/AccessDenied
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
