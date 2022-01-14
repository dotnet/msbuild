// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal sealed class StaticFileHandler
    {
        private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        private readonly IReporter _reporter;

        public StaticFileHandler(IReporter reporter)
        {
            _reporter = reporter;
        }

        public async ValueTask<bool> TryHandleFileChange(DotNetWatchContext context, FileItem file, CancellationToken cancellationToken)
        {
            HotReloadEventSource.Log.HotReloadStart(HotReloadEventSource.StartType.StaticHandler);
            if (!file.IsStaticFile || context.BrowserRefreshServer is null)
            {
                HotReloadEventSource.Log.HotReloadEnd(HotReloadEventSource.StartType.StaticHandler);
                return false;
            }
            _reporter.Verbose($"Handling file change event for static content {file.FilePath}.");
            await HandleBrowserRefresh(context.BrowserRefreshServer, file, cancellationToken);
            HotReloadEventSource.Log.HotReloadEnd(HotReloadEventSource.StartType.StaticHandler);
            _reporter.Output("Hot reload of static file succeeded.");
            return true;
        }

        private static async Task HandleBrowserRefresh(BrowserRefreshServer browserRefreshServer, FileItem fileItem, CancellationToken cancellationToken)
        {
            var message = JsonSerializer.SerializeToUtf8Bytes(new UpdateStaticFileMessage { Path = fileItem.StaticWebAssetPath }, JsonSerializerOptions);
            await browserRefreshServer.SendMessage(message, cancellationToken);
        }

        private readonly struct UpdateStaticFileMessage
        {
            public string Type => "UpdateStaticFile";

            public string Path { get; init; }
        }
    }
}
