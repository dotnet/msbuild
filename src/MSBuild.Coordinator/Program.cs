// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using System.Text;
using Microsoft.Build;
using Microsoft.Build.Coordinator;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Coordinator;

CoordinatorSettings settings = CoordinatorSettings.FromEnvironment();

// Ensure single instance using a named mutex.
string mutexName = GetCoordinatorMutexName(settings.PipeName);

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

static string GetCoordinatorMutexName(string pipeName)
{
    if (NativeMethods.IsWindows)
    {
        return $"Global\\{pipeName}";
    }

    // Named mutexes on Unix do not accept path-like names (for example '/tmp/...').
    // Hash the pipe name into a stable, compact identifier safe for the runtime.
    const string MutexPrefix = "msbuild-coordinator-";

    using SHA256 sha256 = SHA256.Create();
    byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(pipeName));

    int mutexNameLength = MutexPrefix.Length + (hash.Length * 2);

    return string.Create(mutexNameLength, hash, (span, bytes) =>
    {
        MutexPrefix.AsSpan().CopyTo(span);
        span = span[MutexPrefix.Length..];

        for (int i = 0; i < bytes.Length; i++)
        {
            byte b = bytes[i];
            span[0] = HexDigitChar(b / 16);
            span[1] = HexDigitChar(b % 16);

            span = span[2..];
        }
    });

    static char HexDigitChar(int value)
        => (char)(value + (value < 10 ? '0' : 'a' - 10));
}
