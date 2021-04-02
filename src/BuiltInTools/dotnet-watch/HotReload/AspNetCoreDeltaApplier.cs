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
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal class AspNetCoreDeltaApplier : IDeltaApplier
    {
        private readonly IReporter _reporter;
        private Task _task;
        private NamedPipeServerStream _pipe;

        public AspNetCoreDeltaApplier(IReporter reporter)
        {
            _reporter = reporter;
        }

        public bool SuppressBrowserRefreshAfterApply { get; init; }

        public async ValueTask InitializeAsync(DotNetWatchContext context, CancellationToken cancellationToken)
        {
            if (_pipe is not null)
            {
                _pipe.Close();
                await _pipe.DisposeAsync();
            }

            _pipe = new NamedPipeServerStream("netcore-hot-reload", PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            _task = _pipe.WaitForConnectionAsync(cancellationToken);

            if (context.Iteration == 0)
            {
                var deltaApplier = Path.Combine(AppContext.BaseDirectory, "hotreload", "Microsoft.Extensions.AspNetCoreDeltaApplier.dll");
                context.ProcessSpec.EnvironmentVariables.DotNetStartupHooks.Add(deltaApplier);

                // Configure the app for EnC
                context.ProcessSpec.EnvironmentVariables["DOTNET_MODIFIABLE_ASSEMBLIES"] = "debug";
            }
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
                if (result == ApplyResult.Success_RefreshBrowser)
                {
                    await context.BrowserRefreshServer.ReloadAsync(cancellationToken);
                }
                else if (result == ApplyResult.Success)
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
