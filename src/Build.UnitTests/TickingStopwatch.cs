// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Logging;

namespace Microsoft.Build.CommandLine.UnitTests;

/// <summary>
/// Stopwatch that will increase by 0.1, every time you ask them for time. Useful for animations, because they check that NodeStatus
/// reference stays the same, and also for ensuring we are grabbing the time only once per frame.
/// </summary>
internal sealed class TickingStopwatch : StopwatchAbstraction
{
    private double _elapsedSeconds;

    public TickingStopwatch(double elapsedSeconds = 0.0)
    {
        _elapsedSeconds = elapsedSeconds;
    }

    public override double ElapsedSeconds
    {
        get
        {
            var elapsed = _elapsedSeconds;
            _elapsedSeconds += 0.1;
            return elapsed;
        }
    }
    public override void Start() => throw new System.NotImplementedException();
    public override void Stop() => throw new System.NotImplementedException();
}
