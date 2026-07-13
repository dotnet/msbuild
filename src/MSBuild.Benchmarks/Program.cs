// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnostics.Windows;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using static MSBuild.Benchmarks.Extensions;

var argList = new List<string>(args);

// Deterministic allocation A/B harness for the BuildEventArgsWriter NameValueList change.
// Runs a fixed matrix of workloads and prints machine-readable results plus assembly provenance.
// This path is intentionally independent of BenchmarkDotNet so the exact-parent vs fixed
// allocation delta can be measured deterministically and reproducibly. Scoped to .NET (Core);
// the harness uses .NET 5+ APIs and is excluded on net472 (see BinlogAllocationHarness.cs).
#if NET
if (argList.Remove("--alloc-harness"))
{
    return MSBuild.Benchmarks.AllocHarnessRunner.Run(argList);
}
#endif

ParseAndRemoveBooleanParameter(argList, "--collect-etw", out bool collectEtw);
ParseAndRemoveBooleanParameter(argList, "--disable-ngen", out bool disableNGen);
ParseAndRemoveBooleanParameter(argList, "--disable-inlining", out bool disableJitInlining);

return BenchmarkSwitcher
    .FromAssembly(typeof(Program).Assembly)
    .Run([.. argList], GetConfig(collectEtw, disableNGen, disableJitInlining))
    .ToExitCode();

static IConfig GetConfig(bool collectEtw, bool disableNGen, bool disableJitInlining)
{
    if (Debugger.IsAttached)
    {
        return new DebugInProcessConfig();
    }

    IConfig config = DefaultConfig.Instance;

    if (collectEtw)
    {
        config = config.AddDiagnoser(new EtwProfiler());
    }

    // Use a mutator for settings that should apply to all jobs
    // (default or CLI-specified like --job short).
    Job overrides = new Job()
        .DontEnforcePowerPlan();

    if (disableNGen)
    {
        overrides = overrides
            .WithEnvironmentVariable("COMPlus_ZapDisable", "1")
            .WithEnvironmentVariable("COMPlus_ReadyToRun", "0")
            .WithEnvironmentVariable("DOTNET_ReadyToRun", "0");
    }

    if (disableJitInlining)
    {
        overrides = overrides
            .WithEnvironmentVariable("COMPlus_JitNoInline", "1")
            .WithEnvironmentVariable("DOTNET_JitNoInline", "1");
    }

    config = config.AddJob(overrides.AsMutator());

    return config;
}

static void ParseAndRemoveBooleanParameter(List<string> argsList, string parameter, out bool parameterValue)
{
    int parameterIndex = argsList.IndexOf(parameter);

    if (parameterIndex != -1)
    {
        argsList.RemoveAt(parameterIndex);

        parameterValue = true;
    }
    else
    {
        parameterValue = false;
    }
}
