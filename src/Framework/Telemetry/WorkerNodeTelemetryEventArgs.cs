// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Framework;

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
            writer.Write(entry.Value.TotalMemoryConsumption);
        }

        writer.Write7BitEncodedInt(WorkerNodeTelemetryData.TargetsExecutionData.Count);
        foreach (KeyValuePair<string, bool> entry in WorkerNodeTelemetryData.TargetsExecutionData)
        {
            writer.Write(entry.Key);
            writer.Write(entry.Value);
        }
    }

    internal override void CreateFromStream(BinaryReader reader, int version)
    {
        int count = reader.Read7BitEncodedInt();
        Dictionary<string, TaskExecutionStats> tasksExecutionData = new();
        for (int i = 0; i < count; i++)
        {
            tasksExecutionData.Add(reader.ReadString(),
                new TaskExecutionStats(TimeSpan.FromTicks(reader.ReadInt64()), reader.ReadInt16(), reader.ReadInt64()));
        }

        count = reader.Read7BitEncodedInt();
        Dictionary<string, bool> targetsExecutionData = new();
        for (int i = 0; i < count; i++)
        {
            targetsExecutionData.Add(reader.ReadString(), reader.ReadBoolean());
        }

        WorkerNodeTelemetryData = new WorkerNodeTelemetryData(tasksExecutionData, targetsExecutionData);
    }
}
