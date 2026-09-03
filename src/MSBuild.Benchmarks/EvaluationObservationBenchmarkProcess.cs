// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace MSBuild.Benchmarks;

internal static class EvaluationObservationBenchmarkProcess
{
    private const int HostTimeoutMilliseconds = 120_000;

    internal static EvaluationObservationBenchmarkResult Run(
        EvaluationObservationBenchmarkMode mode,
        EvaluationObservationBenchmarkScenario scenario,
        string projectPath,
        IReadOnlyList<string> comparisonRoots,
        int iterations,
        IReadOnlyDictionary<string, string>? globalProperties = null,
        string? measurementRoot = null,
        bool includeNativeOnlyPaths = false)
    {
        string assemblyPath = typeof(EvaluationObservationBenchmarkProcess).Assembly.Location;
        measurementRoot ??= Path.GetDirectoryName(projectPath)!;
        string executable;
        string arguments;

#if NETFRAMEWORK
        executable = Path.ChangeExtension(assemblyPath, ".exe");
        arguments = CreateHostArguments(
            projectPath,
            iterations,
            mode,
            scenario,
            measurementRoot,
            globalProperties);
#else
        executable = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
        arguments = string.Concat(
            Quote(assemblyPath),
            " ",
            CreateHostArguments(
                projectPath,
                iterations,
                mode,
                scenario,
                measurementRoot,
                globalProperties));
#endif

        if ((mode & EvaluationObservationBenchmarkMode.Detours) != 0)
        {
            return EvaluationObservationDetoursRunner.Run(
                executable,
                arguments,
                comparisonRoots,
                measurementRoot,
                includeNativeOnlyPaths);
        }

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
        EvaluationObservationBenchmarkMode mode,
        EvaluationObservationBenchmarkScenario scenario,
        string measurementRoot,
        IReadOnlyDictionary<string, string>? globalProperties)
    {
        StringBuilder arguments = new();
        arguments.Append("--evaluation-observation-host --project ");
        arguments.Append(Quote(projectPath));
        arguments.Append(" --iterations ");
        arguments.Append(iterations.ToString(CultureInfo.InvariantCulture));
        arguments.Append(" --mode ");
        arguments.Append(mode);
        arguments.Append(" --scenario ");
        arguments.Append(scenario);
        arguments.Append(" --measurement-root ");
        arguments.Append(Quote(measurementRoot));

        if (globalProperties is not null)
        {
            foreach (KeyValuePair<string, string> property in globalProperties)
            {
                arguments.Append(" --global-property ");
                arguments.Append(Quote(string.Concat(property.Key, "=", property.Value)));
            }
        }

        return arguments.ToString();
    }

    internal static string Quote(string value) => string.Concat("\"", value.Replace("\"", "\\\""), "\"");
}
