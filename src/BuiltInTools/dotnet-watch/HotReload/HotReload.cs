// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Watcher.Internal;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal class HotReload : IDisposable
    {
        private readonly StaticFileHandler _staticFileHandler;
        private readonly ScopedCssFileHandler _scopedCssFileHandler;
        private readonly CompilationHandler _compilationHandler;

        public HotReload(ProcessRunner processRunner, IReporter reporter)
        {
            _staticFileHandler = new StaticFileHandler(reporter);
            _scopedCssFileHandler = new ScopedCssFileHandler(processRunner, reporter);
            _compilationHandler = new CompilationHandler(reporter);
        }

        public async ValueTask InitializeAsync(DotNetWatchContext dotNetWatchContext, CancellationToken cancellationToken)
        {
            await _compilationHandler.InitializeAsync(dotNetWatchContext, cancellationToken);
        }

        public async ValueTask<bool> TryHandleFileChange(DotNetWatchContext context, FileItem[] files, CancellationToken cancellationToken)
        {
            HotReloadEventSource.Log.HotReloadStart(HotReloadEventSource.StartType.Main);

            var fileHandlerResult = false;
            for (var i = files.Length - 1; i >= 0; i--)
            {
                var file = files[i];
                if (await _staticFileHandler.TryHandleFileChange(context, file, cancellationToken) ||
                    await _scopedCssFileHandler.TryHandleFileChange(context, file, cancellationToken))
                {
                    fileHandlerResult = true;
                }
            }

            fileHandlerResult |= await _compilationHandler.TryHandleFileChange(context, files, cancellationToken);

            HotReloadEventSource.Log.HotReloadEnd(HotReloadEventSource.StartType.Main);
            return fileHandlerResult;
        }

        public void Dispose()
        {
            _compilationHandler.Dispose();
        }
    }
}
