using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Microsoft.AspNetCore.Identity.Service.Extensions
{
    public class IdentityServiceRouteAttribute : RouteAttribute
    {
        public IdentityServiceRouteAttribute(string template) : 
            base("tfp/IdentityService/" + template)
        { }
    }
}
