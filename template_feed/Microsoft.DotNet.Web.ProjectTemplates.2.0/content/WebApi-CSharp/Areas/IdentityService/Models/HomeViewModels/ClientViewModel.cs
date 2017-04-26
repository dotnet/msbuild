using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Company.WebApplication1.Identity.Models.HomeViewModels
{
    public class ClientViewModel
    {
        public string Name { get; set; }

        [Display(Name = "Client ID")]
        public string ClientId { get; set; }

        [Display(Name = "Redirect URIs")]
        public IEnumerable<string> RedirectUris { get; set; }

        public IEnumerable<string> Scopes { get; set; }
    }
}