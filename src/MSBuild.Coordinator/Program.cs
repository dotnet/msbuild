// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Coordinator;
using Microsoft.Build.Framework.Coordinator;
using Microsoft.Build.Framework.Telemetry;

static string FormatHighPriorityReservedNodes(int value)
    => value == 0 ? "0 (disabled)" : value.ToString();

static string FormatMaxNodesPerBuild(int value)
    => value == 0 ? "0 (uncapped)" : value.ToString();

TelemetryManager.Instance.Initialize(isStandalone: true);

CoordinatorSettings settings = CoordinatorSettings.FromEnvironment();

// Ensure single instance using a named mutex.
using Mutex mutex = new(initiallyOwned: false, settings.ServerMutexName, out bool createdNew);

if (!createdNew)
{
    // Another coordinator is already running.
    Console.Error.WriteLine("Another MSBuild coordinator is already running.");
    return 1;
}

Console.WriteLine($"MSBuild Coordinator starting.");
Console.WriteLine($"  Pipe: {settings.PipeName}");
Console.WriteLine($"  Node budget: {settings.TotalNodeBudget}");
Console.WriteLine($"  High-priority reserved nodes: {FormatHighPriorityReservedNodes(settings.HighPriorityReservedNodes)}");
Console.WriteLine($"  Max nodes per build: {FormatMaxNodesPerBuild(settings.MaxNodesPerBuild)}");
if (settings.AutoStrictPolicyOptOutMessage is { } autoStrictPolicyOptOutMessage)
{
    Console.WriteLine($"  Auto strict policy active. {autoStrictPolicyOptOutMessage}");
}

Console.WriteLine($"  Heartbeat interval: {settings.HeartbeatIntervalMs}ms");
Console.WriteLine($"  Shutdown timeout: {settings.ShutdownTimeoutMs}ms");

using CoordinatorServer server = new(settings);

using CancellationTokenSource cts = new();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    await server.RunAsync(cts.Token);
}
catch (OperationCanceledException)
{
    // Normal shutdown.
}

TelemetryManager.Instance.Dispose();

Console.WriteLine("MSBuild Coordinator shut down.");
return 0;
