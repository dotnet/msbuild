// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Build.Framework.Telemetry;

internal interface IWorkerNodeTelemetryData
{
    Dictionary<TaskOrTargetTelemetryKey, TaskExecutionStats> TasksExecutionData { get; }

    Dictionary<TaskOrTargetTelemetryKey, TargetExecutionStats> TargetsExecutionData { get; }
}

/// <summary>
/// Represents the execution statistics of a target.
/// </summary>
/// <param name="wasExecuted">Whether the target was executed (not skipped).</param>
/// <param name="skipReason">The reason the target was skipped, if applicable.</param>
internal readonly struct TargetExecutionStats(bool wasExecuted, TargetSkipReason skipReason = TargetSkipReason.None)
{
    /// <summary>
    /// Whether the target was executed (not skipped).
    /// </summary>
    public bool WasExecuted { get; } = wasExecuted;

    /// <summary>
    /// The reason the target was skipped. Only meaningful when <see cref="WasExecuted"/> is false.
    /// </summary>
    public TargetSkipReason SkipReason { get; } = skipReason;

    /// <summary>
    /// Creates stats for an executed target.
    /// </summary>
    public static TargetExecutionStats Executed() => new(wasExecuted: true);

    /// <summary>
    /// Creates stats for a skipped target with the given reason.
    /// </summary>
    public static TargetExecutionStats Skipped(TargetSkipReason reason) => new(wasExecuted: false, skipReason: reason);
}
