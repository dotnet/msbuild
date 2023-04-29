// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Watch.Api;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal abstract class DeltaApplier : IDisposable
    {
        public abstract void Initialize(DotNetWatchContext context, CancellationToken cancellationToken);

        public abstract Task<ImmutableArray<string>> GetApplyUpdateCapabilitiesAsync(DotNetWatchContext context, CancellationToken cancellationToken);

        public abstract Task<ApplyStatus> Apply(DotNetWatchContext context, ImmutableArray<WatchHotReloadService.Update> updates, CancellationToken cancellationToken);

        public abstract void Dispose();
    }

    internal enum ApplyStatus
    {
        Failed = 0,
        AllChangesApplied = 1,
        SomeChangesApplied = 2,
        NoChangesApplied = 3,
    }
}
