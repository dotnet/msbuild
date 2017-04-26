using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.Service;
using Microsoft.AspNetCore.Identity.Service.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Company.WebApplication1.Identity.Models.HomeViewModels;

namespace Company.WebApplication1.Identity.Controllers
{
    [Area("IdentityService")]
    [IdentityServiceRoute("")]
    [Authorize(IdentityServiceOptions.LoginPolicyName)]
    [AllowAnonymous]
    public class HomeController : Controller
    {
        public HomeController(
            ApplicationManager<IdentityServiceApplication> appManager,
            IOptions<IdentityServiceOptions> options)
        {
            ApplicationManager = appManager;
            Options = options.Value;
        }

        public ApplicationManager<IdentityServiceApplication> ApplicationManager { get; set; }
        public IdentityServiceOptions Options { get; set; }

        [HttpGet]
        public IActionResult Index()
        {
            var model = new IdentityServiceViewModel
            {
                Issuer = Options.Issuer,
                Clients = ApplicationManager.Applications
                    .Select(app => new ClientViewModel()
                    {
                        Name = app.Name,
                        ClientId = app.ClientId,
                        RedirectUris = app.RedirectUris
                            .Where(uri => !uri.IsLogout)
                            .Select(uri => uri.Value),
                        Scopes = app.Scopes.Select(scope => scope.Value)
                    }),
            };
            
            return View(model);
        }
    }
}
