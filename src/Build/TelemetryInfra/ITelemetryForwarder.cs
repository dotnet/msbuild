// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd.Logging;

namespace Microsoft.Build.TelemetryInfra;

/// <summary>
/// A build component responsible for accumulating telemetry data from worker node and then sending it to main node
/// at the end of the build.
/// </summary>
internal interface ITelemetryForwarder
{
    bool IsTelemetryCollected { get; }

    void AddTask(
        string name,
        TimeSpan cumulativeExecutionTime,
        short executionsCount,
        long totalMemoryConsumed,
        bool isCustom,
        bool isFromNugetCache);

    /// <summary>
    /// Add info about target execution to the telemetry.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="wasExecuted">Means anytime, not necessarily from the last time target was added to telemetry</param>
    /// <param name="isCustom"></param>
    /// <param name="isMetaproj"></param>
    /// <param name="isFromNugetCache"></param>
    void AddTarget(string name, bool wasExecuted, bool isCustom, bool isMetaproj, bool isFromNugetCache);

    void FinalizeProcessing(LoggingContext loggingContext);
}
