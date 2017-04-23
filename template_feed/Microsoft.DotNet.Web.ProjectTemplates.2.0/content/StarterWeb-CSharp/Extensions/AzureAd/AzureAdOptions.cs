using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Authentication.Extensions
{
    public class AzureAdOptions
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string AzureAdInstance { get; set; }
        public string Domain { get; set; }
        public string TenantId { get; set; }
        public string CallbackPath { get; set; }
        public string Authority => $"{AzureAdInstance}{TenantId}";
    }
}
