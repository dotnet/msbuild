// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Execution;

namespace MSBuild.Benchmarks.Analysis;

/// <summary>
/// Measures where a full project evaluation spends its time and how much file system work it performs.
/// </summary>
/// <remarks>
/// <para>
/// Run with <c>--analyze</c>. The analysis is deliberately not a BenchmarkDotNet benchmark: it needs to observe a
/// single evaluation from the inside (event source markers, injected file system) rather than measure a steady-state
/// throughput number. <see cref="FullEvaluationBenchmark"/> provides the statistically rigorous timings that this
/// analysis is calibrated against.
/// </para>
/// <para>
/// Two cache regimes are reported because they behave very differently:
/// <list type="bullet">
/// <item><description><em>Cold</em>: a fresh <see cref="ProjectCollection"/> and a fresh
/// <see cref="EvaluationContext"/> per evaluation, so no project XML, SDK resolution, or file existence result is
/// reused. This is what a command line MSBuild process does for its first project.</description></item>
/// <item><description><em>Warm</em>: one shared collection and context across evaluations, which is what Visual
/// Studio and project graph scenarios do.</description></item>
/// </list>
/// </para>
/// </remarks>
internal static class EvaluationBreakdownAnalysis
{
    /// <summary>Scopes whose per-payload detail is worth keeping.</summary>
    private static readonly string[] DetailScopes = ["LoadDocument", "Parse", "ExpandGlob", "EvaluateImport", "SdkResolverResolveSdk"];

    /// <summary>
    /// Evaluations discarded before any measurement. Tiered JIT keeps promoting evaluation code for a surprisingly
    /// long time, and without this the first measured scenario is charged for the rest of the run's compilation.
    /// </summary>
    private const int MaxWarmupEvaluations = 400;

    public static int Run(string[] args)
    {
        string? projectPath = GetOption(args, "--project");
        string? outputPath = GetOption(args, "--output");
        int iterations = int.TryParse(GetOption(args, "--iterations"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : 11;
        bool profileOnly = Array.IndexOf(args, "--profile-only") >= 0;
        string? multiProjectDirectory = GetOption(args, "--multi-project");

        MSBuildEnvironment.Ensure(GetOption(args, "--msbuild-exe-path"));

        // Evaluation is single threaded and short; background activity on the machine is the dominant source of
        // run-to-run noise.
        try
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
        }
        catch (Exception e) when (e is UnauthorizedAccessException or PlatformNotSupportedException)
        {
        }

        if (multiProjectDirectory is not null)
        {
            return RunMultiProject(multiProjectDirectory, iterations);
        }

        using ConsoleAppFixture fixture = projectPath is null
            ? ConsoleAppFixture.Create()
            : ConsoleAppFixture.FromExistingProject(projectPath);

        if (profileOnly)
        {
            // A clean workload for an external CPU sampler: warm up, then do nothing but cold evaluations, so every
            // sample outside start-up belongs to evaluation.
            _ = WarmUp(fixture.ProjectFile);
            Console.WriteLine($"Warm-up complete; running {iterations} cold evaluations under the profiler.");
            _ = MeasureCold(fixture.ProjectFile, ProjectEvaluationStage.Full, iterations);
            return 0;
        }

        StringBuilder report = new();
        AppendEnvironment(report, fixture);

        TimeSpan firstEvaluation = Evaluate(fixture.ProjectFile, ProjectEvaluationStage.Full, cold: true, out ProjectInstance firstInstance);

        // Everything after this point measures warmed-up code. The first evaluation is reported on its own because
        // it is dominated by one-time process costs that are not evaluation work.
        (int warmupCount, TimeSpan warmupFinal) = WarmUp(fixture.ProjectFile);

        AppendInventory(report, firstInstance, firstEvaluation, warmupCount, warmupFinal);
        AppendTimings(report, fixture.ProjectFile, iterations, firstEvaluation);

        using (MSBuildMarkerCollector collector = new(DetailScopes))
        {
            AppendMarkerBreakdown(report, collector, fixture.ProjectFile, iterations, cold: true);
            AppendMarkerBreakdown(report, collector, fixture.ProjectFile, iterations, cold: false);
        }

        AppendFileSystemBreakdown(report, fixture.ProjectFile);
        AppendAllocationBreakdown(report, fixture.ProjectFile, iterations);
        AppendDocumentLoadDecomposition(report, fixture.ProjectFile, firstInstance, iterations);
        AppendStabilityCheck(report, fixture.ProjectFile, iterations);

        string text = report.ToString();
        Console.WriteLine(text);

        if (outputPath is not null)
        {
            string fullOutputPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
            File.WriteAllText(fullOutputPath, text);
            Console.WriteLine($"Report written to {fullOutputPath}");
        }

        return 0;
    }

    private static string? GetOption(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static TimeSpan Evaluate(string projectPath, ProjectEvaluationStage stage, bool cold, out ProjectInstance instance)
    {
        using ProjectCollection collection = new();
        ProjectOptions options = new()
        {
            ProjectCollection = collection,
            EvaluationStage = stage,
            EvaluationContext = cold ? EvaluationContext.Create(EvaluationContext.SharingPolicy.Isolated) : null,
        };

        long start = Stopwatch.GetTimestamp();
        instance = ProjectInstance.FromFile(projectPath, options);
        return Stopwatch.GetElapsedTime(start);
    }

    /// <summary>
    /// Evaluates repeatedly until the batch median stops improving, so that later measurements are not charged for
    /// tiered JIT compilation that is still in progress.
    /// </summary>
    /// <remarks>
    /// A single "this batch was not faster than the last" check is not enough: with tiered compilation and dynamic
    /// PGO, evaluation keeps getting faster in steps as individual methods are re-jitted, and a plateau between two
    /// batches is routinely followed by another drop. Requiring several consecutive batches close to the best batch
    /// seen so far is what actually converges.
    /// </remarks>
    /// <returns>The number of warm-up evaluations performed and the final batch median.</returns>
    private static (int Count, TimeSpan Median) WarmUp(string projectPath)
    {
        const int BatchSize = 10;
        const int RequiredStableBatches = 3;
        const double Tolerance = 1.05;

        TimeSpan best = TimeSpan.MaxValue;
        TimeSpan median = TimeSpan.Zero;
        int stableBatches = 0;
        int count = 0;

        while (count < MaxWarmupEvaluations)
        {
            List<TimeSpan> batch = MeasureCold(projectPath, ProjectEvaluationStage.Full, BatchSize);
            count += BatchSize;

            batch.Sort();
            median = batch[BatchSize / 2];

            if (median < best)
            {
                best = median;
            }

            stableBatches = median <= best * Tolerance ? stableBatches + 1 : 0;

            if (stableBatches >= RequiredStableBatches)
            {
                break;
            }
        }

        return (count, median);
    }

    /// <summary>
    /// Evaluates every project under a directory in one <see cref="ProjectCollection"/> and one shared
    /// <see cref="EvaluationContext"/>, the way a solution or graph build does.
    /// </summary>
    /// <remarks>
    /// The single-project warm numbers re-evaluate the <em>same</em> project, which is an idealized upper bound.
    /// A real multi-project build evaluates <em>different</em> projects that happen to share the same SDK imports,
    /// so each one still pays for its own passes while reusing the cached SDK XML. This measures that marginal cost,
    /// which is what determines whether evaluation at scale is dominated by the first project or by the rest.
    /// </remarks>
    private static int RunMultiProject(string directory, int iterations)
    {
        string[] projects = Directory.GetFiles(directory, "*.csproj", SearchOption.AllDirectories);

        if (projects.Length < 2)
        {
            Console.Error.WriteLine($"Need at least two projects under '{directory}'; found {projects.Length}.");
            return 1;
        }

        // Warm the runtime using a project that is not part of the reported sample.
        for (int i = 0; i < 20; i++)
        {
            _ = Evaluate(projects[0], ProjectEvaluationStage.Full, cold: true, out _);
        }

        List<TimeSpan> shared = [];
        List<TimeSpan> sharedSdkCache = [];
        List<TimeSpan> sharedXmlOnly = [];
        List<TimeSpan> isolated = [];

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            using (ProjectCollection collection = new())
            {
                EvaluationContext context = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared);

                foreach (string project in projects)
                {
                    long start = Stopwatch.GetTimestamp();
                    _ = ProjectInstance.FromFile(project, new ProjectOptions
                    {
                        ProjectCollection = collection,
                        EvaluationStage = ProjectEvaluationStage.Full,
                        EvaluationContext = context,
                    });
                    shared.Add(Stopwatch.GetElapsedTime(start));
                }
            }

            // SDK resolution shared, file existence and glob caches isolated per project. This is the closest
            // model of the build path: BuildRequestConfiguration passes a shared sdkResolverService explicitly,
            // but no EvaluationContext, so ProjectInstance.Initialize creates an Isolated one per project.
            using (ProjectCollection collection = new())
            {
                EvaluationContext sdkOnly = EvaluationContext.Create(EvaluationContext.SharingPolicy.SharedSDKCache);

                foreach (string project in projects)
                {
                    long start = Stopwatch.GetTimestamp();
                    _ = ProjectInstance.FromFile(project, new ProjectOptions
                    {
                        ProjectCollection = collection,
                        EvaluationStage = ProjectEvaluationStage.Full,
                        EvaluationContext = sdkOnly,
                    });
                    sharedSdkCache.Add(Stopwatch.GetElapsedTime(start));
                }
            }

            // Shared project XML but a fully isolated EvaluationContext per project, so SDK resolution is
            // repeated too. Included to separate the cost of losing SDK resolution from the cost of losing
            // the file system caches.
            using (ProjectCollection collection = new())
            {
                foreach (string project in projects)
                {
                    long start = Stopwatch.GetTimestamp();
                    _ = ProjectInstance.FromFile(project, new ProjectOptions
                    {
                        ProjectCollection = collection,
                        EvaluationStage = ProjectEvaluationStage.Full,
                        EvaluationContext = EvaluationContext.Create(EvaluationContext.SharingPolicy.Isolated),
                    });
                    sharedXmlOnly.Add(Stopwatch.GetElapsedTime(start));
                }
            }

            foreach (string project in projects)
            {
                isolated.Add(Evaluate(project, ProjectEvaluationStage.Full, cold: true, out _));
            }
        }

        Report("Shared collection + fully shared context (best case)", shared, projects.Length);
        Report("Shared collection + SharedSDKCache context (closest to the build path)", sharedSdkCache, projects.Length);
        Report("Shared collection + fully isolated context per project", sharedXmlOnly, projects.Length);
        Report("Fresh collection and context per project (no sharing at all)", isolated, projects.Length);

        return 0;

        static void Report(string title, List<TimeSpan> samples, int projectCount)
        {
            // The first project of each pass populates the caches; the rest are the marginal cost.
            List<TimeSpan> first = [];
            List<TimeSpan> rest = [];

            for (int i = 0; i < samples.Count; i++)
            {
                (i % projectCount == 0 ? first : rest).Add(samples[i]);
            }

            first.Sort();
            rest.Sort();

            Console.WriteLine(title);
            Console.WriteLine($"  projects per pass          : {projectCount}");
            Console.WriteLine($"  first project (median)     : {first[first.Count / 2].TotalMilliseconds:F1} ms");
            Console.WriteLine($"  each later project (median): {rest[rest.Count / 2].TotalMilliseconds:F1} ms");
            Console.WriteLine($"  total for {projectCount} projects      : {(first[first.Count / 2] + rest[rest.Count / 2] * (projectCount - 1)).TotalMilliseconds:F1} ms");
            Console.WriteLine();
        }
    }

    private static void AppendEnvironment(StringBuilder report, ConsoleAppFixture fixture)
    {
        report.AppendLine("# Full evaluation cost breakdown");
        report.AppendLine();
        report.AppendLine($"- Project: `{fixture.ProjectFile}`");
        report.AppendLine($"- Runtime: {RuntimeInformation.FrameworkDescription}");
        report.AppendLine($"- OS: {RuntimeInformation.OSDescription}");
        report.AppendLine($"- Processors: {Environment.ProcessorCount}");
        report.AppendLine($"- Microsoft.Build: `{typeof(ProjectInstance).Assembly.Location}`");
        report.AppendLine($"- MSBUILD_EXE_PATH: `{Environment.GetEnvironmentVariable("MSBUILD_EXE_PATH")}`");
        report.AppendLine($"- Build configuration: {(IsDebugBuild() ? "**Debug** (numbers are inflated; prefer Release)" : "Release")}");
        report.AppendLine();
    }

    private static bool IsDebugBuild()
        => typeof(ProjectInstance).Assembly
            .GetCustomAttributes(typeof(DebuggableAttribute), inherit: false)
            .OfType<DebuggableAttribute>()
            .Any(a => a.IsJITTrackingEnabled);

    private static void AppendInventory(StringBuilder report, ProjectInstance instance, TimeSpan firstEvaluation, int warmupCount, TimeSpan warmupFinal)
    {
        report.AppendLine("## Inventory");
        report.AppendLine();
        report.AppendLine("What a single evaluation of this project produces. These are the denominators for the per-unit costs below.");
        report.AppendLine();
        report.AppendLine("| Quantity | Count |");
        report.AppendLine("| --- | ---: |");
        report.AppendLine($"| Imported files (unique) | {instance.ImportPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count()} |");
        report.AppendLine($"| Imported files (including duplicates) | {instance.ImportPaths.Count} |");
        report.AppendLine($"| Properties | {instance.Properties.Count} |");
        report.AppendLine($"| Items | {instance.Items.Count} |");
        report.AppendLine($"| Item types | {instance.Items.Select(i => i.ItemType).Distinct(StringComparer.OrdinalIgnoreCase).Count()} |");
        report.AppendLine($"| Item definitions | {instance.ItemDefinitions.Count} |");
        report.AppendLine($"| Targets | {instance.Targets.Count} |");
        report.AppendLine($"| Target child elements (tasks, property and item groups) | {instance.Targets.Values.Sum(t => t.Children.Count)} |");
        report.AppendLine($"| First evaluation in the process | {Format(firstEvaluation)} |");
        report.AppendLine($"| Warm-up evaluations to reach steady state | {warmupCount} (final median {Format(warmupFinal)}) |");
        report.AppendLine();
    }

    private static void AppendTimings(StringBuilder report, string projectPath, int iterations, TimeSpan firstEvaluation)
    {
        report.AppendLine("## Wall clock");
        report.AppendLine();
        report.AppendLine("Uninstrumented timings, measured after warm-up. `Cold` uses a fresh `ProjectCollection` and an");
        report.AppendLine("isolated `EvaluationContext` per evaluation (command line behavior); `Warm` shares both (Visual");
        report.AppendLine("Studio and project graph behavior).");
        report.AppendLine();
        report.AppendLine("| Scenario | Stage | Median | Min | Max | vs cold Full |");
        report.AppendLine("| --- | --- | ---: | ---: | ---: | ---: |");

        TimeSpan baseline = TimeSpan.Zero;

        foreach (ProjectEvaluationStage stage in new[] { ProjectEvaluationStage.Full, ProjectEvaluationStage.Items, ProjectEvaluationStage.Properties })
        {
            TimeSpan cold = AppendSample(report, "Cold", stage, MeasureCold(projectPath, stage, iterations), baseline);

            if (stage == ProjectEvaluationStage.Full)
            {
                baseline = cold;
            }

            AppendSample(report, "Warm", stage, MeasureWarm(projectPath, stage, iterations), baseline);
        }

        report.AppendLine();
        report.AppendLine($"The first evaluation in the process took {Format(firstEvaluation)}. The gap between that and the cold");
        report.AppendLine("median is one-time process cost (JIT, assembly loading, SDK resolver discovery), not evaluation work.");
        report.AppendLine();

        static TimeSpan AppendSample(StringBuilder report, string scenario, ProjectEvaluationStage stage, List<TimeSpan> samples, TimeSpan baseline)
        {
            samples.Sort();
            TimeSpan median = samples[samples.Count / 2];
            string relative = baseline > TimeSpan.Zero ? $"{median.Ticks * 100.0 / baseline.Ticks:F0}%" : "baseline";
            report.AppendLine($"| {scenario} | {stage} | {Format(median)} | {Format(samples[0])} | {Format(samples[^1])} | {relative} |");
            return median;
        }
    }

    private static List<TimeSpan> MeasureCold(string projectPath, ProjectEvaluationStage stage, int iterations)
    {
        List<TimeSpan> samples = new(iterations);

        for (int i = 0; i < iterations; i++)
        {
            samples.Add(Evaluate(projectPath, stage, cold: true, out _));
        }

        return samples;
    }

    private static List<TimeSpan> MeasureWarm(string projectPath, ProjectEvaluationStage stage, int iterations)
    {
        using ProjectCollection collection = new();
        EvaluationContext context = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared);
        List<TimeSpan> samples = new(iterations);

        // The first evaluation populates the shared caches; it is not part of the steady-state sample.
        for (int i = 0; i <= iterations; i++)
        {
            ProjectOptions options = new()
            {
                ProjectCollection = collection,
                EvaluationStage = stage,
                EvaluationContext = context,
            };

            long start = Stopwatch.GetTimestamp();
            _ = ProjectInstance.FromFile(projectPath, options);
            TimeSpan elapsed = Stopwatch.GetElapsedTime(start);

            if (i > 0)
            {
                samples.Add(elapsed);
            }
        }

        return samples;
    }

    private static void AppendMarkerBreakdown(StringBuilder report, MSBuildMarkerCollector collector, string projectPath, int iterations, bool cold)
    {
        using ProjectCollection? warmCollection = cold ? null : new ProjectCollection();
        EvaluationContext? warmContext = cold ? null : EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared);

        // Prime the listener path (and, for the warm scenario, the shared caches) before measuring.
        collector.Start();
        RunOnce();
        collector.Stop();
        collector.Reset();

        collector.Start();
        long start = Stopwatch.GetTimestamp();

        for (int i = 0; i < iterations; i++)
        {
            RunOnce();
        }

        TimeSpan instrumentedTotal = Stopwatch.GetElapsedTime(start);
        collector.Stop();

        report.AppendLine($"## Where the time goes ({(cold ? "cold" : "warm")} evaluation)");
        report.AppendLine();
        report.AppendLine($"{iterations} evaluations. Instrumented cost per evaluation: {Format(instrumentedTotal / iterations)}, of which");
        report.AppendLine($"{Format(collector.MeasuredOverhead / iterations)} is listener overhead ({collector.EventCount / iterations} marker callbacks per evaluation).");
        report.AppendLine();
        report.AppendLine("`Inclusive` counts nested scopes; `Exclusive` is time spent in the scope itself. Values are per evaluation.");
        report.AppendLine();
        report.AppendLine("| Scope | Calls/eval | Inclusive | Incl % | Exclusive | Excl % |");
        report.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: |");

        TimeSpan total = collector.Scopes.TryGetValue("Evaluate", out ScopeStats? evaluate) ? evaluate.Inclusive : instrumentedTotal;

        foreach ((string scope, ScopeStats stats) in collector.Scopes.OrderByDescending(kvp => kvp.Value.Inclusive))
        {
            report.AppendLine(
                $"| {scope} | {stats.Count / (double)iterations:F1} | {Format(stats.Inclusive / iterations)} | {Percent(stats.Inclusive, total)} " +
                $"| {Format(stats.Exclusive / iterations)} | {Percent(stats.Exclusive, total)} |");
        }

        report.AppendLine();

        if (collector.UnpairedEvents.Count > 0)
        {
            report.AppendLine("Unpaired markers (these do not participate in the nesting reconstruction): " +
                string.Join(", ", collector.UnpairedEvents.OrderByDescending(kvp => kvp.Value).Select(kvp => $"`{kvp.Key}` x{kvp.Value}")));
            report.AppendLine();
        }

        if (cold)
        {
            AppendDetail(report, collector, iterations);
        }

        void RunOnce()
        {
            if (cold)
            {
                _ = Evaluate(projectPath, ProjectEvaluationStage.Full, cold: true, out _);
                return;
            }

            _ = ProjectInstance.FromFile(projectPath, new ProjectOptions
            {
                ProjectCollection = warmCollection,
                EvaluationStage = ProjectEvaluationStage.Full,
                EvaluationContext = warmContext,
            });
        }
    }

    private static void AppendDetail(StringBuilder report, MSBuildMarkerCollector collector, int iterations)
    {
        if (collector.Detail.Count == 0)
        {
            return;
        }

        report.AppendLine("### Most expensive individual files, globs, imports and SDK resolutions");
        report.AppendLine();
        report.AppendLine("| Scope | Payload | Calls/eval | Inclusive | Exclusive |");
        report.AppendLine("| --- | --- | ---: | ---: | ---: |");

        foreach ((string key, ScopeStats stats) in collector.Detail.OrderByDescending(kvp => kvp.Value.Exclusive).Take(30))
        {
            int separator = key.IndexOf('|');
            string scope = key[..separator];
            string payload = key[(separator + 1)..];
            report.AppendLine($"| {scope} | `{Shorten(payload)}` | {stats.Count / (double)iterations:F1} | {Format(stats.Inclusive / iterations)} | {Format(stats.Exclusive / iterations)} |");
        }

        report.AppendLine();

        static string Shorten(string payload)
        {
            string name = payload.Contains(Path.DirectorySeparatorChar) ? Path.GetFileName(payload) : payload;
            return name.Length <= 70 ? name : name[..70] + "...";
        }
    }

    private static void AppendFileSystemBreakdown(StringBuilder report, string projectPath)
    {
        CountingFileSystem fileSystem = new();

        using (ProjectCollection collection = new())
        {
            _ = ProjectInstance.FromFile(projectPath, new ProjectOptions
            {
                ProjectCollection = collection,
                EvaluationStage = ProjectEvaluationStage.Full,
                EvaluationContext = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared, fileSystem),
            });
        }

        report.AppendLine("## File system work");
        report.AppendLine();
        report.AppendLine("One cold evaluation, observed through an injected `MSBuildFileSystemBase` that reproduces MSBuild's");
        report.AppendLine("own caching. `Logical` is what the evaluator asked for; `Real` is what reached the operating system.");
        report.AppendLine();
        report.AppendLine("> Project XML reads are **not** included here: `XmlReaderExtension` opens a `FileStream` directly and");
        report.AppendLine("> bypasses the file system abstraction. See the XML document loading section below for that cost.");
        report.AppendLine();
        report.AppendLine("| Operation | Logical | Cache hits | Real | Positive results | Time |");
        report.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: |");

        foreach ((FileOperationKind kind, FileOperationStats stats) in fileSystem.Stats.OrderByDescending(kvp => kvp.Value.LogicalCalls))
        {
            report.AppendLine($"| {kind} | {stats.LogicalCalls} | {stats.CacheHits} | {stats.RealCalls} | {stats.PositiveResults} | {Format(stats.Elapsed)} |");
        }

        report.AppendLine($"| **Total** | **{fileSystem.TotalLogicalCalls}** | | **{fileSystem.TotalRealCalls}** | | **{Format(fileSystem.TotalElapsed)}** |");
        report.AppendLine();

        List<KeyValuePair<string, int>> repeated = fileSystem.PathProbeCounts
            .Where(kvp => kvp.Value > 1)
            .OrderByDescending(kvp => kvp.Value)
            .ToList();

        report.AppendLine($"Distinct paths touched: {fileSystem.PathProbeCounts.Count}. Paths probed more than once: {repeated.Count} " +
            $"({repeated.Sum(kvp => kvp.Value - 1)} redundant logical probes, all absorbed by the cache).");
        report.AppendLine();

        if (repeated.Count > 0)
        {
            report.AppendLine("| Most repeatedly probed path | Probes |");
            report.AppendLine("| --- | ---: |");

            foreach ((string path, int count) in repeated.Take(10))
            {
                report.AppendLine($"| `{path}` | {count} |");
            }

            report.AppendLine();
        }
    }

    /// <summary>
    /// Measures allocation volume and garbage collection cost, which CPU profiles attribute to GC poll sites spread
    /// across every category and therefore hide.
    /// </summary>
    private static void AppendAllocationBreakdown(StringBuilder report, string projectPath, int iterations)
    {
        report.AppendLine("## Allocations and garbage collection");
        report.AppendLine();
        report.AppendLine("A cold evaluation builds an entire construction model (one DOM per imported file) and throws it");
        report.AppendLine("away, so allocation volume is a first-class cost rather than a side effect.");
        report.AppendLine();
        report.AppendLine("| Scenario | Stage | Allocated/eval | Gen0/eval | Gen1/eval | Gen2/eval | GC pause/eval |");
        report.AppendLine("| --- | --- | ---: | ---: | ---: | ---: | ---: |");

        Measure("Cold", ProjectEvaluationStage.Full, cold: true);
        Measure("Cold", ProjectEvaluationStage.Properties, cold: true);
        Measure("Warm", ProjectEvaluationStage.Full, cold: false);

        report.AppendLine();

        void Measure(string scenario, ProjectEvaluationStage stage, bool cold)
        {
            using ProjectCollection? warmCollection = cold ? null : new ProjectCollection();
            EvaluationContext? warmContext = cold ? null : EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared);

            RunOnce();

            long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
            TimeSpan pauseBefore = GC.GetTotalPauseDuration();
            int gen0 = GC.CollectionCount(0);
            int gen1 = GC.CollectionCount(1);
            int gen2 = GC.CollectionCount(2);

            for (int i = 0; i < iterations; i++)
            {
                RunOnce();
            }

            long allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
            TimeSpan pause = GC.GetTotalPauseDuration() - pauseBefore;

            report.AppendLine(
                $"| {scenario} | {stage} | {allocated / (double)iterations / (1024 * 1024):F1} MB " +
                $"| {(GC.CollectionCount(0) - gen0) / (double)iterations:F2} " +
                $"| {(GC.CollectionCount(1) - gen1) / (double)iterations:F2} " +
                $"| {(GC.CollectionCount(2) - gen2) / (double)iterations:F2} " +
                $"| {Format(pause / iterations)} |");

            void RunOnce()
            {
                if (cold)
                {
                    _ = Evaluate(projectPath, stage, cold: true, out _);
                    return;
                }

                _ = ProjectInstance.FromFile(projectPath, new ProjectOptions
                {
                    ProjectCollection = warmCollection,
                    EvaluationStage = stage,
                    EvaluationContext = warmContext,
                });
            }
        }
    }

    /// <summary>
    /// Splits the cost of MSBuild's <c>LoadDocument</c> into reading bytes, tokenizing XML, building a plain DOM,
    /// and the extra work <c>XmlDocumentWithLocation</c> does on top of a stock DOM.
    /// </summary>
    /// <remarks>
    /// The reader configuration mirrors <c>XmlReaderExtension</c> exactly (an <see cref="XmlTextReader"/> over a
    /// <see cref="StreamReader"/> with BOM detection and <see cref="DtdProcessing.Ignore"/>), so the comparison is
    /// apples to apples. The operating system file cache is warm for all measurements, which means the "read bytes"
    /// number is a floor rather than what a genuinely cold disk would cost.
    /// </remarks>
    private static void AppendDocumentLoadDecomposition(StringBuilder report, string projectPath, ProjectInstance instance, int iterations)
    {
        string[] files = instance.ImportPaths
            .Concat([projectPath])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(File.Exists)
            .ToArray();

        long bytes = files.Sum(f => new FileInfo(f).Length);

        // Warm the operating system cache so the comparison isolates CPU cost rather than first-touch disk cost.
        foreach (string file in files)
        {
            _ = File.ReadAllBytes(file);
        }

        TimeSpan read = MeasureMedian(iterations, () =>
        {
            foreach (string file in files)
            {
                _ = File.ReadAllBytes(file);
            }
        });

        // ProjectRootElement.LoadDocument stats every file it loads (to record LastWriteTime for cache
        // invalidation) on top of opening it, so the stat is a real per-file cost of loading a document.
        TimeSpan stat = MeasureMedian(iterations, () =>
        {
            foreach (string file in files)
            {
                _ = new FileInfo(file).LastWriteTimeUtc;
            }
        });

        TimeSpan tokenize = MeasureMedian(iterations, () =>
        {
            foreach (string file in files)
            {
                using XmlReader reader = CreateReader(file);
                while (reader.Read())
                {
                }
            }
        });

        TimeSpan plainDom = MeasureMedian(iterations, () =>
        {
            foreach (string file in files)
            {
                using XmlReader reader = CreateReader(file);
                XmlDocument document = new();
                document.Load(reader);
            }
        });

        // MSBuild does not use a stock XmlDocument: XmlDocumentWithLocation attaches line/column information to
        // every element and attribute. Measuring it directly (it is internal, hence the reflection) isolates that
        // cost from the underlying XML work.
        Func<XmlDocument>? createLocationAwareDocument = TryGetXmlDocumentWithLocationFactory();
        TimeSpan? locationDom = createLocationAwareDocument is null
            ? null
            : MeasureMedian(iterations, () =>
            {
                foreach (string file in files)
                {
                    using XmlReader reader = CreateReader(file);
                    XmlDocument document = createLocationAwareDocument();
                    document.Load(reader);
                }
            });

        report.AppendLine("## XML document loading");
        report.AppendLine();
        report.AppendLine("`LoadDocument` is the single largest scope in a cold evaluation. This decomposes it over the");
        report.AppendLine($"{files.Length} files an evaluation reads ({bytes / 1024.0:F0} KB total), using the same reader configuration");
        report.AppendLine("as `XmlReaderExtension` and a warm operating system file cache.");
        report.AppendLine();
        report.AppendLine("| Step | Time | Notes |");
        report.AppendLine("| --- | ---: | --- |");
        report.AppendLine($"| Stat every file | {Format(stat)} | `FileInfo.LastWriteTimeUtc`. `LoadDocument` does this once per file on top of opening it. |");
        report.AppendLine($"| Read bytes only | {Format(read)} | `File.ReadAllBytes` over every file: open, read, close. |");
        report.AppendLine($"| Tokenize XML | {Format(tokenize)} | `XmlTextReader.Read()` to end of file; includes the read. |");
        report.AppendLine($"| Build a plain `XmlDocument` | {Format(plainDom)} | Includes read and tokenize. |");

        if (locationDom is { } withLocation)
        {
            report.AppendLine($"| Build an `XmlDocumentWithLocation` | {Format(withLocation)} | What MSBuild actually does. Includes read and tokenize. |");
        }

        report.AppendLine();
        report.AppendLine("Marginal cost of each layer:");
        report.AppendLine();
        report.AppendLine("| Layer | Time | Share of the loaded-document cost |");
        report.AppendLine("| --- | ---: | ---: |");

        TimeSpan totalDocumentCost = locationDom ?? plainDom;
        report.AppendLine($"| Reading bytes (open, read, close) | {Format(read)} | {Percent(read, totalDocumentCost)} |");
        report.AppendLine($"| Tokenizing XML | {Format(tokenize - read)} | {Percent(tokenize - read, totalDocumentCost)} |");
        report.AppendLine($"| Building a stock DOM | {Format(plainDom - tokenize)} | {Percent(plainDom - tokenize, totalDocumentCost)} |");

        if (locationDom is { } withLocation2)
        {
            report.AppendLine($"| Attaching element locations | {Format(withLocation2 - plainDom)} | {Percent(withLocation2 - plainDom, totalDocumentCost)} |");
        }

        report.AppendLine();

        static XmlReader CreateReader(string file)
        {
            FileStream stream = new(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            StreamReader streamReader = new(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), detectEncodingFromByteOrderMarks: true);
            string uri = new UriBuilder(Uri.UriSchemeFile, string.Empty) { Path = file }.ToString();

            return new XmlTextReader(uri, streamReader) { DtdProcessing = DtdProcessing.Ignore };
        }
    }

    /// <summary>
    /// Builds a factory for MSBuild's internal location-tracking <see cref="XmlDocument"/>, or <see langword="null"/>
    /// if the type or its constructor cannot be found (for example after a rename).
    /// </summary>
    private static Func<XmlDocument>? TryGetXmlDocumentWithLocationFactory()
    {
        Type? type = typeof(ProjectInstance).Assembly.GetType("Microsoft.Build.Construction.XmlDocumentWithLocation", throwOnError: false);
        ConstructorInfo? constructor = type?.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, binder: null, Type.EmptyTypes, modifiers: null);

        return constructor is null ? null : () => (XmlDocument)constructor.Invoke(null);
    }

    /// <summary>
    /// Re-measures the cold full evaluation at the very end of the run. If this disagrees with the number reported at
    /// the start, the process had not reached steady state and every absolute number in the report should be treated
    /// as an upper bound. Relative shares remain valid either way.
    /// </summary>
    private static void AppendStabilityCheck(StringBuilder report, string projectPath, int iterations)
    {
        List<TimeSpan> samples = MeasureCold(projectPath, ProjectEvaluationStage.Full, iterations);
        samples.Sort();

        report.AppendLine("## Steady state check");
        report.AppendLine();
        report.AppendLine($"Cold full evaluation re-measured at the end of the run: median {Format(samples[samples.Count / 2])} ");
        report.AppendLine($"(min {Format(samples[0])}, max {Format(samples[^1])}). Compare against the wall clock table above; a large");
        report.AppendLine("gap means tiered compilation was still in progress and the absolute numbers are upper bounds.");
        report.AppendLine();
    }

    private static TimeSpan MeasureMedian(int iterations, Action action)    {
        List<TimeSpan> samples = new(iterations);

        for (int i = 0; i < iterations; i++)
        {
            long start = Stopwatch.GetTimestamp();
            action();
            samples.Add(Stopwatch.GetElapsedTime(start));
        }

        samples.Sort();
        return samples[samples.Count / 2];
    }

    private static string Format(TimeSpan value) => $"{value.TotalMilliseconds:F2} ms";

    private static string Percent(TimeSpan value, TimeSpan total)
        => total > TimeSpan.Zero ? $"{value.Ticks * 100.0 / total.Ticks:F1}%" : "n/a";
}
