// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Logging;

namespace Microsoft.Build.CommandLine.UnitTests;

internal sealed class MockStopwatch : StopwatchAbstraction
{
    public override double ElapsedSeconds
    {
        get
        {
            return _elapsed;
        }
    }

    public override void Start()
    {
        IsStarted = true;
        Tick();
    }

    public override void Stop() => IsStarted = false;

    public bool IsStarted { get; private set; }

    private double _elapsed = 0d;

    public void Tick(double seconds = 0.1)
    {
        _elapsed += seconds;
    }
}
