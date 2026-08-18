// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Telemetry;

namespace Microsoft.Build.TelemetryInfra;

/// <summary>
/// Collects task and target telemetry for a single BuildRequestEngine's lifetime.
/// Not thread-safe: only one RequestBuilder is active at a time per
/// BuildRequestEngine, so <see cref="AddTarget"/> and <see cref="AddTask"/>
/// are always called from a single thread.
/// Created per BuildRequestEngine by <see cref="TelemetryCollectorProvider.CreateCollector"/>.
/// </summary>
internal interface ITelemetryCollector
{
    bool IsTelemetryCollected { get; }

    void AddTarget(TaskOrTargetTelemetryKey key, bool wasExecuted, TargetSkipReason skipReason = TargetSkipReason.None);

    void AddTask(
        TaskOrTargetTelemetryKey key,
        TimeSpan cumulativeExecutionTime,
        int executionsCount,
        long totalMemoryConsumed,
        string? taskFactoryName,
        string? taskHostRuntime);

    /// <summary>
    /// Sends accumulated telemetry as a <see cref="WorkerNodeTelemetryEventArgs"/> and resets for the next build.
    /// </summary>
    void FinalizeProcessing(LoggingContext loggingContext);
}
