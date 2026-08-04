// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnostics.Windows;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using static MSBuild.Benchmarks.Extensions;

var argList = new List<string>(args);
const string orchardCoreProjectEnvironmentVariable = "MSBUILD_BENCHMARK_ORCHARDCORE_PROJECT";
const string orchardCoreBenchmarkType = "MSBuild.Benchmarks.OrchardCoreEvaluationBenchmark";

ParseAndRemoveBooleanParameter(argList, "--collect-etw", out bool collectEtw);
ParseAndRemoveBooleanParameter(argList, "--disable-ngen", out bool disableNGen);
ParseAndRemoveBooleanParameter(argList, "--disable-inlining", out bool disableJitInlining);
if (!TryParseAndRemoveStringParameter(
        argList,
        "--orchard-core-project",
        out string? orchardCoreProject,
        out string? parseError))
{
    Console.Error.WriteLine(parseError);
    return 1;
}

if (orchardCoreProject is not null)
{
    Environment.SetEnvironmentVariable(orchardCoreProjectEnvironmentVariable, orchardCoreProject);
}

bool includeOrchardCoreBenchmark =
    !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(orchardCoreProjectEnvironmentVariable));
Type[] benchmarkTypes = Assembly.GetExecutingAssembly()
    .GetTypes()
    .Where(type =>
        type.GetMethods().Any(method => method.GetCustomAttribute<BenchmarkAttribute>() is not null) &&
        (includeOrchardCoreBenchmark || type.FullName != orchardCoreBenchmarkType))
    .ToArray();

return BenchmarkSwitcher
    .FromTypes(benchmarkTypes)
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

static bool TryParseAndRemoveStringParameter(
    List<string> argsList,
    string parameter,
    out string? parameterValue,
    out string? error)
{
    int parameterIndex = argsList.IndexOf(parameter);

    if (parameterIndex == -1)
    {
        parameterValue = null;
        error = null;
        return true;
    }

    if (parameterIndex == argsList.Count - 1 ||
        string.IsNullOrWhiteSpace(argsList[parameterIndex + 1]) ||
        argsList[parameterIndex + 1].StartsWith("--", StringComparison.Ordinal))
    {
        parameterValue = null;
        error = $"Missing value for {parameter}.";
        return false;
    }

    parameterValue = argsList[parameterIndex + 1];
    argsList.RemoveRange(parameterIndex, 2);
    error = null;
    return true;
}
