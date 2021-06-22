// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Watch.Api;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal class BlazorWebAssemblyHostedDeltaApplier : IDeltaApplier
    {
        private readonly BlazorWebAssemblyDeltaApplier _wasmApplier;
        private readonly DefaultDeltaApplier _hostApplier;

        public BlazorWebAssemblyHostedDeltaApplier(IReporter reporter)
        {
            _wasmApplier = new BlazorWebAssemblyDeltaApplier(reporter);
            _hostApplier = new DefaultDeltaApplier(reporter);
        }

        public async ValueTask InitializeAsync(DotNetWatchContext context, CancellationToken cancellationToken)
        {
            await _wasmApplier.InitializeAsync(context, cancellationToken);
            await _hostApplier.InitializeAsync(context, cancellationToken);
        }
        
        public async ValueTask<bool> Apply(DotNetWatchContext context, string changedFile, ImmutableArray<WatchHotReloadService.Update> solutionUpdate, CancellationToken cancellationToken)
        {
            return await _hostApplier.Apply(context, changedFile, solutionUpdate, cancellationToken) &&
                await _wasmApplier.Apply(context, changedFile, solutionUpdate, cancellationToken);
        }
        
        public async ValueTask ReportDiagnosticsAsync(DotNetWatchContext context, IEnumerable<string> diagnostics, CancellationToken cancellationToken)
        {
            // Both WASM and Host have similar implementations for diagnostics. We could pick either to report diagnostics.
            await _hostApplier.ReportDiagnosticsAsync(context, diagnostics, cancellationToken);
        }
        
        public void Dispose()
        {
            _hostApplier.Dispose();
            _wasmApplier.Dispose();
        }
    }
}
