// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Http.Headers;
using Microsoft.Build.Logging;

namespace Microsoft.Build.CommandLine.UnitTests;

/// <summary>
/// Stopwatch that always show the time provided in constructor.
/// </summary>
internal sealed class StaticStopwatch : StopwatchAbstraction
{
    public StaticStopwatch(double elapsedSeconds)
    {
        ElapsedSeconds = elapsedSeconds;
    }

    public override double ElapsedSeconds { get; }

    public override void Start() => throw new System.NotImplementedException();
    public override void Stop() => throw new System.NotImplementedException();
}
