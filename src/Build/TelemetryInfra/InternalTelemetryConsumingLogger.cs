// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Telemetry;

namespace Microsoft.Build.TelemetryInfra;

internal sealed class InternalTelemetryConsumingLogger : ILogger
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

    public IWorkerNodeTelemetryData WorkerNodeTelemetryData => _workerNodeTelemetryData;

    private void EventSource5_WorkerNodeTelemetryLogged(object? sender, WorkerNodeTelemetryEventArgs e)
    {
        _workerNodeTelemetryData.Add(e.WorkerNodeTelemetryData);
    }

    private void EventSourceOnBuildFinished(object sender, BuildFinishedEventArgs e)
    {
        TestOnly_InternalTelemetryAggregted?.Invoke(_workerNodeTelemetryData);
        FlushDataIntoConsoleIfRequested();
    }

    private void FlushDataIntoConsoleIfRequested()
    {
        if (!Traits.Instance.FlushNodesTelemetryIntoConsole)
        {
            return;
        }

        Console.WriteLine("==========================================");
        Console.WriteLine($"Targets ({_workerNodeTelemetryData.TargetsExecutionData.Count}):");
        foreach (var target in _workerNodeTelemetryData.TargetsExecutionData)
        {
            Console.WriteLine($"{target.Key} : {target.Value}");
        }
        Console.WriteLine("==========================================");
        Console.WriteLine($"Tasks: ({_workerNodeTelemetryData.TasksExecutionData.Count})");
        Console.WriteLine("Custom tasks:");
        foreach (var task in _workerNodeTelemetryData.TasksExecutionData.Where(t => t.Key.IsCustom))
        {
            Console.WriteLine($"{task.Key}");
        }
        Console.WriteLine("==========================================");
        Console.WriteLine("Tasks by time:");
        foreach (var task in _workerNodeTelemetryData.TasksExecutionData.OrderByDescending(t => t.Value.CumulativeExecutionTime))
        {
            Console.WriteLine($"{task.Key} - {task.Value.CumulativeExecutionTime}");
        }
        Console.WriteLine("==========================================");
        Console.WriteLine("Tasks by memory consumption:");
        foreach (var task in _workerNodeTelemetryData.TasksExecutionData.OrderByDescending(t => t.Value.TotalMemoryBytes))
        {
            Console.WriteLine($"{task.Key} - {task.Value.TotalMemoryBytes / 1024.0:0.00}kB");
        }

        Console.WriteLine("==========================================");
        Console.WriteLine("Tasks by Executions count:");
        foreach (var task in _workerNodeTelemetryData.TasksExecutionData.OrderByDescending(t => t.Value.ExecutionsCount))
        {
            Console.WriteLine($"{task.Key} - {task.Value.ExecutionsCount}");
        }

        Console.WriteLine("==========================================");
    }

    public void Shutdown()
    { }
}
