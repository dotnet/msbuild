// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft;
using Microsoft.Build;
using Microsoft.Build.Coordinator;
using Microsoft.Build.Framework.Coordinator;

// Ensure single instance using a named mutex.
string mutexName = $"Global\\{Protocol.GetPipeName()}";

using Mutex mutex = new(initiallyOwned: false, mutexName, out bool createdNew);

if (!createdNew)
{
    // Another coordinator is already running.
    Console.Error.WriteLine("Another MSBuild coordinator is already running.");
    return 1;
}

int totalBudget = GetEnvironmentInt(
    Protocol.NodeBudgetEnvironmentVariable,
    Environment.ProcessorCount);

int heartbeatIntervalMs = GetEnvironmentInt(
    Protocol.HeartbeatIntervalEnvironmentVariable,
    Protocol.DefaultHeartbeatIntervalMs);

int shutdownTimeoutMs = GetEnvironmentInt(
    Protocol.ShutdownTimeoutEnvironmentVariable,
    60_000);

string pipeName = Protocol.GetPipeName();

Console.WriteLine($"MSBuild Coordinator starting.");
Console.WriteLine($"  Pipe: {pipeName}");
Console.WriteLine($"  Node budget: {totalBudget}");
Console.WriteLine($"  Heartbeat interval: {heartbeatIntervalMs}ms");
Console.WriteLine($"  Shutdown timeout: {shutdownTimeoutMs}ms");

using CoordinatorServer server = new(
    totalBudget,
    pipeName,
    heartbeatIntervalMs,
    shutdownTimeoutMs: shutdownTimeoutMs);

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

Console.WriteLine("MSBuild Coordinator shut down.");
return 0;

int GetEnvironmentInt(string variable, int defaultValue)
{
    string? value = Environment.GetEnvironmentVariable(variable);

    return int.TryParse(value, out int result) ? result : defaultValue;
}
