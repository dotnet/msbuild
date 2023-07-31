// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using Microsoft.Build.Graph;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal sealed class ScopedCssFileHandler
    {
        private readonly IReporter _reporter;

        public ScopedCssFileHandler(IReporter reporter)
        {
            _reporter = reporter;
        }

        public async ValueTask<bool> TryHandleFileChange(DotNetWatchContext context, FileItem file, CancellationToken cancellationToken)
        {
            HotReloadEventSource.Log.HotReloadStart(HotReloadEventSource.StartType.ScopedCssHandler);
            if (!file.FilePath.EndsWith(".razor.css", StringComparison.Ordinal) &&
                !file.FilePath.EndsWith(".cshtml.css", StringComparison.Ordinal))
            {
                HotReloadEventSource.Log.HotReloadEnd(HotReloadEventSource.StartType.ScopedCssHandler);
                return default;
            }

            _reporter.Verbose($"Handling file change event for scoped css file {file.FilePath}.");
            if (!RebuildScopedCss(context.ProjectGraph!, file.ProjectPath))
            {
                HotReloadEventSource.Log.HotReloadEnd(HotReloadEventSource.StartType.ScopedCssHandler);
                return false;
            }
            await HandleBrowserRefresh(context.BrowserRefreshServer, file, cancellationToken);
            _reporter.Output("Hot reload of scoped css succeeded.", emoji: "🔥");
            HotReloadEventSource.Log.HotReloadEnd(HotReloadEventSource.StartType.ScopedCssHandler);
            return true;
        }

        private bool RebuildScopedCss(ProjectGraph projectGraph, string projectPath)
        {
            var project = projectGraph.ProjectNodesTopologicallySorted.FirstOrDefault(f => string.Equals(f.ProjectInstance.FullPath, projectPath, StringComparison.OrdinalIgnoreCase));
            if (project is null)
            {
                return false;
            }

            var projectInstance = project.ProjectInstance.DeepCopy();
            var logger = _reporter.IsVerbose ? new[] { new Build.Logging.ConsoleLogger() } : null;
            return projectInstance.Build("GenerateComputedBuildStaticWebAssets", logger);
        }

        private static async Task HandleBrowserRefresh(BrowserRefreshServer? browserRefreshServer, FileItem fileItem, CancellationToken cancellationToken)
        {
            if (browserRefreshServer is null)
            {
                return;
            }

            // We'd like an accurate scoped css path, but this needs a lot of work to wire-up now.
            // We'll handle this as part of https://github.com/dotnet/aspnetcore/issues/31217.
            // For now, we'll make it look like some css file which would cause JS to update a
            // single file if it's from the current project, or all locally hosted css files if it's a file from
            // referenced project.
            var cssFilePath = Path.GetFileNameWithoutExtension(fileItem.ProjectPath) + ".css";
            var message = new UpdateStaticFileMessage { Path = cssFilePath };
            await browserRefreshServer.SendJsonSerlialized(message, cancellationToken);
        }

        private readonly struct UpdateStaticFileMessage
        {
            public string Type => "UpdateStaticFile";

            public string Path { get; init; }
        }
    }
}
