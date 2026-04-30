// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnostics.Windows;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using static MSBuild.Benchmarks.Extensions;

var argList = new List<string>(args);

ParseAndRemoveBooleanParameter(argList, "--collect-etw", out bool collectEtw);
ParseAndRemoveBooleanParameter(argList, "--disable-ngen", out bool disableNGen);
ParseAndRemoveBooleanParameter(argList, "--disable-inlining", out bool disableJitInlining);
ParseAndRemoveBooleanParameter(argList, "--ci", out bool ci);

// In CI mode, run all benchmarks unless the caller already specified a filter.
if (ci && !argList.Any(a => a == "--filter" || a.StartsWith("--filter=")))
{
    argList.Add("--filter");
    argList.Add("*");
}

return BenchmarkSwitcher
    .FromAssembly(typeof(Program).Assembly)
    .Run([.. argList], GetConfig(collectEtw, disableNGen, disableJitInlining, ci))
    .ToExitCode();

static IConfig GetConfig(bool collectEtw, bool disableNGen, bool disableJitInlining, bool ci)
{
    if (Debugger.IsAttached)
    {
        return new DebugInProcessConfig();
    }

    // CI dry-run: execute every benchmark exactly once in-process to verify they don't crash.
    if (ci)
    {
        return ManualConfig.Create(DefaultConfig.Instance)
            .AddJob(Job.Dry.WithToolchain(InProcessEmitToolchain.Instance))
            .WithOption(ConfigOptions.DisableOptimizationsValidator, true);
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
