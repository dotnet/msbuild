// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

[assembly: HostingStartup(typeof(Microsoft.AspNetCore.Watch.BrowserRefresh.HostingStartup))]

namespace Microsoft.AspNetCore.Watch.BrowserRefresh
{
    internal sealed class HostingStartup : IHostingStartup, IStartupFilter
    {
        public void Configure(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services => services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupFilter>(this)));
        }

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                app.Map(Urls.ClearSiteData, app1 => app1.Run(context =>
                {
                    // Scoped css files can contain links to other css files. We'll try clearing out the http caches to force the browser to re-download.
                    // See https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Clear-Site-Data#directives
                    context.Response.Headers["Clear-Site-Data"] = "\"cache\"";
                    return Task.CompletedTask;
                }));

                app.Map(Urls.BlazorHotReloadMiddleware, app1 => app1.UseMiddleware<BlazorWasmHotReloadMiddleware>());

                app.Map(Urls.BrowserRefreshJS,
                    app1 => app1.UseMiddleware<BrowserScriptMiddleware>(BrowserScriptMiddleware.GetBrowserRefreshJS()));

                app.Map(Urls.BlazorHotReloadJS,
                    app1 => app1.UseMiddleware<BrowserScriptMiddleware>(BrowserScriptMiddleware.GetBlazorHotReloadJS()));

                app.UseMiddleware<BrowserRefreshMiddleware>();
                next(app);
            };
        }
    }
}
