// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET
using System.Diagnostics;
using System.Runtime.Versioning;
using BenchmarkDotNet.Attributes;
using Microsoft.Build.Shared;

namespace MSBuild.Benchmarks;

/// <summary>
/// Measures the per-call cost of <see cref="ProcessExtensions.TryGetCommandLine"/> against a stable target
/// process. <see cref="GlobalSetup"/> starts a long-running child process (<c>ping -n 31 127.0.0.1</c> on
/// Windows, <c>sleep 30</c> elsewhere) and reuses its PID as the target for every iteration. The underlying
/// platform implementation exercised by the benchmark is <c>dbgeng!IDebugClient::GetRunningProcessDescription</c>
/// on Windows, <c>/proc/{pid}/cmdline</c> on Linux, and <c>sysctl KERN_PROCARGS2</c> on macOS/BSD.
/// </summary>
public class TryGetCommandLineBenchmark
{
    private Process _target = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Long-running process so the PID stays valid for the entire benchmark run. ping -t is roughly equivalent
        // to the one used in ProcessExtensions_Tests.
        ProcessStartInfo psi = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("ping", "-n 31 127.0.0.1")
            : new ProcessStartInfo("sleep", "30")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        };
        _target = Process.Start(psi)!;

        // Let the kernel publish the process fully before the first query.
        Thread.Sleep(200);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        try
        {
            if (_target is { HasExited: false })
            {
                _target.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best effort cleanup.
        }
        finally
        {
            _target?.Dispose();
        }
    }

    [Benchmark(Description = "Process - TryGetCommandLine")]
    public string? TryGetCommandLine_Windows() => _target.TryGetCommandLine(out var commandLine) ? commandLine : null;
}
#endif
