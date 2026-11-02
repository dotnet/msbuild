// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Telemetry;

namespace Microsoft.Build.TelemetryInfra;

/// <summary>
/// Collects task and target telemetry for an isolated processing scope.
/// Not thread-safe: each collector must be used from a single execution path.
/// Created by <see cref="TelemetryCollectorProvider.CreateCollector"/>.
/// </summary>
internal interface ITelemetryCollector
{
    /// <summary>
    /// Gets whether this collector records telemetry.
    /// </summary>
    bool IsTelemetryCollected { get; }

    /// <summary>
    /// Adds target detail information.
    /// </summary>
    void AddTarget(TaskOrTargetTelemetryKey key, bool wasExecuted, TargetSkipReason skipReason = TargetSkipReason.None);

    /// <summary>
    /// Adds one target outcome to the aggregate target counters.
    /// </summary>
    void AddTargetResult(bool wasExecuted, TargetSkipReason skipReason = TargetSkipReason.None);

    /// <summary>
    /// Adds one target with both Inputs and Outputs to the incrementality counters.
    /// </summary>
    void AddInputOutputTarget(bool upToDate);

    /// <summary>
    /// Adds task execution statistics.
    /// </summary>
    void AddTask(
        TaskOrTargetTelemetryKey key,
        TimeSpan cumulativeExecutionTime,
        int executionsCount,
        long? totalMemoryConsumed,
        string? taskFactoryName,
        string? taskHostRuntime);

    /// <summary>
    /// Sends accumulated telemetry as a <see cref="WorkerNodeTelemetryEventArgs"/> and resets for the next build.
    /// </summary>
    void FinalizeProcessing(LoggingContext loggingContext);
}
