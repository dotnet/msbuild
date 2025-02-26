// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Telemetry;
using System.IO;

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
        FlushDataIntoJsonFileIfRequested();
    }

    private void FlushDataIntoConsoleIfRequested()
    {
        if (!Traits.IsEnvVarOneOrTrue("MSBUILDOUTPUTNODESTELEMETRY"))
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
        foreach (var task in _workerNodeTelemetryData.TasksExecutionData.OrderByDescending(t => t.Value.CumulativeExecutionTime).Take(20))
        {
            Console.WriteLine($"{task.Key} - {task.Value.CumulativeExecutionTime}");
        }
        Console.WriteLine("==========================================");
        Console.WriteLine("Tasks by memory consumption:");
        foreach (var task in _workerNodeTelemetryData.TasksExecutionData.OrderByDescending(t => t.Value.TotalMemoryConsumption).Take(20))
        {
            Console.WriteLine($"{task.Key} - {task.Value.TotalMemoryConsumption / 1024.0:0.00}kB");
        }
        Console.WriteLine("==========================================");
        Console.WriteLine("Tasks by Executions count:");
        foreach (var task in _workerNodeTelemetryData.TasksExecutionData.OrderByDescending(t => t.Value.ExecutionsCount))
        {
            Console.WriteLine($"{task.Key} - {task.Value.ExecutionsCount}");
        }
        Console.WriteLine("==========================================");
    }

    private void FlushDataIntoJsonFileIfRequested()
    {
        const string jsonFileNameVariable = "MSBUILDNODETELEMETRYFILENAME";
        var jsonFilePath = Environment.GetEnvironmentVariable(jsonFileNameVariable);
        if (string.IsNullOrEmpty(jsonFilePath))
        {
            return;
        }

        var telemetryTags = _workerNodeTelemetryData.AsActivityDataHolder(true, true)?.GetActivityProperties();

        using var stream = File.OpenWrite(jsonFilePath);
        stream.SetLength(0);
        JsonSerializer.Serialize(stream, telemetryTags, new JsonSerializerOptions() { WriteIndented = true });
    }

    public void Shutdown()
    { }
}
