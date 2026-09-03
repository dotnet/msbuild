// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;

namespace MSBuild.Benchmarks;

public enum EvaluationObservationBenchmarkScenario
{
    Typical,
    GlobHeavy,
    AmbientAndSdk,
}

internal sealed class EvaluationObservationBenchmarkResult
{
    private const string Prefix = "EVALUATION_OBSERVATION_BENCHMARK";

    internal long EvaluationTicks { get; init; }
    internal long AllocatedManagedBytes { get; init; }
    internal int NativeReports { get; init; }
    internal int NativeObservations { get; init; }

    internal string Serialize()
    {
        return string.Join(
            "|",
            Prefix,
            Pair(nameof(EvaluationTicks), EvaluationTicks),
            Pair(nameof(AllocatedManagedBytes), AllocatedManagedBytes),
            Pair(nameof(NativeReports), NativeReports),
            Pair(nameof(NativeObservations), NativeObservations));
    }

    internal static EvaluationObservationBenchmarkResult Parse(string output)
    {
        string? line = null;
        using (StringReader reader = new(output))
        {
            while (reader.ReadLine() is { } candidate)
            {
                if (candidate.StartsWith(Prefix, StringComparison.Ordinal))
                {
                    line = candidate;
                }
            }
        }

        if (line is null)
        {
            throw new InvalidOperationException(
                $"Benchmark host did not return a {Prefix} result.{Environment.NewLine}{output}");
        }

        Dictionary<string, long> values = new(StringComparer.Ordinal);
        string[] fields = line.Split('|');
        for (int i = 1; i < fields.Length; i++)
        {
            int separator = fields[i].IndexOf('=');
            if (separator <= 0 ||
                !long.TryParse(
                    fields[i].Substring(separator + 1),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out long value))
            {
                throw new InvalidOperationException($"Invalid benchmark result field '{fields[i]}'.");
            }

            values.Add(fields[i].Substring(0, separator), value);
        }

        return new EvaluationObservationBenchmarkResult
        {
            EvaluationTicks = Get(nameof(EvaluationTicks)),
            AllocatedManagedBytes = Get(nameof(AllocatedManagedBytes)),
            NativeReports = checked((int)Get(nameof(NativeReports))),
            NativeObservations = checked((int)Get(nameof(NativeObservations))),
        };

        long Get(string name) =>
            values.TryGetValue(name, out long value)
                ? value
                : throw new InvalidOperationException($"Benchmark result did not contain '{name}'.");
    }

    private static string Pair(string name, long value) =>
        string.Concat(name, "=", value.ToString(CultureInfo.InvariantCulture));
}

internal sealed class EvaluationObservationNativeMetrics
{
    internal int Reports;
    internal int Observations;
}
