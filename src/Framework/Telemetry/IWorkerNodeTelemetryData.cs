// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Build.Framework.Telemetry;

internal interface IWorkerNodeTelemetryData
{
    /// <summary>
    /// Gets task execution details keyed by task identity.
    /// </summary>
    Dictionary<TaskOrTargetTelemetryKey, TaskExecutionStats> TasksExecutionData { get; }

    /// <summary>
    /// Gets target details keyed by target identity.
    /// </summary>
    Dictionary<TaskOrTargetTelemetryKey, TargetExecutionStats> TargetsExecutionData { get; }

    /// <summary>
    /// Gets the number of target instances that ran.
    /// </summary>
    int ExecutedTargetsCount { get; }

    /// <summary>
    /// Gets the number of target instances that were skipped.
    /// </summary>
    int SkippedTargetsCount { get; }

    /// <summary>
    /// Gets the number of target instances skipped because their outputs were up to date.
    /// </summary>
    int SkippedDueToUpToDateCount { get; }

    /// <summary>
    /// Gets the number of target instances skipped because their condition was false.
    /// </summary>
    int SkippedDueToConditionCount { get; }

    /// <summary>
    /// Gets the number of target instances skipped because they were already built.
    /// </summary>
    int SkippedDueToPreviouslyBuiltCount { get; }

    /// <summary>
    /// Gets the number of target instances with Inputs and Outputs whose outputs were up to date.
    /// </summary>
    int UpToDateInputOutputTargetsCount { get; }

    /// <summary>
    /// Gets the number of target instances with Inputs and Outputs that ran.
    /// </summary>
    int ExecutedInputOutputTargetsCount { get; }
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
