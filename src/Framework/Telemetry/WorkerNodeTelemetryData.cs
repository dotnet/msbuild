// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Framework;

internal class WorkerNodeTelemetryData : IWorkerNodeTelemetryData
{
    // Indicate custom targets/task - those must be hashed.
    public const string CustomPrefix = "C:";
    // Indicate targets/tasks sourced from nuget cache - those can be custom or MSFT provided ones.
    public const string FromNugetPrefix = "N:";
    // Indicate targets/tasks generated during build - those must be hashed (as they contain paths).
    public const string MetaProjPrefix = "M:";

    public WorkerNodeTelemetryData(Dictionary<string, TaskExecutionStats> tasksExecutionData, Dictionary<string, bool> targetsExecutionData)
    {
        TasksExecutionData = tasksExecutionData;
        TargetsExecutionData = targetsExecutionData;
    }

    public void Add(IWorkerNodeTelemetryData other)
    {
        foreach (var task in other.TasksExecutionData)
        {
            AddTask(task.Key, task.Value.CumulativeExecutionTime, task.Value.ExecutionsCount, task.Value.TotalMemoryConsumption);
        }

        foreach (var target in other.TargetsExecutionData)
        {
            AddTarget(target.Key, target.Value);
        }
    }

    public void AddTask(string name, TimeSpan cumulativeExectionTime, short executionsCount, long totalMemoryConsumption)
    {
        TaskExecutionStats? taskExecutionStats;
        if (!TasksExecutionData.TryGetValue(name, out taskExecutionStats))
        {
            taskExecutionStats = new(cumulativeExectionTime, executionsCount, totalMemoryConsumption);
            TasksExecutionData[name] = taskExecutionStats;
        }
        else
        {
            taskExecutionStats.CumulativeExecutionTime += cumulativeExectionTime;
            taskExecutionStats.ExecutionsCount += executionsCount;
            taskExecutionStats.TotalMemoryConsumption += totalMemoryConsumption;
        }
    }

    public void AddTarget(string name, bool wasExecuted)
    {
        TargetsExecutionData[name] =
            // we just need to store if it was ever executed
            wasExecuted || (TargetsExecutionData.TryGetValue(name, out bool wasAlreadyExecuted) && wasAlreadyExecuted);
    }

    public WorkerNodeTelemetryData()
        : this(new Dictionary<string, TaskExecutionStats>(StringComparer.OrdinalIgnoreCase), new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase))
    { }

    public Dictionary<string, TaskExecutionStats> TasksExecutionData { get; }
    public Dictionary<string, bool> TargetsExecutionData { get; }
}
