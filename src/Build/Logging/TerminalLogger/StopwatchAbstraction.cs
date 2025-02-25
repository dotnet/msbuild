// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Logging;

internal abstract class StopwatchAbstraction
{
    public abstract void Start();
    public abstract void Stop();

    public abstract double ElapsedSeconds { get; }
}
