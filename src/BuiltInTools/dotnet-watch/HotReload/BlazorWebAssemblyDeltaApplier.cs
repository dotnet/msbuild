// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Watch.Api;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal sealed class BlazorWebAssemblyDeltaApplier : SingleProcessDeltaApplier
    {
        private static Task<ImmutableArray<string>>? s_cachedCapabilties;
        private readonly IReporter _reporter;
        private int _sequenceId;

        public BlazorWebAssemblyDeltaApplier(IReporter reporter)
        {
            _reporter = reporter;
        }

        public override void Initialize(DotNetWatchContext context, CancellationToken cancellationToken)
        {
            Debug.Assert(context.ProcessSpec != null);

            base.Initialize(context, cancellationToken);

            // Configure the app for EnC
            context.ProcessSpec.EnvironmentVariables["DOTNET_MODIFIABLE_ASSEMBLIES"] = "debug";
        }

        public override Task<ImmutableArray<string>> GetApplyUpdateCapabilitiesAsync(DotNetWatchContext context, CancellationToken cancellationToken)
        {
            return s_cachedCapabilties ??= GetApplyUpdateCapabilitiesCoreAsync();

            async Task<ImmutableArray<string>> GetApplyUpdateCapabilitiesCoreAsync()
            {
                if (context.BrowserRefreshServer is null)
                {
                    throw new ApplicationException("The browser refresh server is unavailable.");
                }

                _reporter.Verbose("Connecting to the browser.");

                await context.BrowserRefreshServer.WaitForClientConnectionAsync(cancellationToken);
                await context.BrowserRefreshServer.SendJsonSerlialized(default(BlazorRequestApplyUpdateCapabilities), cancellationToken);

                var buffer = ArrayPool<byte>.Shared.Rent(32 * 1024);
                try
                {
                    // We'll query the browser and ask it send capabilities.
                    var response = await context.BrowserRefreshServer.ReceiveAsync(buffer, cancellationToken);
                    if (!response.HasValue || !response.Value.EndOfMessage || response.Value.MessageType != WebSocketMessageType.Text)
                    {
                        throw new ApplicationException("Unable to connect to the browser refresh server.");
                    }

                    var capabilities = Encoding.UTF8.GetString(buffer.AsSpan(0, response.Value.Count));

                    // Capabilities are expressed a space-separated string.
                    // e.g. https://github.com/dotnet/runtime/blob/14343bdc281102bf6fffa1ecdd920221d46761bc/src/coreclr/System.Private.CoreLib/src/System/Reflection/Metadata/AssemblyExtensions.cs#L87
                    return capabilities.Split(' ').ToImmutableArray();
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }

        public override async Task<ApplyStatus> Apply(DotNetWatchContext context, ImmutableArray<WatchHotReloadService.Update> updates, CancellationToken cancellationToken)
        {
            if (context.BrowserRefreshServer is null)
            {
                _reporter.Verbose("Unable to send deltas because the browser refresh server is unavailable.");
                return ApplyStatus.Failed;
            }

            var applicableUpdates = await FilterApplicableUpdatesAsync(context, updates, cancellationToken);
            if (applicableUpdates.Count == 0)
            {
                return ApplyStatus.NoChangesApplied;
            }

            await context.BrowserRefreshServer.SendJsonWithSecret(sharedSecret => new UpdatePayload
            {
                SharedSecret = sharedSecret,
                Deltas = updates.Select(update => new UpdateDelta
                {
                    SequenceId = _sequenceId++,
                    ModuleId = update.ModuleId,
                    MetadataDelta = update.MetadataDelta.ToArray(),
                    ILDelta = update.ILDelta.ToArray(),
                    UpdatedTypes = update.UpdatedTypes.ToArray(),
                })
            }, cancellationToken);

            bool result = await ReceiveApplyUpdateResult(context.BrowserRefreshServer, cancellationToken);

            return !result ? ApplyStatus.Failed : (applicableUpdates.Count < updates.Length) ? ApplyStatus.SomeChangesApplied : ApplyStatus.AllChangesApplied;
        }

        private async Task<bool> ReceiveApplyUpdateResult(BrowserRefreshServer browserRefresh, CancellationToken cancellationToken)
        {
            var _receiveBuffer = new byte[1];
            var result = await browserRefresh.ReceiveAsync(_receiveBuffer, cancellationToken);
            if (result is null)
            {
                // A null result indicates no clients are connected. No deltas could have been applied in this state.
                _reporter.Verbose("Apply confirmation: No browser is connected");
                return false;
            }

            if (IsDeltaReceivedMessage(result.Value))
            {
                // 1 indicates success.
                return _receiveBuffer[0] == 1;
            }

            return false;

            bool IsDeltaReceivedMessage(ValueWebSocketReceiveResult result)
            {
                _reporter.Verbose($"Apply confirmation: Received {_receiveBuffer[0]} from browser in [Count: {result.Count}, MessageType: {result.MessageType}, EndOfMessage: {result.EndOfMessage}].");
                return result.Count == 1 // Should have received 1 byte on the socket for the acknowledgement
                    && result.MessageType is WebSocketMessageType.Binary
                    && result.EndOfMessage;
            }
        }

        public override void Dispose()
        {
            // Do nothing.
        }

        private readonly struct UpdatePayload
        {
            public string Type => "BlazorHotReloadDeltav1";
            public string? SharedSecret { get; init; }
            public IEnumerable<UpdateDelta> Deltas { get; init; }
        }

        private readonly struct UpdateDelta
        {
            public int SequenceId { get; init; }
            public string ServerId { get; init; }
            public Guid ModuleId { get; init; }
            public byte[] MetadataDelta { get; init; }
            public byte[] ILDelta { get; init; }
            public int[] UpdatedTypes { get; init; }
        }

        private readonly struct BlazorRequestApplyUpdateCapabilities
        {
            public string Type => "BlazorRequestApplyUpdateCapabilities";
        }
    }
}
