// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Collections.Immutable;
using Microsoft.CodeAnalysis.ExternalAccess.Watch.Api;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal sealed class BlazorWebAssemblyHostedDeltaApplier : DeltaApplier
    {
        private readonly BlazorWebAssemblyDeltaApplier _wasmApplier;
        private readonly DefaultDeltaApplier _hostApplier;

        public BlazorWebAssemblyHostedDeltaApplier(IReporter reporter)
        {
            _wasmApplier = new BlazorWebAssemblyDeltaApplier(reporter);
            _hostApplier = new DefaultDeltaApplier(reporter);
        }

        public override void Initialize(DotNetWatchContext context, CancellationToken cancellationToken)
        {
            _wasmApplier.Initialize(context, cancellationToken);
            _hostApplier.Initialize(context, cancellationToken);
        }

        public override async Task<ApplyStatus> Apply(DotNetWatchContext context, ImmutableArray<WatchHotReloadService.Update> updates, CancellationToken cancellationToken)
        {
            // Apply to both processes.
            // The module the change is for does not need to be loaded in either of the processes, yet we still consider it successful if the application does not fail.
            // In each process we store the deltas for application when/if the module is loaded to the process later.
            // An error is only reported if the delta application fails, which would be a bug either in the runtime (applying valid delta incorrectly),
            // the compiler (producing wrong delta), or rude edit detection (the change shouldn't have been allowed).

            var result = await Task.WhenAll(
                _wasmApplier.Apply(context, updates, cancellationToken),
                _hostApplier.Apply(context, updates, cancellationToken));

            var wasmResult = result[0];
            var hostResult = result[1];

            ReportStatus(context.Reporter, wasmResult, "client");
            ReportStatus(context.Reporter, hostResult, "host");

            return (wasmResult, hostResult) switch
            {
                (ApplyStatus.Failed, _) or (_, ApplyStatus.Failed) => ApplyStatus.Failed,
                (ApplyStatus.NoChangesApplied, ApplyStatus.NoChangesApplied) => ApplyStatus.NoChangesApplied,
                (ApplyStatus.AllChangesApplied, ApplyStatus.AllChangesApplied) => ApplyStatus.AllChangesApplied,
                _ => ApplyStatus.SomeChangesApplied,
            };

            static void ReportStatus(IReporter reporter, ApplyStatus status, string target)
            {
                if (status == ApplyStatus.NoChangesApplied)
                {
                    reporter.Warn($"No changes applied to {target} because they are not supported by the runtime.");
                }
                else if (status == ApplyStatus.SomeChangesApplied)
                {
                    reporter.Verbose($"Some changes not applied to {target} because they are not supported by the runtime.");
                }
            }
        }

        public override void Dispose()
        {
            _hostApplier.Dispose();
            _wasmApplier.Dispose();
        }

        public override async Task<ImmutableArray<string>> GetApplyUpdateCapabilitiesAsync(DotNetWatchContext context, CancellationToken cancellationToken)
        {
            var result = await Task.WhenAll(
                _wasmApplier.GetApplyUpdateCapabilitiesAsync(context, cancellationToken),
                _hostApplier.GetApplyUpdateCapabilitiesAsync(context, cancellationToken));

            // Allow updates that are supported by at least one process.
            // When applying changes we will filter updates applied to a specific process based on their required capabilities.
            return result[0].Union(result[1], StringComparer.OrdinalIgnoreCase).ToImmutableArray();
        }
    }
}
