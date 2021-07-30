// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Watcher.Internal;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    public class DotNetBuildFilter : IWatchFilter
    {
        private readonly string _muxer = DotnetMuxer.MuxerPath;
        private readonly ProcessRunner _processRunner;
        private readonly IReporter _reporter;

        public DotNetBuildFilter(ProcessRunner processRunner, IReporter reporter)
        {
            _processRunner = processRunner;
            _reporter = reporter;
        }

        public async ValueTask ProcessAsync(DotNetWatchContext context, CancellationToken cancellationToken)
        {
            using var fileSetWatcher = new FileSetWatcher(context.FileSet, _reporter);
            while (!cancellationToken.IsCancellationRequested)
            {
                var arguments = context.RequiresMSBuildRevaluation ?
                   new[] { "msbuild", "/t:Build", "/restore", "/nologo" } :
                   new[] { "msbuild", "/t:Build", "/nologo" };

                var processSpec = new ProcessSpec
                {
                    Executable = _muxer,
                    Arguments = arguments,
                    WorkingDirectory = context.ProcessSpec.WorkingDirectory,
                };

                _reporter.Output("Building...");
                var exitCode = await _processRunner.RunAsync(processSpec, cancellationToken);
                if (exitCode == 0)
                {
                    return;
                }

                // If the build fails, we'll retry until we have a successful build.
                await fileSetWatcher.GetChangedFileAsync(cancellationToken, () => _reporter.Warn("Waiting for a file to change before restarting dotnet..."));
            }
        }
    }
}
