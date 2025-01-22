// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Framework;


internal struct TaskExecutionStats(TimeSpan cumulativeExecutionTime, short executionsCount)
{
    public TimeSpan CumulativeExecutionTime { get; set; } = cumulativeExecutionTime;
    public short ExecutionsCount { get; set; } = executionsCount;
}

internal interface IWorkerNodeTelemetryData
{
    Dictionary<string, TaskExecutionStats> TasksExecutionData { get; }
    Dictionary<string, bool> TargetsExecutionData { get; }
}

internal class WorkerNodeTelemetryData : IWorkerNodeTelemetryData
{
    public WorkerNodeTelemetryData(Dictionary<string, TaskExecutionStats> tasksExecutionData, Dictionary<string, bool> targetsExecutionData)
    {
        TasksExecutionData = tasksExecutionData;
        TargetsExecutionData = targetsExecutionData;
    }

    public WorkerNodeTelemetryData()
        : this([], [])
    { }

    public Dictionary<string, TaskExecutionStats> TasksExecutionData { get; private init; }
    public Dictionary<string, bool> TargetsExecutionData { get; private init; }
}

internal sealed class WorkerNodeTelemetryEventArgs(IWorkerNodeTelemetryData workerNodeTelemetryData) : BuildEventArgs
{
    public WorkerNodeTelemetryEventArgs()
        : this(new WorkerNodeTelemetryData())
    { }

    public IWorkerNodeTelemetryData WorkerNodeTelemetryData { get; private set; } = workerNodeTelemetryData;

    internal override void WriteToStream(BinaryWriter writer)
    {
        writer.Write7BitEncodedInt(WorkerNodeTelemetryData.TasksExecutionData.Count);
        foreach (KeyValuePair<string, TaskExecutionStats> entry in WorkerNodeTelemetryData.TasksExecutionData)
        {
            writer.Write(entry.Key);
            writer.Write(entry.Value.CumulativeExecutionTime.Ticks);
            writer.Write(entry.Value.ExecutionsCount);
        }

        writer.Write7BitEncodedInt(WorkerNodeTelemetryData.TargetsExecutionData.Count);
        foreach (KeyValuePair<string, bool> entry in WorkerNodeTelemetryData.TargetsExecutionData)
        {
            writer.Write(entry.Key);
        }
    }

    internal override void CreateFromStream(BinaryReader reader, int version)
    {
        int count = reader.Read7BitEncodedInt();
        Dictionary<string, TaskExecutionStats> tasksExecutionData = new();
        for (int i = 0; i < count; i++)
        {
            tasksExecutionData.Add(reader.ReadString(),
                new TaskExecutionStats(TimeSpan.FromTicks(reader.ReadInt64()), reader.ReadInt16()));
        }

        count = reader.Read7BitEncodedInt();
        Dictionary<string, bool> targetsExecutionData = new();
        for (int i = 0; i < count; i++)
        {
            targetsExecutionData.Add(reader.ReadString(), true);
        }

        WorkerNodeTelemetryData = new WorkerNodeTelemetryData(tasksExecutionData, targetsExecutionData);
    }
}
