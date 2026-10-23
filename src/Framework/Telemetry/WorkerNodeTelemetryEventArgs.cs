// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.Framework.Telemetry;

internal sealed class WorkerNodeTelemetryEventArgs(IWorkerNodeTelemetryData workerNodeTelemetryData) : BuildEventArgs
{
    private const int PayloadMarker = int.MaxValue;
    private const int CurrentPayloadVersion = 2;

    public WorkerNodeTelemetryEventArgs()
        : this(new WorkerNodeTelemetryData())
    { }

    public IWorkerNodeTelemetryData WorkerNodeTelemetryData { get; private set; } = workerNodeTelemetryData;

    internal override void WriteToStream(BinaryWriter writer)
    {
        writer.Write7BitEncodedInt(PayloadMarker);
        writer.Write7BitEncodedInt(CurrentPayloadVersion);
        writer.Write7BitEncodedInt(WorkerNodeTelemetryData.TasksExecutionData.Count);
        foreach (KeyValuePair<TaskOrTargetTelemetryKey, TaskExecutionStats> entry in WorkerNodeTelemetryData.TasksExecutionData)
        {
            WriteToStream(writer, entry.Key);
            writer.Write(entry.Value.CumulativeExecutionTime.Ticks);
            writer.Write(entry.Value.ExecutionsCount);
            writer.Write(entry.Value.TotalMemoryBytes ?? -1);
            writer.Write(entry.Value.TaskFactoryName ?? string.Empty);
            writer.Write(entry.Value.TaskHostRuntime ?? string.Empty);
        }

        writer.Write7BitEncodedInt(WorkerNodeTelemetryData.TargetsExecutionData.Count);
        foreach (KeyValuePair<TaskOrTargetTelemetryKey, TargetExecutionStats> entry in WorkerNodeTelemetryData.TargetsExecutionData)
        {
            WriteToStream(writer, entry.Key);
            writer.Write(entry.Value.WasExecuted);
            writer.Write((int)entry.Value.SkipReason);
        }

        writer.Write(WorkerNodeTelemetryData.UpToDateInputOutputTargetsCount);
        writer.Write(WorkerNodeTelemetryData.ExecutedInputOutputTargetsCount);
        writer.Write(WorkerNodeTelemetryData.ExecutedTargetsCount);
        writer.Write(WorkerNodeTelemetryData.SkippedTargetsCount);
        writer.Write(WorkerNodeTelemetryData.SkippedDueToUpToDateCount);
        writer.Write(WorkerNodeTelemetryData.SkippedDueToConditionCount);
        writer.Write(WorkerNodeTelemetryData.SkippedDueToPreviouslyBuiltCount);
    }

    internal override void CreateFromStream(BinaryReader reader, int version)
    {
        int payloadVersion;
        int count = reader.Read7BitEncodedInt();
        if (count == PayloadMarker)
        {
            payloadVersion = reader.Read7BitEncodedInt();
            if (payloadVersion != CurrentPayloadVersion)
            {
                throw new InvalidDataException($"Unsupported worker telemetry payload version {payloadVersion}.");
            }

            count = reader.Read7BitEncodedInt();
        }
        else
        {
            payloadVersion = 1;
        }

        Dictionary<TaskOrTargetTelemetryKey, TaskExecutionStats> tasksExecutionData = new();
        for (int i = 0; i < count; i++)
        {
            var key = ReadFromStream(reader);
            var cumulativeExecutionTime = TimeSpan.FromTicks(reader.ReadInt64());
            var executionsCount = reader.ReadInt32();
            long serializedMemoryBytes = reader.ReadInt64();
            long? totalMemoryBytes = serializedMemoryBytes >= 0 ? serializedMemoryBytes : null;
            var taskFactoryName = reader.ReadString();
            var taskHostRuntime = reader.ReadString();

            tasksExecutionData.Add(
                key,
                new TaskExecutionStats(
                    cumulativeExecutionTime,
                    executionsCount,
                    totalMemoryBytes,
                    string.IsNullOrEmpty(taskFactoryName) ? null : taskFactoryName,
                    string.IsNullOrEmpty(taskHostRuntime) ? null : taskHostRuntime));
        }

        count = reader.Read7BitEncodedInt();
        Dictionary<TaskOrTargetTelemetryKey, TargetExecutionStats> targetsExecutionData = new();
        for (int i = 0; i < count; i++)
        {
            var key = ReadFromStream(reader);
            var wasExecuted = reader.ReadBoolean();
            var skipReason = (TargetSkipReason)reader.ReadInt32();
            targetsExecutionData.Add(key, new TargetExecutionStats(wasExecuted, skipReason));
        }

        WorkerNodeTelemetryData = payloadVersion >= 2
            ? new WorkerNodeTelemetryData(
                tasksExecutionData,
                targetsExecutionData,
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32())
            : new WorkerNodeTelemetryData(tasksExecutionData, targetsExecutionData);
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
