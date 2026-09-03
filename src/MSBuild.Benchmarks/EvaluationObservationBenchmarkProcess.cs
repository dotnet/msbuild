// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;

namespace MSBuild.Benchmarks;

internal static class EvaluationObservationBenchmarkProcess
{
    private const int HostTimeoutMilliseconds = 120_000;

    internal static EvaluationObservationBenchmarkResult Run(
        bool observationEnabled,
        EvaluationObservationBenchmarkScenario scenario,
        string projectPath,
        int iterations)
    {
        string assemblyPath = typeof(EvaluationObservationBenchmarkProcess).Assembly.Location;
        string executable;
        string arguments;

#if NETFRAMEWORK
        executable = Path.ChangeExtension(assemblyPath, ".exe");
        arguments = CreateHostArguments(projectPath, iterations, observationEnabled, scenario);
#else
        executable = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
        arguments = string.Concat(
            Quote(assemblyPath),
            " ",
            CreateHostArguments(projectPath, iterations, observationEnabled, scenario));
#endif

        ProcessStartInfo startInfo = new(executable, arguments)
        {
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        using Process process = Process.Start(startInfo) ??
            throw new InvalidOperationException($"Could not start benchmark host '{executable}'.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(HostTimeoutMilliseconds))
        {
            process.Kill();
            throw new TimeoutException($"Benchmark host exceeded {HostTimeoutMilliseconds} ms.");
        }

        Task.WaitAll(standardOutput, standardError);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Benchmark host exited with code {process.ExitCode}.{Environment.NewLine}" +
                $"{standardOutput.Result}{Environment.NewLine}{standardError.Result}");
        }

        return EvaluationObservationBenchmarkResult.Parse(standardOutput.Result);
    }

    private static string CreateHostArguments(
        string projectPath,
        int iterations,
        bool observationEnabled,
        EvaluationObservationBenchmarkScenario scenario)
    {
        return string.Join(
            " ",
            "--evaluation-observation-host",
            "--project",
            Quote(projectPath),
            "--iterations",
            iterations.ToString(CultureInfo.InvariantCulture),
            "--observation-enabled",
            observationEnabled.ToString(CultureInfo.InvariantCulture),
            "--scenario",
            scenario.ToString());
    }

    private static string Quote(string value) =>
        string.Concat("\"", value.Replace("\"", "\\\""), "\"");
}
