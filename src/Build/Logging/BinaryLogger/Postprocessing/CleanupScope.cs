// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Logging;

internal readonly struct CleanupScope : IDisposable
{
    private readonly Action _disposeAction;

    public CleanupScope(Action disposeAction) => _disposeAction = disposeAction;

    public void Dispose() => _disposeAction();
}
