using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Company.WebApplication1
{
    public class AzureAdOptions
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string AADInstance { get; set; }
        public string Domain { get; set; }
        public string TenantId { get; set; }
        public string CallbackPath { get; set; }
#if (MultiOrgAuth)
        public string Authority => $"{AADInstance}Common";
#elseif (SingleOrgAuth)
        public string Authority => $"{AADInstance}{TenantId}";
#endif
    }
}
