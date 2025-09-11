// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Framework.Telemetry;

internal class WorkerNodeTelemetryData : IWorkerNodeTelemetryData
{
    public WorkerNodeTelemetryData(Dictionary<TaskOrTargetTelemetryKey, TaskExecutionStats> tasksExecutionData)
    {
        TasksExecutionData = tasksExecutionData;
    }

    public void Add(IWorkerNodeTelemetryData other)
    {
        foreach (var task in other.TasksExecutionData)
        {
            AddTask(task.Key, task.Value.CumulativeExecutionTime, task.Value.ExecutionsCount, task.Value.TotalMemoryBytes);
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

    public WorkerNodeTelemetryData()
        : this(new Dictionary<TaskOrTargetTelemetryKey, TaskExecutionStats>())
    { }

    public Dictionary<TaskOrTargetTelemetryKey, TaskExecutionStats> TasksExecutionData { get; }
}
