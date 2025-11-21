// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Framework.Telemetry;

internal class WorkerNodeTelemetryData : IWorkerNodeTelemetryData
{
    public WorkerNodeTelemetryData(Dictionary<TaskOrTargetTelemetryKey, TaskExecutionStats> tasksExecutionData, Dictionary<TaskOrTargetTelemetryKey, bool> targetsExecutionData)
    {
        TasksExecutionData = tasksExecutionData;
        TargetsExecutionData = targetsExecutionData;
    }

    public void Add(IWorkerNodeTelemetryData other)
    {
        foreach (var task in other.TasksExecutionData)
        {
            AddTask(task.Key, task.Value.CumulativeExecutionTime, task.Value.ExecutionsCount, task.Value.TotalMemoryBytes);
        }

        foreach (var target in other.TargetsExecutionData)
        {
            AddTarget(target.Key, target.Value);
        }
    }

    public void AddTask(TaskOrTargetTelemetryKey task, TimeSpan cumulativeExectionTime, int executionsCount, long totalMemoryConsumption)
    {
        TaskExecutionStats? taskExecutionStats;
        if (!TasksExecutionData.TryGetValue(task, out taskExecutionStats))
        {
            taskExecutionStats = new(cumulativeExectionTime, executionsCount, totalMemoryConsumption);
            TasksExecutionData[task] = taskExecutionStats;
        }
        else
        {
            taskExecutionStats.CumulativeExecutionTime += cumulativeExectionTime;
            taskExecutionStats.ExecutionsCount += executionsCount;
            taskExecutionStats.TotalMemoryBytes += totalMemoryConsumption;
        }
    }

    public void AddTarget(TaskOrTargetTelemetryKey target, bool wasExecuted)
    {
        TargetsExecutionData[target] =
            // we just need to store if it was ever executed
            wasExecuted || (TargetsExecutionData.TryGetValue(target, out bool wasAlreadyExecuted) && wasAlreadyExecuted);
    }

    public WorkerNodeTelemetryData()
        : this(new Dictionary<TaskOrTargetTelemetryKey, TaskExecutionStats>(), new Dictionary<TaskOrTargetTelemetryKey, bool>())
    { }

    public Dictionary<TaskOrTargetTelemetryKey, TaskExecutionStats> TasksExecutionData { get; }
    public Dictionary<TaskOrTargetTelemetryKey, bool> TargetsExecutionData { get; }
}
