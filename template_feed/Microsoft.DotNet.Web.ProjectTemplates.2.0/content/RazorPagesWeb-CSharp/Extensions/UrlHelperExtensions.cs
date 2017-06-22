using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Microsoft.AspNetCore.Mvc
{
    public static class UrlHelperExtensions
    {
        /// <summary>
        /// Returns the provided URL if it is actually local, otherwise returns the URL for the
        /// application's home page.
        /// </summary>
        /// <param name="urlHelper">The <see cref="IUrlHelper"/>.</param>
        /// <param name="localUrl">The local URL.</param>
        /// <returns>The provided <paramref name="localUrl"/> if it is actually local, otherwise home page URL.</returns>
        public static string GetLocalUrl(this IUrlHelper urlHelper, string localUrl)
        {
            if (!urlHelper.IsLocalUrl(localUrl))
            {
                return urlHelper.Page("/Index");
            }

            return localUrl;
        }

        public static string EmailConfirmationLink(this IUrlHelper urlHelper, string userId, string code, string scheme)
        {
            return urlHelper.Page("/Account/ConfirmEmail", pageHandler: null, values: new { userId = userId, code = code }, protocol: scheme);
        }
    }
}
