// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO.Pipes;
using Microsoft.CodeAnalysis.ExternalAccess.Watch.Api;
using Microsoft.Extensions.HotReload;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal sealed class DefaultDeltaApplier : SingleProcessDeltaApplier
    {
        private static readonly string _namedPipeName = Guid.NewGuid().ToString();
        private readonly IReporter _reporter;
        private Task<ImmutableArray<string>>? _capabilitiesTask;
        private NamedPipeServerStream? _pipe;

        public DefaultDeltaApplier(IReporter reporter)
        {
            _reporter = reporter;
        }

        internal bool SuppressNamedPipeForTests { get; set; }

        public override void Initialize(DotNetWatchContext context, CancellationToken cancellationToken)
        {
            Debug.Assert(context.ProcessSpec != null);

            base.Initialize(context, cancellationToken);

            if (!SuppressNamedPipeForTests)
            {
                _pipe = new NamedPipeServerStream(_namedPipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                _capabilitiesTask = Task.Run(async () =>
                {
                    _reporter.Verbose($"Connecting to the application.");

                    await _pipe.WaitForConnectionAsync(cancellationToken);

                    // When the client connects, the first payload it sends is the initialization payload which includes the apply capabilities.

                    var capabilities = ClientInitializationPayload.Read(_pipe).Capabilities;
                    return capabilities.Split(' ').ToImmutableArray();
                });
            }

            if (context.Iteration == 0)
            {
                var deltaApplier = Path.Combine(AppContext.BaseDirectory, "hotreload", "Microsoft.Extensions.DotNetDeltaApplier.dll");
                context.ProcessSpec.EnvironmentVariables.DotNetStartupHooks.Add(deltaApplier);

                // Configure the app for EnC
                context.ProcessSpec.EnvironmentVariables["DOTNET_MODIFIABLE_ASSEMBLIES"] = "debug";
                context.ProcessSpec.EnvironmentVariables["DOTNET_HOTRELOAD_NAMEDPIPE_NAME"] = _namedPipeName;
            }
        }

        public override Task<ImmutableArray<string>> GetApplyUpdateCapabilitiesAsync(DotNetWatchContext context, CancellationToken cancellationToken)
            => _capabilitiesTask ?? Task.FromResult(ImmutableArray<string>.Empty);

        public override async Task<ApplyStatus> Apply(DotNetWatchContext context, ImmutableArray<WatchHotReloadService.Update> updates, CancellationToken cancellationToken)
        {
            if (_capabilitiesTask is null || !_capabilitiesTask.IsCompletedSuccessfully || _pipe is null || !_pipe.IsConnected)
            {
                // The client isn't listening
                _reporter.Verbose("No client connected to receive delta updates.");
                return ApplyStatus.Failed;
            }

            var applicableUpdates = await FilterApplicableUpdatesAsync(context, updates, cancellationToken);
            if (applicableUpdates.Count == 0)
            {
                return ApplyStatus.NoChangesApplied;
            }

            var payload = new UpdatePayload(applicableUpdates.Select(update => new UpdateDelta(
                update.ModuleId,
                metadataDelta: update.MetadataDelta.ToArray(),
                ilDelta: update.ILDelta.ToArray(),
                update.UpdatedTypes.ToArray())).ToArray());

            await payload.WriteAsync(_pipe, cancellationToken);
            await _pipe.FlushAsync(cancellationToken);

            if (!await ReceiveApplyUpdateResult(cancellationToken))
            {
                return ApplyStatus.Failed;
            }

            return (applicableUpdates.Count < updates.Length) ? ApplyStatus.SomeChangesApplied : ApplyStatus.AllChangesApplied;
        }

        private async Task<bool> ReceiveApplyUpdateResult(CancellationToken cancellationToken)
        {
            Debug.Assert(_pipe != null);

            var bytes = ArrayPool<byte>.Shared.Rent(1);
            try
            {
                var numBytes = await _pipe.ReadAsync(bytes, cancellationToken);
                if (numBytes != 1)
                {
                    _reporter.Verbose($"Apply confirmation: Received {numBytes} bytes.");
                    return false;
                }

                if (bytes[0] != UpdatePayload.ApplySuccessValue)
                {
                    _reporter.Verbose($"Apply confirmation: Received value: '{bytes[0]}'.");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                // Log it, but we'll treat this as a failed apply.
                _reporter.Verbose(ex.Message);
                return false;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bytes);
            }
        }

        public override void Dispose()
        {
            _pipe?.Dispose();
        }
    }
}
