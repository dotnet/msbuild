// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnostics.Windows;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using static MSBuild.Benchmarks.Extensions;

var argList = new List<string>(args);

ParseAndRemoveBooleanParameter(argList, "--collect-etw", out bool collectEtw);
ParseAndRemoveBooleanParameter(argList, "--disable-ngen", out bool disableNGen);
ParseAndRemoveBooleanParameter(argList, "--disable-inlining", out bool disableJitInlining);
ParseAndRemoveBooleanParameter(argList, "--enforce-power-plan", out bool enforcePowerPlan);

return BenchmarkSwitcher
    .FromAssembly(typeof(Program).Assembly)
    .Run([.. argList], GetConfig(collectEtw, disableNGen, disableJitInlining, enforcePowerPlan))
    .ToExitCode();

static IConfig GetConfig(
    bool collectEtw,
    bool disableNGen,
    bool disableJitInlining,
    bool enforcePowerPlan)
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
    Job overrides = new();

    // Dedicated benchmark machines should use a stable power plan. Leave the host unchanged by
    // default, but allow BenchmarkDotNet to temporarily select High Performance when requested.
    if (!enforcePowerPlan)
    {
        overrides = overrides.DontEnforcePowerPlan();
    }

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
