// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.Build.Logging.TerminalLogger;

internal sealed class StaticTimeStopwatch : StopwatchAbstraction
{
    public StaticTimeStopwatch(double elapsedMilliseconds)
    {
        ElapsedSeconds = elapsedMilliseconds / 1000;
    }

    public override double ElapsedSeconds { get; }

    public override void Start() => throw new System.NotSupportedException();
    public override void Stop() => throw new System.NotSupportedException();
}
