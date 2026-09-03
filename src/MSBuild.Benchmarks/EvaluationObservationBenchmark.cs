// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace MSBuild.Benchmarks;

public partial class EvaluationObservationBenchmark
{
    private const int TypicalFileCount = 200;
    private const int GlobHeavyFileCount = 2_000;

    [Params(
        EvaluationObservationBenchmarkScenario.Typical,
        EvaluationObservationBenchmarkScenario.GlobHeavy,
        EvaluationObservationBenchmarkScenario.AmbientAndSdk)]
    public EvaluationObservationBenchmarkScenario Scenario { get; set; }

    [Params(50)]
    public int EvaluationsPerProcess { get; set; }

    private string _root = null!;
    private string _projectPath = null!;
    private string? _previousSdkPath;
    private string? _previousEnvironmentInput;
    private readonly Dictionary<EvaluationObservationBenchmarkMode, Aggregate> _aggregates = new();

    [GlobalSetup]
    public void GlobalSetup()
    {
        _root = Path.Combine(Path.GetTempPath(), $"evaluation-observer-benchmark-{Guid.NewGuid():N}");
        string sourceDirectory = Path.Combine(_root, "src");
        Directory.CreateDirectory(sourceDirectory);

        int fileCount = Scenario == EvaluationObservationBenchmarkScenario.Typical
            ? TypicalFileCount
            : Scenario == EvaluationObservationBenchmarkScenario.GlobHeavy
                ? GlobHeavyFileCount
                : TypicalFileCount;

        for (int i = 0; i < fileCount; i++)
        {
            string directory = Path.Combine(sourceDirectory, $"dir{i % 20}");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, $"File{i}.cs"), string.Empty);
        }

        File.WriteAllText(Path.Combine(_root, "present.marker"), string.Empty);
        File.WriteAllText(Path.Combine(_root, "settings.txt"), "settings-value");

        string sdkRoot = Path.Combine(_root, "Sdks");
        string sdkDirectory = Path.Combine(sdkRoot, "Observed.Sdk", "Sdk");
        Directory.CreateDirectory(sdkDirectory);
        File.WriteAllText(Path.Combine(sdkDirectory, "Sdk.props"), "<Project />");
        File.WriteAllText(Path.Combine(sdkDirectory, "Sdk.targets"), "<Project />");

        _previousSdkPath = Environment.GetEnvironmentVariable("MSBuildSDKsPath");
        _previousEnvironmentInput = Environment.GetEnvironmentVariable("EVALUATION_OBSERVATION_BENCHMARK_ENV");
        Environment.SetEnvironmentVariable("MSBuildSDKsPath", sdkRoot);
        Environment.SetEnvironmentVariable("EVALUATION_OBSERVATION_BENCHMARK_ENV", "benchmark-environment-value");
        File.WriteAllText(
            Path.Combine(_root, "imported.props"),
            """
            <Project>
              <PropertyGroup>
                <ImportedProperty>ImportedValue</ImportedProperty>
              </PropertyGroup>
            </Project>
            """);

        StringBuilder project = new();
        project.AppendLine(Scenario == EvaluationObservationBenchmarkScenario.AmbientAndSdk
            ? "<Project Sdk=\"Observed.Sdk\">"
            : "<Project>");
        project.AppendLine("  <Import Project=\"imported.props\" />");
        project.AppendLine("  <Import Project=\"imported.props\" />");
        project.AppendLine("  <PropertyGroup>");
        project.AppendLine("    <RequestedProperty>$(ImportedProperty)</RequestedProperty>");
        project.AppendLine("    <EscapedProperty>property%3Bvalue</EscapedProperty>");
        project.AppendLine("    <PresentMarker Condition=\"Exists('present.marker')\">true</PresentMarker>");
        project.AppendLine("    <MissingMarker Condition=\"Exists('missing.marker')\">true</MissingMarker>");
        if (Scenario == EvaluationObservationBenchmarkScenario.AmbientAndSdk)
        {
            project.AppendLine("    <ImportedEnvironment>$(EVALUATION_OBSERVATION_BENCHMARK_ENV)</ImportedEnvironment>");
            project.AppendLine("    <LiveEnvironment>$([System.Environment]::GetEnvironmentVariable('EVALUATION_OBSERVATION_BENCHMARK_ENV'))</LiveEnvironment>");
            project.AppendLine("    <Settings>$([System.IO.File]::ReadAllText('$(MSBuildThisFileDirectory)settings.txt'))</Settings>");
            project.AppendLine("    <Above>$([MSBuild]::GetPathOfFileAbove('settings.txt', '$(MSBuildThisFileDirectory)'))</Above>");
            project.AppendLine("    <Volatile>$([System.DateTime]::UtcNow.Kind)</Volatile>");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                project.AppendLine("    <Registry>$([MSBuild]::GetRegistryValue('HKEY_CURRENT_USER\\Software\\MSBuildObservationMissing', 'Value', 'fallback'))</Registry>");
            }
        }
        project.AppendLine("  </PropertyGroup>");
        project.AppendLine("  <ItemDefinitionGroup>");
        project.AppendLine("    <Ordered><Inherited>definition</Inherited><Override>default</Override></Ordered>");
        project.AppendLine("  </ItemDefinitionGroup>");
        project.AppendLine("  <ItemGroup>");
        project.AppendLine("    <Compile Include=\"src/**/*.cs\" />");
        project.AppendLine("    <Ordered Include=\"first\"><Position>1</Position></Ordered>");
        project.AppendLine("    <Ordered Include=\"second\"><Position>2</Position><Override>item</Override></Ordered>");
        project.AppendLine("    <Ordered Include=\"first\"><Position>3</Position></Ordered>");
        project.AppendLine("    <Escaped Include=\"semi%3Bcolon\"><EscapedMetadata>metadata%3Bvalue</EscapedMetadata></Escaped>");
        if (Scenario == EvaluationObservationBenchmarkScenario.AmbientAndSdk)
        {
            project.AppendLine("    <Input Include=\"settings.txt\" />");
            project.AppendLine("    <MetadataValue Include=\"@(Input->'%(ModifiedTime)')\" />");
        }
        project.AppendLine("  </ItemGroup>");
        if (Scenario == EvaluationObservationBenchmarkScenario.AmbientAndSdk)
        {
            project.AppendLine("  <UsingTask TaskName=\"ObservedTask\" AssemblyFile=\"observed-task.dll\" />");
        }
        project.AppendLine("</Project>");

        _projectPath = Path.Combine(_root, "benchmark.proj");
        File.WriteAllText(_projectPath, project.ToString());
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        foreach (KeyValuePair<EvaluationObservationBenchmarkMode, Aggregate> entry in _aggregates)
        {
            Console.WriteLine(entry.Value.Format(entry.Key, Scenario));
        }

        Environment.SetEnvironmentVariable("MSBuildSDKsPath", _previousSdkPath);
        Environment.SetEnvironmentVariable("EVALUATION_OBSERVATION_BENCHMARK_ENV", _previousEnvironmentInput);

        if (Directory.Exists(_root))
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (IOException exception)
            {
                Console.Error.WriteLine($"Could not delete benchmark directory '{_root}': {exception.Message}");
            }
            catch (UnauthorizedAccessException exception)
            {
                Console.Error.WriteLine($"Could not delete benchmark directory '{_root}': {exception.Message}");
            }
        }
    }

    [Benchmark(Baseline = true)]
    public long Baseline() => Run(EvaluationObservationBenchmarkMode.Baseline);

    private long Run(EvaluationObservationBenchmarkMode mode)
    {
        EvaluationObservationBenchmarkResult result = EvaluationObservationBenchmarkProcess.Run(
            mode,
            Scenario,
            _projectPath,
            [_root],
            EvaluationsPerProcess);

        if (!_aggregates.TryGetValue(mode, out Aggregate? aggregate))
        {
            aggregate = new Aggregate();
            _aggregates.Add(mode, aggregate);
        }

        aggregate.Add(result);
        return result.EvaluationTicks;
    }

    private sealed class Aggregate
    {
        private int _samples;
        private long _evaluationTicks;
        private long _minimumEvaluationTicks = long.MaxValue;
        private long _maximumEvaluationTicks = long.MinValue;
        private double _meanEvaluationTicks;
        private double _evaluationTicksM2;
        private long _allocatedManagedBytes;
        private long _retainedManagedBytes;
        private long _privateBytes;
        private long _peakWorkingSetBytes;
        private long _nativeReports;
        private long _nativePathProbes;
        private long _nativeEnumerations;
        private long _nativeMetadataReads;
        private long _nativeFileReads;
        private long _nativeSemanticObservations;
        private long _nativeUniquePaths;
        private long _semanticComparisons;
        private long _semanticImports;
        private long _semanticProperties;
        private long _semanticItems;
        private long _semanticMetadata;
        private long _detoursAccesses;
        private long _detoursUniquePaths;
        private long _nativeDetoursOverlap;
        private long _nativeOnlyPaths;
        private long _detoursOnlyPaths;

        internal void Add(EvaluationObservationBenchmarkResult result)
        {
            _samples++;
            _evaluationTicks += result.EvaluationTicks;
            _minimumEvaluationTicks = Math.Min(_minimumEvaluationTicks, result.EvaluationTicks);
            _maximumEvaluationTicks = Math.Max(_maximumEvaluationTicks, result.EvaluationTicks);

            double delta = result.EvaluationTicks - _meanEvaluationTicks;
            _meanEvaluationTicks += delta / _samples;
            _evaluationTicksM2 += delta * (result.EvaluationTicks - _meanEvaluationTicks);

            _allocatedManagedBytes += result.AllocatedManagedBytes;
            _retainedManagedBytes += result.RetainedManagedBytes;
            _privateBytes += result.PrivateBytes;
            _peakWorkingSetBytes += result.PeakWorkingSetBytes;
            _nativeReports += result.NativeReports;
            _nativePathProbes += result.NativePathProbes;
            _nativeEnumerations += result.NativeEnumerations;
            _nativeMetadataReads += result.NativeMetadataReads;
            _nativeFileReads += result.NativeFileReads;
            _nativeSemanticObservations += result.NativeSemanticObservations;
            _nativeUniquePaths += result.NativeUniquePaths;
            _semanticComparisons += result.SemanticComparisons;
            _semanticImports += result.SemanticImports;
            _semanticProperties += result.SemanticProperties;
            _semanticItems += result.SemanticItems;
            _semanticMetadata += result.SemanticMetadata;
            _detoursAccesses += result.DetoursAccesses;
            _detoursUniquePaths += result.DetoursUniquePaths;
            _nativeDetoursOverlap += result.NativeDetoursOverlap;
            _nativeOnlyPaths += result.NativeOnlyPaths;
            _detoursOnlyPaths += result.DetoursOnlyPaths;
        }

        internal string Format(
            EvaluationObservationBenchmarkMode mode,
            EvaluationObservationBenchmarkScenario scenario)
        {
            return string.Join(
                "|",
                "EVALUATION_OBSERVATION_SUMMARY",
                $"Mode={mode}",
                $"Scenario={scenario}",
                Pair("Samples", _samples),
                Pair("EvaluationTicks", Average(_evaluationTicks)),
                Pair("EvaluationMilliseconds", ToMilliseconds(_meanEvaluationTicks)),
                Pair("EvaluationStdDevMilliseconds", ToMilliseconds(StandardDeviationTicks())),
                Pair("EvaluationMinMilliseconds", ToMilliseconds(_minimumEvaluationTicks)),
                Pair("EvaluationMaxMilliseconds", ToMilliseconds(_maximumEvaluationTicks)),
                Pair("AllocatedManagedBytes", Average(_allocatedManagedBytes)),
                Pair("RetainedManagedBytes", Average(_retainedManagedBytes)),
                Pair("PrivateBytes", Average(_privateBytes)),
                Pair("PeakWorkingSetBytes", Average(_peakWorkingSetBytes)),
                Pair("NativeReports", Average(_nativeReports)),
                Pair("NativePathProbes", Average(_nativePathProbes)),
                Pair("NativeEnumerations", Average(_nativeEnumerations)),
                Pair("NativeMetadataReads", Average(_nativeMetadataReads)),
                Pair("NativeFileReads", Average(_nativeFileReads)),
                Pair("NativeSemanticObservations", Average(_nativeSemanticObservations)),
                Pair("NativeUniquePaths", Average(_nativeUniquePaths)),
                Pair("SemanticComparisons", Average(_semanticComparisons)),
                Pair("SemanticImports", Average(_semanticImports)),
                Pair("SemanticProperties", Average(_semanticProperties)),
                Pair("SemanticItems", Average(_semanticItems)),
                Pair("SemanticMetadata", Average(_semanticMetadata)),
                Pair("DetoursAccesses", Average(_detoursAccesses)),
                Pair("DetoursUniquePaths", Average(_detoursUniquePaths)),
                Pair("NativeDetoursOverlap", Average(_nativeDetoursOverlap)),
                Pair("NativeOnlyPaths", Average(_nativeOnlyPaths)),
                Pair("DetoursOnlyPaths", Average(_detoursOnlyPaths)));
        }

        private long Average(long value) => _samples == 0 ? 0 : value / _samples;

        private static string Pair(string name, long value) =>
            string.Concat(name, "=", value.ToString(CultureInfo.InvariantCulture));

        private static string Pair(string name, double value) =>
            string.Concat(name, "=", value.ToString("F3", CultureInfo.InvariantCulture));

        private double StandardDeviationTicks() =>
            _samples <= 1 ? 0 : Math.Sqrt(_evaluationTicksM2 / (_samples - 1));

        private static double ToMilliseconds(double ticks) =>
            ticks * 1_000d / Stopwatch.Frequency;
    }
}
