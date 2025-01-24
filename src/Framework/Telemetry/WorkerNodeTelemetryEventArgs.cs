// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
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

    public void Add(IWorkerNodeTelemetryData other)
    {
        foreach (var task in other.TasksExecutionData)
        {
            AddTask(task.Key, task.Value.CumulativeExecutionTime, task.Value.ExecutionsCount);
        }

        foreach (var target in other.TargetsExecutionData)
        {
            AddTarget(target.Key, target.Value);
        }
    }

    public void AddTask(string name, TimeSpan cumulativeExectionTime, short executionsCount)
    {
        TaskExecutionStats taskExecutionStats;
        if (!TasksExecutionData.TryGetValue(name, out taskExecutionStats))
        {
            taskExecutionStats = new(cumulativeExectionTime, executionsCount);
            TasksExecutionData[name] = taskExecutionStats;
        }
        else
        {
            taskExecutionStats.CumulativeExecutionTime += cumulativeExectionTime;
            taskExecutionStats.ExecutionsCount += executionsCount;
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
                new TaskExecutionStats(TimeSpan.FromTicks(reader.ReadInt64()), reader.ReadInt16()));
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

internal sealed class InternalTelemeteryConsumingLogger : ILogger
{
    public LoggerVerbosity Verbosity { get; set; }
    public string? Parameters { get; set; }
    internal static event Action<WorkerNodeTelemetryData>? TestOnly_InternalTelemetryAggregted; 

    public void Initialize(IEventSource eventSource)
    {
        if (eventSource is IEventSource5 eventSource5)
        {
            eventSource5.WorkerNodeTelemetryLogged += EventSource5_WorkerNodeTelemetryLogged;
            eventSource.BuildFinished += EventSourceOnBuildFinished;
        }
    }

    private readonly WorkerNodeTelemetryData _workerNodeTelemetryData = new();

    private void EventSource5_WorkerNodeTelemetryLogged(object? sender, WorkerNodeTelemetryEventArgs e)
    {
        _workerNodeTelemetryData.Add(e.WorkerNodeTelemetryData);
    }

    private void EventSourceOnBuildFinished(object sender, BuildFinishedEventArgs e)
    {
        TestOnly_InternalTelemetryAggregted?.Invoke(_workerNodeTelemetryData);
    }

    public void Shutdown()
    { }
}

/// <remarks>
/// Ensure that events filtering is in sync with <see cref="InternalTelemeteryConsumingLogger"/>.
/// </remarks>
internal class InternalTelemeteryForwardingLogger : IForwardingLogger
{
    public IEventRedirector? BuildEventRedirector { get; set; }

    public int NodeId { get; set; }

    public LoggerVerbosity Verbosity { get => LoggerVerbosity.Quiet; set { return; } }

    public string? Parameters { get; set; }

    public void Initialize(IEventSource eventSource, int nodeCount) => Initialize(eventSource);

    public void Initialize(IEventSource eventSource)
    {
        if (BuildEventRedirector != null && eventSource is IEventSource5 eventSource5)
        {
            eventSource5.WorkerNodeTelemetryLogged += (o,e) => BuildEventRedirector.ForwardEvent(e);
        }
    }

    public void Shutdown()
    {
    }
}
