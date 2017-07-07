using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Company.WebApplication1.Data;
using Company.WebApplication1.Extensions;
using Company.WebApplication1.Services;


namespace Company.WebApplication1.Pages.Account.Manage
{
    public partial class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailSender _emailSender;

        public IndexModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailSender emailSender)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
        }

        public string Email { get; set; }

        public bool HasAuthenticator { get; set; }

        public bool HasPassword { get; set; }

        public bool HasAny2faProviders { get; set; }

        public bool IsEmailConfirmed { get; set; }

        public bool Is2faEnabled { get; set; }

        public IList<UserLoginInfo> Logins { get; set; }

        public int RecoveryCodesLeft { get ; set;}

        public bool ShowStatusMessage => !string.IsNullOrEmpty(StatusMessage);

        [TempData]
        public string StatusMessage { get; set; }

        public string StatusMessageClass => StatusMessage.Equals(ManageMessages.Error) ? "error" : "success";

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            if (user == null)
            {
                return RedirectToPage("/Error");
            }

            HasAuthenticator = await _userManager.GetAuthenticatorKeyAsync(user) != null;
            HasPassword = await _userManager.HasPasswordAsync(user);
            Logins = await _userManager.GetLoginsAsync(user);
            Email = await _userManager.GetEmailAsync(user);
            IsEmailConfirmed = await _userManager.IsEmailConfirmedAsync(user);
            Is2faEnabled = await _userManager.GetTwoFactorEnabledAsync(user);
            HasAny2faProviders = (await _userManager.GetValidTwoFactorProvidersAsync(user)).Any();
            RecoveryCodesLeft = await _userManager.CountRecoveryCodesAsync(user);

            return Page();
        }

        public async Task<IActionResult> OnPostConfirmEmailAsync()
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            var email = await _userManager.GetEmailAsync(user);
            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var callbackUrl = Url.EmailConfirmationLink(user.Id, code, Request.Scheme);
            await _emailSender.SendEmailConfirmationAsync(email, callbackUrl);
            return RedirectToPage("./EmailConfirmationSent");
        }
    }
}
