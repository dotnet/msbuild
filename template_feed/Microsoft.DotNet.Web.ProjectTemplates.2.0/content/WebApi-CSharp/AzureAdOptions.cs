using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Company.WebApplication1
{
    public class AzureAdOptions
    {
        public string Audience { get; set; }
        public string AAdInstance { get; set; }
        public string TenantId { get; set; }
        public string Authority => $"{AAdInstance}/{TenantId}";
    }
}
