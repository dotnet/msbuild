using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Authentication.Extensions
{
    public class AzureAdOptions
    {
        public string Audience { get; set; }
        public string AzureAdInstance { get; set; }
        public string TenantId { get; set; }
        public string Authority => $"{AzureAdInstance}/{TenantId}";
    }
}
