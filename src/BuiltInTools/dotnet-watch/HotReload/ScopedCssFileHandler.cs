// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Watcher.Internal;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal sealed class ScopedCssFileHandler
    {
        private static readonly string _muxerPath = DotnetMuxer.MuxerPath;
        private readonly ProcessRunner _processRunner;
        private readonly IReporter _reporter;

        public ScopedCssFileHandler(ProcessRunner processRunner, IReporter reporter)
        {
            _processRunner = processRunner;
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
            if (!await RebuildScopedCss(file.ProjectPath, cancellationToken))
            {
                HotReloadEventSource.Log.HotReloadEnd(HotReloadEventSource.StartType.ScopedCssHandler);
                return false;
            }
            await HandleBrowserRefresh(context.BrowserRefreshServer, file, cancellationToken);
            _reporter.Output("Hot reload of scoped css succeeded.");
            HotReloadEventSource.Log.HotReloadEnd(HotReloadEventSource.StartType.ScopedCssHandler);
            return true;
        }

        private async ValueTask<bool> RebuildScopedCss(string projectPath, CancellationToken cancellationToken)
        {
            var build = new ProcessSpec
            {
                Executable = _muxerPath,
                Arguments = new[] { "msbuild", "/nologo", "/t:_PrepareForScopedCss", projectPath, }
            };

            var result = await _processRunner.RunAsync(build, cancellationToken);
            return result == 0;
        }

        private static async Task HandleBrowserRefresh(BrowserRefreshServer browserRefreshServer, FileItem fileItem, CancellationToken cancellationToken)
        {
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
