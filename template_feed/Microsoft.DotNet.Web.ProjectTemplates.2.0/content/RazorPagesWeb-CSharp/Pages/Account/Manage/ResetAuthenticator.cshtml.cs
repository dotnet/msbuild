using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Company.WebApplication1.Data;

namespace Company.WebApplication1.Pages.Account.Manage
{
    public class Reset2faModel : PageModel
    {
        UserManager<ApplicationUser> _userManager;
        ILogger<Reset2faModel> _logger;

        public Reset2faModel(
            UserManager<ApplicationUser> userManager,
            ILogger<Reset2faModel> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }
        public async Task<IActionResult> OnGet()
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            if (user == null)
            {
                return RedirectToPage("/Error");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            if (user == null)
            {
                return RedirectToPage("/Error");
            }

            await _userManager.SetTwoFactorEnabledAsync(user, false);
            await _userManager.ResetAuthenticatorKeyAsync(user);
            _logger.LogInformation("{UserName} has reset their authentication app key.", user.UserName);

            return RedirectToPage("./EnableAuthenticator");
        }
    }
}