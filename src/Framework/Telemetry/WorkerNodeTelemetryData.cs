// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Framework.Telemetry;

internal class WorkerNodeTelemetryData : IWorkerNodeTelemetryData
{
    public WorkerNodeTelemetryData(Dictionary<TaskOrTargetTelemetryKey, TaskExecutionStats> tasksExecutionData, Dictionary<TaskOrTargetTelemetryKey, TargetExecutionStats> targetsExecutionData)
    {
        TasksExecutionData = tasksExecutionData;
        TargetsExecutionData = targetsExecutionData;
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
    }

    /// <summary>
    /// Adds or aggregates task execution data.
    /// </summary>
    public void AddTask(TaskOrTargetTelemetryKey task, TimeSpan cumulativeExecutionTime, int executionsCount, long totalMemoryConsumption, string? factoryName, string? taskHostRuntime)
    {
        if (!TasksExecutionData.TryGetValue(task, out TaskExecutionStats? taskExecutionStats))
        {
            taskExecutionStats = new(cumulativeExecutionTime, executionsCount, totalMemoryConsumption, factoryName, taskHostRuntime);
            TasksExecutionData[task] = taskExecutionStats;
        }
        else
        {
            taskExecutionStats.CumulativeExecutionTime += cumulativeExecutionTime;
            taskExecutionStats.ExecutionsCount += executionsCount;
            taskExecutionStats.TotalMemoryBytes += totalMemoryConsumption;
            taskExecutionStats.TaskFactoryName ??= factoryName;
            taskExecutionStats.TaskHostRuntime ??= taskHostRuntime;
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

    public bool IsEmpty => TasksExecutionData.Count == 0 && TargetsExecutionData.Count == 0;

    public Dictionary<TaskOrTargetTelemetryKey, TaskExecutionStats> TasksExecutionData { get; }

    public Dictionary<TaskOrTargetTelemetryKey, TargetExecutionStats> TargetsExecutionData { get; }
}
