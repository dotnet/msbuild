// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft;
using Microsoft.Build;
using Microsoft.Build.Coordinator;
using Microsoft.Build.Framework.Coordinator;

CoordinatorSettings settings = CoordinatorSettings.FromEnvironment();

// Ensure single instance using a named mutex.
string mutexName = $"Global\\{settings.PipeName}";

using Mutex mutex = new(initiallyOwned: false, mutexName, out bool createdNew);

if (!createdNew)
{
    // Another coordinator is already running.
    Console.Error.WriteLine("Another MSBuild coordinator is already running.");
    return 1;
}

Console.WriteLine($"MSBuild Coordinator starting.");
Console.WriteLine($"  Pipe: {settings.PipeName}");
Console.WriteLine($"  Node budget: {settings.TotalNodeBudget}");
Console.WriteLine($"  Heartbeat interval: {settings.HeartbeatIntervalMs}ms");
Console.WriteLine($"  Shutdown timeout: {settings.ShutdownTimeoutMs}ms");

using CoordinatorServer server = new(
    settings);

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
