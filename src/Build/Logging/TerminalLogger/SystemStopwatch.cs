// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.Build.Logging;

internal sealed class SystemStopwatch : StopwatchAbstraction
{
    private Stopwatch _stopwatch = new();

    public override double ElapsedSeconds => _stopwatch.Elapsed.TotalSeconds;

    public override void Start() => _stopwatch.Start();
    public override void Stop() => _stopwatch.Stop();

    public static StopwatchAbstraction StartNew()
    {
        SystemStopwatch wallClockStopwatch = new();
        wallClockStopwatch.Start();

        return wallClockStopwatch;
    }
}
