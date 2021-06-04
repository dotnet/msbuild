// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Watch.Api;
using Microsoft.Extensions.HotReload;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal class DefaultDeltaApplier : IDeltaApplier
    {
        private static readonly string _namedPipeName = Guid.NewGuid().ToString();
        private readonly IReporter _reporter;
        private Task _task;
        private NamedPipeServerStream _pipe;
        private bool _refreshBrowserAfterFileChange;

        public DefaultDeltaApplier(IReporter reporter)
        {
            _reporter = reporter;
        }

        public bool SuppressBrowserRefreshAfterApply { get; init; }

        public ValueTask InitializeAsync(DotNetWatchContext context, CancellationToken cancellationToken)
        {
            _pipe = new NamedPipeServerStream(_namedPipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            _task = _pipe.WaitForConnectionAsync(cancellationToken);

            if (context.Iteration == 0)
            {
                var deltaApplier = Path.Combine(AppContext.BaseDirectory, "hotreload", "Microsoft.Extensions.DotNetDeltaApplier.dll");
                context.ProcessSpec.EnvironmentVariables.DotNetStartupHooks.Add(deltaApplier);

                // Configure the app for EnC
                context.ProcessSpec.EnvironmentVariables["DOTNET_MODIFIABLE_ASSEMBLIES"] = "debug";
                context.ProcessSpec.EnvironmentVariables["DOTNET_HOTRELOAD_NAMEDPIPE_NAME"] = _namedPipeName;

                // If there's any .razor file, we'll assume this is a blazor app and not cause a browser refresh.
                if (!SuppressBrowserRefreshAfterApply)
                {
                    _refreshBrowserAfterFileChange = !context.FileSet.Any(f => f.FilePath.EndsWith(".razor", StringComparison.Ordinal));
                }
            }

            return default;
        }

        public async ValueTask<bool> Apply(DotNetWatchContext context, string changedFile, ImmutableArray<WatchHotReloadService.Update> solutionUpdate, CancellationToken cancellationToken)
        {
            if (!_task.IsCompletedSuccessfully || !_pipe.IsConnected)
            {
                // The client isn't listening
                _reporter.Verbose("No client connected to receive delta updates.");
                return false;
            }

            var payload = new UpdatePayload
            {
                ChangedFile = changedFile,
                Deltas = ImmutableArray.CreateRange(solutionUpdate, c => new UpdateDelta
                {
                    ModuleId = c.ModuleId,
                    ILDelta = c.ILDelta.ToArray(),
                    MetadataDelta = c.MetadataDelta.ToArray(),
                }),
            };

            await payload.WriteAsync(_pipe, cancellationToken);
            await _pipe.FlushAsync(cancellationToken);

            var result = ApplyResult.Failed;
            var bytes = ArrayPool<byte>.Shared.Rent(1);
            try
            {
                var timeout =
#if DEBUG
                 Timeout.InfiniteTimeSpan;
#else
                 TimeSpan.FromSeconds(5);
#endif

                using var cancellationTokenSource = new CancellationTokenSource(timeout);
                var numBytes = await _pipe.ReadAsync(bytes, cancellationTokenSource.Token);

                if (numBytes == 1)
                {
                    result = (ApplyResult)bytes[0];
                }
            }
            catch (Exception ex)
            {
                // Log it, but we'll treat this as a failed apply.
                _reporter.Verbose(ex.Message);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bytes);
            }

            if (result == ApplyResult.Failed)
            {
                return false;
            }

            if (!SuppressBrowserRefreshAfterApply && context.BrowserRefreshServer is not null)
            {
                // For a Web app, we have the option of either letting the app update the UI or
                // refresh the browser. In general, for Blazor apps, we will choose not to refresh the UI
                // and for other apps we'll always refresh
                if (_refreshBrowserAfterFileChange)
                {
                    await context.BrowserRefreshServer.ReloadAsync(cancellationToken);
                }
                else
                {
                    await context.BrowserRefreshServer.SendJsonSerlialized(new HotReloadApplied());
                }
            }

            return true;
        }

        public async ValueTask ReportDiagnosticsAsync(DotNetWatchContext context, IEnumerable<string> diagnostics, CancellationToken cancellationToken)
        {
            if (context.BrowserRefreshServer != null)
            {
                var message = new HotReloadDiagnostics
                {
                    Diagnostics = diagnostics
                };

                await context.BrowserRefreshServer.SendJsonSerlialized(message, cancellationToken);
            }
        }

        public void Dispose()
        {
            _pipe?.Dispose();
        }

        public readonly struct HotReloadDiagnostics
        {
            public string Type => "HotReloadDiagnosticsv1";

            public IEnumerable<string> Diagnostics { get; init; }
        }

        public readonly struct HotReloadApplied
        {
            public string Type => "HotReloadApplied";
        }
    }
}
