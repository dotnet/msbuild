// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Framework.Telemetry;

internal sealed class WorkerNodeTelemetryEventArgs(IWorkerNodeTelemetryData workerNodeTelemetryData) : BuildEventArgs
{
    public WorkerNodeTelemetryEventArgs()
        : this(new WorkerNodeTelemetryData())
    { }

    public IWorkerNodeTelemetryData WorkerNodeTelemetryData { get; private set; } = workerNodeTelemetryData;

    internal override void WriteToStream(BinaryWriter writer)
    {
        writer.Write7BitEncodedInt(WorkerNodeTelemetryData.TasksExecutionData.Count);
        foreach (KeyValuePair<TaskOrTargetTelemetryKey, TaskExecutionStats> entry in WorkerNodeTelemetryData.TasksExecutionData)
        {
            WriteToStream(writer, entry.Key);
            writer.Write(entry.Value.CumulativeExecutionTime.Ticks);
            writer.Write(entry.Value.ExecutionsCount);
            writer.Write(entry.Value.TotalMemoryBytes);
        }

        writer.Write7BitEncodedInt(WorkerNodeTelemetryData.TargetsExecutionData.Count);
        foreach (KeyValuePair<TaskOrTargetTelemetryKey, bool> entry in WorkerNodeTelemetryData.TargetsExecutionData)
        {
            WriteToStream(writer, entry.Key);
            writer.Write(entry.Value);
        }
    }

    internal override void CreateFromStream(BinaryReader reader, int version)
    {
        int count = reader.Read7BitEncodedInt();
        Dictionary<TaskOrTargetTelemetryKey, TaskExecutionStats> tasksExecutionData = new();
        for (int i = 0; i < count; i++)
        {
            tasksExecutionData.Add(ReadFromStream(reader),
                new TaskExecutionStats(
                    TimeSpan.FromTicks(reader.ReadInt64()),
                    reader.ReadInt32(),
                    reader.ReadInt64()));
        }

        count = reader.Read7BitEncodedInt();
        Dictionary<TaskOrTargetTelemetryKey, bool> targetsExecutionData = new();
        for (int i = 0; i < count; i++)
        {
            targetsExecutionData.Add(ReadFromStream(reader), reader.ReadBoolean());
        }

        WorkerNodeTelemetryData = new WorkerNodeTelemetryData(tasksExecutionData, targetsExecutionData);
    }

    private static void WriteToStream(BinaryWriter writer, TaskOrTargetTelemetryKey key)
    {
        writer.Write(key.Name);
        writer.Write(key.IsCustom);
        writer.Write(key.IsNuget);
        writer.Write(key.IsMetaProj);
    }

    private static TaskOrTargetTelemetryKey ReadFromStream(BinaryReader reader)
    {
        return new TaskOrTargetTelemetryKey(
            reader.ReadString(),
            reader.ReadBoolean(),
            reader.ReadBoolean(),
            reader.ReadBoolean());
    }
}
