// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.DotNetWatch;

namespace Microsoft.DotNet.Watcher.Tools
{
    interface IDeltaApplier : IDisposable
    {
        ValueTask InitializeAsync(DotNetWatchContext context, CancellationToken cancellationToken);

        ValueTask<bool> Apply(DotNetWatchContext context, string changedFile, DotNetWatchManagedModuleUpdatesWrapper? solutionUpdate, CancellationToken cancellationToken);

        ValueTask ReportDiagnosticsAsync(DotNetWatchContext context, IEnumerable<string> diagnostics, CancellationToken cancellationToken);
    }
}
