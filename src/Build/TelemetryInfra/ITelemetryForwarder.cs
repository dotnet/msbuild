// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Telemetry;

namespace Microsoft.Build.TelemetryInfra;

/// <summary>
/// Collects task and target telemetry for a single build engine's lifetime.
/// Not thread-safe: the engine's one-active-builder-at-a-time invariant
/// guarantees single-threaded access to <see cref="AddTarget"/> and <see cref="AddTask"/>.
/// Created per engine by <see cref="TelemetryForwarderProvider.CreateForwarder"/>.
/// </summary>
internal interface ITelemetryForwarder
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
