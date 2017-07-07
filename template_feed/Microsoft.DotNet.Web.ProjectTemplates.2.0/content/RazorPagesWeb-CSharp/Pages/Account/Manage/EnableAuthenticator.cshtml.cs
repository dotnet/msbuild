using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Company.WebApplication1.Data;

namespace Company.WebApplication1.Pages.Account.Manage
{
    public class EnableAuthenticator : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<EnableAuthenticator> _logger;

        private const string AuthenicatorUriFormat = "otpauth://totp/{0}:{1}?secret={2}&issuer={0}";

        public EnableAuthenticator(
            UserManager<ApplicationUser> userManager,
            ILogger<EnableAuthenticator> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        public string AuthenticatorUri { get; set; }

        public string PublicKey { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public string StatusMessageClass => StatusMessage.Equals(ManageMessages.Error) ? "error" : "success";

        public bool ShowStatusMessage => !string.IsNullOrEmpty(StatusMessage);

        [BindProperty]
        [Required]
        [StringLength(7, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Text)]
        [Display(Name = "Verification Code")]
        public string VerificationCode { get; set; }        

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            if (user == null)
            {
                return RedirectToPage("/Error");
            }

            var unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(unformattedKey))
            {
                await _userManager.ResetAuthenticatorKeyAsync(user);
                unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
            }

            PublicKey = FormatKey(unformattedKey);
            AuthenticatorUri = GenerateQrCodeUri(user.Email, unformattedKey);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            if (user == null)
            {
                return RedirectToPage("/Error");
            }

            if (ModelState.IsValid)
            {
                // Strip spaces and hypens
                VerificationCode = VerificationCode.Replace(" ", string.Empty).Replace("-", string.Empty);
                
                var result = await _userManager.VerifyTwoFactorTokenAsync(user, _userManager.Options.Tokens.AuthenticatorTokenProvider, VerificationCode);

                if (result)
                {
                    await _userManager.SetTwoFactorEnabledAsync(user, true);
                    _logger.LogInformation("{UserName} has enabled 2FA with an authenticator app.", user.UserName);
                    return RedirectToPage("./GenerateRecoveryCodes");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Confirmation code is incorrect.");
                    VerificationCode = string.Empty;
                }
            }

            var unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
            PublicKey = FormatKey(unformattedKey);
            AuthenticatorUri = GenerateQrCodeUri(user.Email, unformattedKey);

            return Page();
        }

        private string FormatKey(string unformattedKey)
        {
            StringBuilder result = new StringBuilder();
            int currentPosition = 0;
            while (currentPosition + 4 < unformattedKey.Length)
            {
                result.Append(unformattedKey.Substring(currentPosition, 4)).Append(" ");
                currentPosition += 4;
            }
            if (currentPosition < unformattedKey.Length)
            {
                result.Append(unformattedKey.Substring(currentPosition));
            }

            return result.ToString().ToLowerInvariant();
        }

        private string GenerateQrCodeUri(string email, string unformattedKey)
        {
            return string.Format(
                AuthenicatorUriFormat,
                "Company.WebApplication1",
                email,
                unformattedKey);

        }
    }
}