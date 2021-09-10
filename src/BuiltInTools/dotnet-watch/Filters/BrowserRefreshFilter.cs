// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Graph;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    public sealed class BrowserRefreshFilter : IWatchFilter, IAsyncDisposable
    {
        private readonly bool _suppressBrowserRefresh;
        private readonly IReporter _reporter;
        private BrowserRefreshServer? _refreshServer;

        public BrowserRefreshFilter(DotNetWatchOptions dotNetWatchOptions, IReporter reporter)
        {
            _suppressBrowserRefresh = dotNetWatchOptions.SuppressBrowserRefresh;
            _reporter = reporter;
        }

        public async ValueTask ProcessAsync(DotNetWatchContext context, CancellationToken cancellationToken)
        {
            if (_suppressBrowserRefresh)
            {
                return;
            }

            if (context.Iteration == 0)
            {
                if (context.ProjectGraph is null)
                {
                    _reporter.Verbose("Unable to determine if this project is a webapp.");
                    return;
                }
                else if (IsWebApp(context.ProjectGraph))
                {
                    _reporter.Verbose("Configuring the app to use browser-refresh middleware.");
                }
                else
                {
                    _reporter.Verbose("Skipping configuring browser-refresh middleware since this is not a webapp.");
                    return;
                }

                _refreshServer = new BrowserRefreshServer(context.Reporter);
                context.BrowserRefreshServer = _refreshServer;
                var serverUrls = string.Join(',', await _refreshServer.StartAsync(cancellationToken));
                context.Reporter.Verbose($"Refresh server running at {serverUrls}.");
                context.ProcessSpec.EnvironmentVariables["ASPNETCORE_AUTO_RELOAD_WS_ENDPOINT"] = serverUrls;
                context.ProcessSpec.EnvironmentVariables["ASPNETCORE_AUTO_RELOAD_WS_KEY"] = _refreshServer.ServerKey;

                var pathToMiddleware = Path.Combine(AppContext.BaseDirectory, "middleware", "Microsoft.AspNetCore.Watch.BrowserRefresh.dll");
                context.ProcessSpec.EnvironmentVariables.DotNetStartupHooks.Add(pathToMiddleware);
                context.ProcessSpec.EnvironmentVariables.AspNetCoreHostingStartupAssemblies.Add("Microsoft.AspNetCore.Watch.BrowserRefresh");
            }
            else if (!_suppressBrowserRefresh)
            {
                // We've detected a change. Notify the browser.
                await (_refreshServer?.SendWaitMessageAsync(cancellationToken) ?? default);
            }
        }

        private static bool IsWebApp(ProjectGraph projectGraph)
        {
            // We only want to enable browser refreshes if this is a WebApp (ASP.NET Core / Blazor app).
            return projectGraph.GraphRoots.FirstOrDefault() is { } projectNode &&
                projectNode.ProjectInstance.GetItems("ProjectCapability").Any(p => p.EvaluatedInclude is "AspNetCore" or "WebAssembly");
        }

        public async ValueTask DisposeAsync()
        {
            if (_refreshServer != null)
            {
                await _refreshServer.DisposeAsync();
            }
        }
    }
}
