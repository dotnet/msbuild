// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Text;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Shared;

namespace MSBuild.Benchmarks;

internal static class EvaluationObservationBenchmarkProtocol
{
    internal const string MeasurementStartMarker = ".evaluation-observer-measure-start";
    internal const string MeasurementStopMarker = ".evaluation-observer-measure-stop";
    internal const string NativePathPrefix = "EVALUATION_OBSERVATION_NATIVE_PATH|";
    internal const string NativeEnumerationPrefix = "EVALUATION_OBSERVATION_NATIVE_ENUMERATION|";
    internal const string NativeGlobPrefix = "EVALUATION_OBSERVATION_NATIVE_GLOB|";
}

[Flags]
internal enum EvaluationObservationBenchmarkMode
{
    Baseline = 0,
    Native = 1,
    Detours = 1 << 1,
    NativeAndDetours = Native | Detours,
}

public enum EvaluationObservationBenchmarkScenario
{
    Typical,
    GlobHeavy,
    AmbientAndSdk,
    ExternalProject,
}

internal sealed class EvaluationObservationBenchmarkResult
{
    private const string Prefix = "EVALUATION_OBSERVATION_BENCHMARK";

    internal long EvaluationTicks { get; init; }
    internal long AllocatedManagedBytes { get; init; }
    internal long RetainedManagedBytes { get; init; }
    internal long PrivateBytes { get; init; }
    internal long PeakWorkingSetBytes { get; init; }
    internal int Gen0Collections { get; init; }
    internal int Gen1Collections { get; init; }
    internal int Gen2Collections { get; init; }
    internal int NativeReports { get; init; }
    internal int NativePathProbes { get; init; }
    internal int NativeEnumerations { get; init; }
    internal int NativeMetadataReads { get; init; }
    internal int NativeFileReads { get; init; }
    internal int NativeSemanticObservations { get; init; }
    internal int NativeUniquePaths { get; init; }
    internal int SemanticComparisons { get; init; }
    internal int SemanticImports { get; init; }
    internal int SemanticProperties { get; init; }
    internal int SemanticItems { get; init; }
    internal int SemanticMetadata { get; init; }
    internal int DetoursAccesses { get; init; }
    internal int DetoursUniquePaths { get; init; }
    internal int NativeDetoursOverlap { get; init; }
    internal int NativeOnlyPaths { get; init; }
    internal int DetoursOnlyPaths { get; init; }

    internal string Serialize()
    {
        return string.Join(
            "|",
            Prefix,
            Pair(nameof(EvaluationTicks), EvaluationTicks),
            Pair(nameof(AllocatedManagedBytes), AllocatedManagedBytes),
            Pair(nameof(RetainedManagedBytes), RetainedManagedBytes),
            Pair(nameof(PrivateBytes), PrivateBytes),
            Pair(nameof(PeakWorkingSetBytes), PeakWorkingSetBytes),
            Pair(nameof(Gen0Collections), Gen0Collections),
            Pair(nameof(Gen1Collections), Gen1Collections),
            Pair(nameof(Gen2Collections), Gen2Collections),
            Pair(nameof(NativeReports), NativeReports),
            Pair(nameof(NativePathProbes), NativePathProbes),
            Pair(nameof(NativeEnumerations), NativeEnumerations),
            Pair(nameof(NativeMetadataReads), NativeMetadataReads),
            Pair(nameof(NativeFileReads), NativeFileReads),
            Pair(nameof(NativeSemanticObservations), NativeSemanticObservations),
            Pair(nameof(NativeUniquePaths), NativeUniquePaths),
            Pair(nameof(SemanticComparisons), SemanticComparisons),
            Pair(nameof(SemanticImports), SemanticImports),
            Pair(nameof(SemanticProperties), SemanticProperties),
            Pair(nameof(SemanticItems), SemanticItems),
            Pair(nameof(SemanticMetadata), SemanticMetadata),
            Pair(nameof(DetoursAccesses), DetoursAccesses),
            Pair(nameof(DetoursUniquePaths), DetoursUniquePaths),
            Pair(nameof(NativeDetoursOverlap), NativeDetoursOverlap),
            Pair(nameof(NativeOnlyPaths), NativeOnlyPaths),
            Pair(nameof(DetoursOnlyPaths), DetoursOnlyPaths));
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
            throw new InvalidOperationException($"Benchmark host did not return a {Prefix} result.{Environment.NewLine}{output}");
        }

        Dictionary<string, long> values = new(StringComparer.Ordinal);
        string[] fields = line.Split('|');
        for (int i = 1; i < fields.Length; i++)
        {
            int separator = fields[i].IndexOf('=');
            if (separator <= 0 ||
                !long.TryParse(fields[i].Substring(separator + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out long value))
            {
                throw new InvalidOperationException($"Invalid benchmark result field '{fields[i]}'.");
            }

            values.Add(fields[i].Substring(0, separator), value);
        }

        return new EvaluationObservationBenchmarkResult
        {
            EvaluationTicks = Get(nameof(EvaluationTicks)),
            AllocatedManagedBytes = Get(nameof(AllocatedManagedBytes)),
            RetainedManagedBytes = Get(nameof(RetainedManagedBytes)),
            PrivateBytes = Get(nameof(PrivateBytes)),
            PeakWorkingSetBytes = Get(nameof(PeakWorkingSetBytes)),
            Gen0Collections = checked((int)Get(nameof(Gen0Collections))),
            Gen1Collections = checked((int)Get(nameof(Gen1Collections))),
            Gen2Collections = checked((int)Get(nameof(Gen2Collections))),
            NativeReports = checked((int)Get(nameof(NativeReports))),
            NativePathProbes = checked((int)Get(nameof(NativePathProbes))),
            NativeEnumerations = checked((int)Get(nameof(NativeEnumerations))),
            NativeMetadataReads = checked((int)Get(nameof(NativeMetadataReads))),
            NativeFileReads = checked((int)Get(nameof(NativeFileReads))),
            NativeSemanticObservations = checked((int)Get(nameof(NativeSemanticObservations))),
            NativeUniquePaths = checked((int)Get(nameof(NativeUniquePaths))),
            SemanticComparisons = checked((int)Get(nameof(SemanticComparisons))),
            SemanticImports = checked((int)Get(nameof(SemanticImports))),
            SemanticProperties = checked((int)Get(nameof(SemanticProperties))),
            SemanticItems = checked((int)Get(nameof(SemanticItems))),
            SemanticMetadata = checked((int)Get(nameof(SemanticMetadata))),
            DetoursAccesses = checked((int)Get(nameof(DetoursAccesses))),
            DetoursUniquePaths = checked((int)Get(nameof(DetoursUniquePaths))),
            NativeDetoursOverlap = checked((int)Get(nameof(NativeDetoursOverlap))),
            NativeOnlyPaths = checked((int)Get(nameof(NativeOnlyPaths))),
            DetoursOnlyPaths = checked((int)Get(nameof(DetoursOnlyPaths))),
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
    internal int Reports = 0;
    internal int PathProbes = 0;
    internal int Enumerations = 0;
    internal int MetadataReads = 0;
    internal int FileReads = 0;
    internal int SemanticObservations = 0;
    private readonly HashSet<string> _uniquePaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _enumerations = new(StringComparer.Ordinal);
    private readonly HashSet<string> _globs = new(StringComparer.Ordinal);
    private bool _pathsSampled;

    internal int UniquePathCount => _uniquePaths.Count;

    internal bool TryBeginPathSample()
    {
        if (_pathsSampled)
        {
            return false;
        }

        _pathsSampled = true;
        return true;
    }

    internal void AddPath(string? path)
    {
        AddPath(path, baseDirectory: null);
    }

    internal void AddPath(string? path, string? baseDirectory)
    {
        if (path is null)
        {
            return;
        }

        string concretePath = path;
        if (concretePath.Length == 0 ||
            FileMatcher.HasWildcards(concretePath) ||
            concretePath.IndexOf("$(", StringComparison.Ordinal) >= 0 ||
            concretePath.IndexOf("@(", StringComparison.Ordinal) >= 0)
        {
            return;
        }

        try
        {
            if (!Path.IsPathRooted(concretePath) && !string.IsNullOrEmpty(baseDirectory))
            {
                concretePath = Path.Combine(baseDirectory, concretePath);
            }

            _uniquePaths.Add(Path.GetFullPath(concretePath));
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            NotSupportedException or
            PathTooLongException)
        {
            throw new InvalidOperationException(
                $"Could not normalize native path '{concretePath}' with base directory '{baseDirectory}'.",
                exception);
        }
    }

    internal void AddEnumeration(EvaluationDirectoryEnumerationObservation observation)
    {
        _enumerations.Add(string.Join(
            "|",
            observation.Kind,
            observation.Path,
            observation.SearchPattern,
            observation.SearchOption,
            observation.EntryCount,
            observation.Completion));
    }

    internal void AddGlob(EvaluationGlobObservation observation)
    {
        if (!string.IsNullOrEmpty(observation.Directory))
        {
            _globs.Add(string.Join(
                "|",
                Path.GetFullPath(observation.Directory),
                observation.Include,
                observation.ResultCount,
                observation.ResultsFingerprint));
        }
    }

    internal string SerializePaths()
    {
        StringBuilder result = new();
        foreach (string path in _uniquePaths.OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
        {
            result.Append(EvaluationObservationBenchmarkProtocol.NativePathPrefix);
            result.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(path)));
            result.AppendLine();
        }

        foreach (string enumeration in _enumerations.OrderBy(static value => value, StringComparer.Ordinal))
        {
            result.Append(EvaluationObservationBenchmarkProtocol.NativeEnumerationPrefix);
            result.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(enumeration)));
            result.AppendLine();
        }

        foreach (string glob in _globs.OrderBy(static value => value, StringComparer.Ordinal))
        {
            result.Append(EvaluationObservationBenchmarkProtocol.NativeGlobPrefix);
            result.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(glob)));
            result.AppendLine();
        }

        return result.ToString();
    }

    internal static HashSet<string> ParsePaths(string content)
    {
        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        using StringReader reader = new(content);
        while (reader.ReadLine() is { } line)
        {
            if (!line.StartsWith(EvaluationObservationBenchmarkProtocol.NativePathPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            string encodedPath = line.Substring(EvaluationObservationBenchmarkProtocol.NativePathPrefix.Length);
            paths.Add(Encoding.UTF8.GetString(Convert.FromBase64String(encodedPath)));
        }

        return paths;
    }
}
