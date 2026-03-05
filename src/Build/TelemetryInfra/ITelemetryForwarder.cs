// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;

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
        bool isFromNugetCache,
        string? taskFactoryName,
        string? taskHostRuntime);

    /// <summary>
    /// Add info about target execution to the telemetry.
    /// </summary>
    /// <param name="name">The target name.</param>
    /// <param name="wasExecuted">Whether the target was executed (not skipped).</param>
    /// <param name="isCustom">Whether this is a custom target.</param>
    /// <param name="isMetaproj">Whether the target is from a meta project.</param>
    /// <param name="isFromNugetCache">Whether the target is from a NuGet package.</param>
    /// <param name="skipReason">The reason the target was skipped, if applicable.</param>
    void AddTarget(string name, bool wasExecuted, bool isCustom, bool isMetaproj, bool isFromNugetCache, TargetSkipReason skipReason = TargetSkipReason.None);

    void FinalizeProcessing(LoggingContext loggingContext);
}
