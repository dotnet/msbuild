// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Http;

namespace Microsoft.AspNetCore.Watch.BrowserRefresh
{
    internal static class Urls
    {
        /// <summary>
        /// An endpoint that responds with cache-clearing headers. See https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Clear-Site-Data#directives.
        /// </summary>
        /// <value><c>/_framework/clear-browser-cache</c></value>
        public static PathString ClearSiteData { get; } = "/_framework/clear-browser-cache";

        /// <summary>
        /// Returns a JS file that handles browser refresh and showing notifications.
        /// </summary>
        /// <value><c>/_framework/aspnetcore-browser-refresh.js</c></value>
        public static PathString BrowserRefreshJS { get; } = "/_framework/aspnetcore-browser-refresh.js";

        /// <summary>
        /// Hosts a middleware that can cache deltas sent by dotnet-watch.
        /// </summary>
        /// <value><c>/_framework/blazor-hotreload</c></value>
        public static PathString BlazorHotReloadMiddleware { get; } = "/_framework/blazor-hotreload";

        /// <summary>
        /// Returns a JS file imported by BlazorWebAssembly as part of it's initialization. Contains
        /// scripts to apply deltas on app start.
        /// </summary>
        /// <value>/_framework/blazor-hotreload.js</value>
        public static PathString BlazorHotReloadJS { get; } = "/_framework/blazor-hotreload.js";
    }
}
