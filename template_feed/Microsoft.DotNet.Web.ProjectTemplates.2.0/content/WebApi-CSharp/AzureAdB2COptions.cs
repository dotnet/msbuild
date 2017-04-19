using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Company.WebApplication1
{
    public class AzureAdB2COptions
    {
        public string ClientId { get; set; }
        public string AzureAdB2CInstance { get; set; }
        public string Tenant { get; set; }
        public string SignUpSignInPolicyId { get; set; }
        public string DefaultPolicy => SignUpSignInPolicyId;
        public string Authority => $"{AzureAdB2CInstance}/{Tenant}/{DefaultPolicy}/v2.0";
        public string Audience => ClientId;
    }
}
