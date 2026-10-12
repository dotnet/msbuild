// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Framework.Telemetry;

internal class WorkerNodeTelemetryData : IWorkerNodeTelemetryData
{
    public WorkerNodeTelemetryData(
        Dictionary<TaskOrTargetTelemetryKey, TaskExecutionStats> tasksExecutionData,
        Dictionary<TaskOrTargetTelemetryKey, TargetExecutionStats> targetsExecutionData,
        int upToDateInputOutputTargetsCount = 0,
        int executedInputOutputTargetsCount = 0,
        int executedTargetsCount = 0,
        int skippedTargetsCount = 0,
        int skippedDueToUpToDateCount = 0,
        int skippedDueToConditionCount = 0,
        int skippedDueToPreviouslyBuiltCount = 0)
    {
        TasksExecutionData = tasksExecutionData;
        TargetsExecutionData = targetsExecutionData;
        UpToDateInputOutputTargetsCount = upToDateInputOutputTargetsCount;
        ExecutedInputOutputTargetsCount = executedInputOutputTargetsCount;
        ExecutedTargetsCount = executedTargetsCount;
        SkippedTargetsCount = skippedTargetsCount;
        SkippedDueToUpToDateCount = skippedDueToUpToDateCount;
        SkippedDueToConditionCount = skippedDueToConditionCount;
        SkippedDueToPreviouslyBuiltCount = skippedDueToPreviouslyBuiltCount;
    }

    /// <summary>
    /// Merges all data from another <see cref="IWorkerNodeTelemetryData"/> into this instance.
    /// </summary>
    public void Add(IWorkerNodeTelemetryData other)
    {
        foreach (var task in other.TasksExecutionData)
        {
            AddTask(task.Key, task.Value.CumulativeExecutionTime, task.Value.ExecutionsCount, task.Value.TotalMemoryBytes, task.Value.TaskFactoryName, task.Value.TaskHostRuntime);
        }

        foreach (var target in other.TargetsExecutionData)
        {
            AddTarget(target.Key, target.Value.WasExecuted, target.Value.SkipReason);
        }

        UpToDateInputOutputTargetsCount += other.UpToDateInputOutputTargetsCount;
        ExecutedInputOutputTargetsCount += other.ExecutedInputOutputTargetsCount;
        ExecutedTargetsCount += other.ExecutedTargetsCount;
        SkippedTargetsCount += other.SkippedTargetsCount;
        SkippedDueToUpToDateCount += other.SkippedDueToUpToDateCount;
        SkippedDueToConditionCount += other.SkippedDueToConditionCount;
        SkippedDueToPreviouslyBuiltCount += other.SkippedDueToPreviouslyBuiltCount;
    }

    /// <summary>
    /// Adds or aggregates task execution data.
    /// </summary>
    public void AddTask(TaskOrTargetTelemetryKey task, TimeSpan cumulativeExecutionTime, int executionsCount, long? totalMemoryConsumption, string? factoryName, string? taskHostRuntime)
    {
        if (!TasksExecutionData.TryGetValue(task, out TaskExecutionStats? taskExecutionStats))
        {
            taskExecutionStats = new(cumulativeExecutionTime, executionsCount, totalMemoryConsumption, factoryName, taskHostRuntime);
            TasksExecutionData[task] = taskExecutionStats;
        }
        else
        {
            bool existingHasExecutions = taskExecutionStats.ExecutionsCount > 0;
            taskExecutionStats.CumulativeExecutionTime += cumulativeExecutionTime;
            taskExecutionStats.ExecutionsCount += executionsCount;
            if (totalMemoryConsumption.HasValue)
            {
                taskExecutionStats.TotalMemoryBytes = (taskExecutionStats.TotalMemoryBytes ?? 0) + totalMemoryConsumption.Value;
            }

            if (!existingHasExecutions && executionsCount > 0)
            {
                taskExecutionStats.TaskFactoryName = factoryName ?? taskExecutionStats.TaskFactoryName;
                taskExecutionStats.TaskHostRuntime = taskHostRuntime ?? taskExecutionStats.TaskHostRuntime;
            }
            else
            {
                taskExecutionStats.TaskFactoryName ??= factoryName;
                taskExecutionStats.TaskHostRuntime ??= taskHostRuntime;
            }
        }
    }

    /// <summary>
    /// Adds or updates target execution data.
    /// </summary>
    public void AddTarget(TaskOrTargetTelemetryKey target, bool wasExecuted, TargetSkipReason skipReason = TargetSkipReason.None)
    {
        if (TargetsExecutionData.TryGetValue(target, out var existingStats))
        {
            // If the target was ever executed, mark it as executed
            // Otherwise, keep the most informative skip reason (non-None preferred)
            if (wasExecuted || existingStats.WasExecuted)
            {
                TargetsExecutionData[target] = TargetExecutionStats.Executed();
            }
            else if (skipReason != TargetSkipReason.None)
            {
                TargetsExecutionData[target] = TargetExecutionStats.Skipped(skipReason);
            }
            // else keep existing stats
        }
        else
        {
            TargetsExecutionData[target] = wasExecuted
                ? TargetExecutionStats.Executed()
                : TargetExecutionStats.Skipped(skipReason);
        }
    }

    public WorkerNodeTelemetryData() : this([], []) { }

    public void AddTargetResult(bool wasExecuted, TargetSkipReason skipReason = TargetSkipReason.None)
    {
        if (wasExecuted)
        {
            ExecutedTargetsCount++;
            return;
        }

        SkippedTargetsCount++;
        switch (skipReason)
        {
            case TargetSkipReason.OutputsUpToDate:
                SkippedDueToUpToDateCount++;
                break;
            case TargetSkipReason.ConditionWasFalse:
                SkippedDueToConditionCount++;
                break;
            case TargetSkipReason.PreviouslyBuiltSuccessfully:
            case TargetSkipReason.PreviouslyBuiltUnsuccessfully:
                SkippedDueToPreviouslyBuiltCount++;
                break;
        }
    }

    public void AddInputOutputTarget(bool upToDate)
    {
        if (upToDate)
        {
            UpToDateInputOutputTargetsCount++;
        }
        else
        {
            ExecutedInputOutputTargetsCount++;
        }
    }

    public bool IsEmpty =>
        TasksExecutionData.Count == 0 &&
        TargetsExecutionData.Count == 0 &&
        ExecutedTargetsCount == 0 &&
        SkippedTargetsCount == 0 &&
        UpToDateInputOutputTargetsCount == 0 &&
        ExecutedInputOutputTargetsCount == 0;

    public Dictionary<TaskOrTargetTelemetryKey, TaskExecutionStats> TasksExecutionData { get; }

    public Dictionary<TaskOrTargetTelemetryKey, TargetExecutionStats> TargetsExecutionData { get; }

    public int ExecutedTargetsCount { get; private set; }

    public int SkippedTargetsCount { get; private set; }

    public int SkippedDueToUpToDateCount { get; private set; }

    public int SkippedDueToConditionCount { get; private set; }

    public int SkippedDueToPreviouslyBuiltCount { get; private set; }

    public int UpToDateInputOutputTargetsCount { get; private set; }

    public int ExecutedInputOutputTargetsCount { get; private set; }
}
