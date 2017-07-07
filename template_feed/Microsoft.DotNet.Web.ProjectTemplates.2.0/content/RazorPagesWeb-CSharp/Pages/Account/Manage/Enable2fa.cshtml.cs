using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Company.WebApplication1.Data;

namespace Company.WebApplication1.Pages.Account.Manage
{
    public class Enable2faModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<Disable2faModel> _logger;

        public Enable2faModel(UserManager<ApplicationUser> userManager,
            ILogger<Disable2faModel> logger)
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

            if (await _userManager.GetTwoFactorEnabledAsync(user))
            {
                return RedirectToPage("/Error");
            }

            if (!(await _userManager.GetValidTwoFactorProvidersAsync(user)).Any())
            {
                return RedirectToPage("/Error");
            }

            await _userManager.SetTwoFactorEnabledAsync(user, true);
            _logger.LogInformation("{UserName} has enabled 2FA.", user.UserName);

            return RedirectToPage("./Index");
        }
    }
}