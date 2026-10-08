// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Shouldly;
using Xunit;

#nullable enable

namespace Microsoft.Build.UnitTests.BackEnd;

public sealed class EvaluationCacheDiagnostics_Tests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void DiagnosticsDoNotChangeReuse(bool enabled, bool multiThreaded)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        Configure(env, enabled, EvaluationCacheMode.SnapshotFileSystem);
        string project = env.CreateFile("reuse.proj", """<Project><Target Name="Build" /></Project>""").Path;
        var logger = new MockLogger(_output);
        using var manager = new BuildManager();
        var parameters = new BuildParameters { Loggers = [logger], MultiThreaded = multiThreaded };
        ProjectInstanceSnapshotCache? cache = null;
        for (int i = 0; i < 2; i++)
        {
            int firstMessage = logger.BuildMessageEvents.Count;
            manager.BeginBuild(parameters);
            cache = ((IBuildComponentHost)manager).BuildParameters.ProjectInstanceSnapshotCache;
            manager.BuildRequest(new BuildRequestData(project, new Dictionary<string, string?>(), null, ["Build"], null))
                .ShouldHaveSucceeded();
            manager.EndBuild();
            if (enabled)
            {
                string buildLog = string.Join(Environment.NewLine, logger.BuildMessageEvents.Skip(firstMessage).Select(message => message.Message));
                if (i == 0)
                {
                    AssertPhases(buildLog, ("RequestKey", 1), ("CacheLookup", 1), ("FreshEvaluation", 1),
                        ("SnapshotCreation", 1), ("CacheAdmission", 1));
                }
                else
                {
                    AssertPhases(buildLog, ("RequestKey", 1), ("CacheLookup", 1), ("Validation", 1),
                        ("RootElementCheck", 1), ("KeyCheck", 1), ("ManifestValidation", 1), ("EnvironmentCheck", 1),
                        ("FileStatLoop", 1), ("StatClassification", 1), ("DiagnosticsPublish", 1), ("Materialization", 1));
                }
            }
        }
        cache.ShouldNotBeNull();
        cache.MaterializedEntries.ShouldBe(1);
        cache.GetStatistics().FreshEvaluations.ShouldBe(1);
        if (enabled)
        {
            logger.FullLog.ShouldContain("|Reason=NoEntryOrHistory|");
            logger.FullLog.ShouldContain("|Reason=Materialized|");
            logger.FullLog.ShouldContain("|Build=2|");
            logger.FullLog.ShouldContain("|Kind=Process|WallMilliseconds=");
            logger.FullLog.ShouldContain("|GcPauseMilliseconds=");
            logger.FullLog.ShouldContain("|Counts=Lifecycle.BuildStarted:1,Lookup.CandidateFound:1,Reuse.Materialized:1,Validation.Accepted:1");
            logger.BuildMessageEvents.Where(message => message.Message?.StartsWith("EvaluationCacheTiming", StringComparison.Ordinal) == true)
                .ShouldAllBe(message => message.Importance == MessageImportance.High);
            logger.BuildMessageEvents.Where(message => message.Message?.StartsWith("EvaluationCacheExperimentStatus|", StringComparison.Ordinal) == true)
                .ShouldAllBe(message => message.Importance == MessageImportance.High);
            logger.FullLog.ShouldNotContain("EvaluationCacheTimingExample|");
        }
        else
        {
            cache.Diagnostics.ShouldBeNull();
            logger.FullLog.ShouldNotContain("EvaluationCacheDiagnostic");
            logger.FullLog.ShouldNotContain("EvaluationCacheTiming");
            logger.BuildMessageEvents.Where(message => message.Message?.StartsWith("EvaluationCacheExperimentStatus|", StringComparison.Ordinal) == true)
                .ShouldAllBe(message => message.Importance == MessageImportance.Low);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DiagnosticsAloneDoNotActivateCache(bool restore)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        Configure(env, true, EvaluationCacheMode.Disabled);
        env.SetEnvironmentVariable(EvaluationCacheConfiguration.ModeEnvironmentVariable, null);
        env.SetEnvironmentVariable("MSBUILDENABLEPROJECTINSTANCESNAPSHOTCACHE", null);
        env.SetEnvironmentVariable("MSBUILDRECORDEVALUATIONINPUTS", null);
        Traits.UpdateFromEnvironment();
        string project = env.CreateFile("disabled.proj", """<Project><Target Name="Build" /></Project>""").Path;
        var logger = new MockLogger(_output);
        using var manager = new BuildManager();
        manager.BeginBuild(new BuildParameters { Loggers = [logger] });
        ((IBuildComponentHost)manager).BuildParameters.ProjectInstanceSnapshotCache.ShouldBeNull();
        var globals = new Dictionary<string, string?>();
        if (restore)
        {
            globals["MSBuildRestoreSessionId"] = "private-restore-value";
        }
        manager.BuildRequest(new BuildRequestData(project, globals, null, ["Build"], null))
            .ShouldHaveSucceeded();
        manager.EndBuild();
        logger.FullLog.ShouldContain("|Reason=CacheDisabled");
        logger.FullLog.ShouldNotContain("|Reason=Stored");
        AssertPhases(logger.FullLog, (restore ? "RestoreEvaluation" : "FreshEvaluation", 1));
    }

    [Fact]
    public void RestoreBypassAndValidationRejectionAreVisible()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        Configure(env, true, EvaluationCacheMode.SnapshotFileSystem);
        string project = env.CreateFile("changing.proj", "<Project />").Path;
        var cache = new ProjectInstanceSnapshotCache();
        var parameters = new BuildParameters
        {
            EvaluationCacheConfiguration = Traits.Instance.EvaluationCache,
            ProjectInstanceSnapshotCache = cache,
        };
        cache.ConfigureValidator(EvaluationCacheValidationPolicy.FileSystem);
        cache.ConfigureDiagnostics(true, "manager", 1, EvaluationCacheMode.SnapshotFileSystem, parameters.EnvironmentPropertiesInternal);
        var host = new MockHost(parameters) { LoggingService = new MockLoggingService(_output.WriteLine) };
        Load(project, parameters, host);
        AssertPhases(Flush(cache.Diagnostics!), ("RequestKey", 1), ("CacheLookup", 1), ("FreshEvaluation", 1),
            ("SnapshotCreation", 1), ("CacheAdmission", 1));
        Load(project, parameters, host, new Dictionary<string, string?> { ["MSBuildRestoreSessionId"] = "private-restore-value" });
        string restoreLog = Flush(cache.Diagnostics!);
        restoreLog.ShouldContain("|Event=Bypass|Reason=RestoreRequest|");
        restoreLog.ShouldNotContain("EvaluationCacheTimingExample|");
        restoreLog.ShouldNotContain("private-restore-value");
        AssertPhases(restoreLog, ("RestoreEvaluation", 1));
        File.WriteAllText(project, "<Project><PropertyGroup><Changed>yes</Changed></PropertyGroup></Project>");
        Load(project, parameters, host);
        string log = Flush(cache.Diagnostics!);
        log.ShouldContain("|Event=Validation|Reason=FileSystemInputChanged|");
        log.ShouldContain(EvaluationCacheDiagnostics.Escape(project));
        log.ShouldNotContain("private-restore-value");
        cache.GetStatistics().FreshEvaluations.ShouldBe(3);
        AssertPhases(log, ("RequestKey", 1), ("CacheLookup", 1), ("Validation", 1), ("RootElementCheck", 1), ("KeyCheck", 1),
            ("ManifestValidation", 1), ("EnvironmentCheck", 1), ("FileStatLoop", 1), ("StatClassification", 1), ("DiagnosticsPublish", 1),
            ("FreshEvaluation", 1), ("SnapshotCreation", 1), ("CacheAdmission", 2), ("FallbackPreparation", 1));
        Examples(log).Single().ShouldContain("|Event=Validation|Reason=FileSystemInputChanged|");
    }

    [Fact]
    public void ValidationDetailSeparatesStatTimeFromGlobReplay()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        Configure(env, true, EvaluationCacheMode.SnapshotFileSystem);
        TransientTestFolder folder = env.CreateFolder();
        env.CreateFile(folder, "a.txt", "a");
        string project = env.CreateFile(folder, "glob.proj", "<Project><ItemGroup><Item Include=\"*.txt\" /></ItemGroup></Project>").Path;
        var cache = new ProjectInstanceSnapshotCache();
        var parameters = new BuildParameters
        {
            EvaluationCacheConfiguration = Traits.Instance.EvaluationCache,
            ProjectInstanceSnapshotCache = cache,
        };
        cache.ConfigureValidator(EvaluationCacheValidationPolicy.FileSystem);
        cache.ConfigureDiagnostics(true, "manager", 1, EvaluationCacheMode.SnapshotFileSystem, parameters.EnvironmentPropertiesInternal);
        var host = new MockHost(parameters) { LoggingService = new MockLoggingService(_output.WriteLine) };
        Load(project, parameters, host);
        Flush(cache.Diagnostics!);

        File.WriteAllText(Path.Combine(folder.Path, "b.txt"), "b");
        Directory.SetLastWriteTimeUtc(folder.Path, Directory.GetLastWriteTimeUtc(folder.Path).AddSeconds(2));
        Load(project, parameters, host);
        string log = Flush(cache.Diagnostics!);

        log.ShouldContain("|Phase=FileStatLoop|NestedUnder=ManifestValidation|Count=1|");
        log.ShouldContain("|Phase=GlobReplay|NestedUnder=ManifestValidation|Count=1|");
        log.ShouldContain("|Phase=EnvironmentCheck|NestedUnder=ManifestValidation|Count=1|");
        log.ShouldContain("|Phase=StatClassification|NestedUnder=ManifestValidation|Count=1|");
        log.ShouldContain("|Phase=GlobBookkeeping|NestedUnder=ManifestValidation|Count=1|");
        log.ShouldContain("|Phase=RootElementCheck|NestedUnder=Validation|Count=1|");
        log.ShouldContain("|Phase=KeyCheck|NestedUnder=Validation|Count=1|");
        log.ShouldContain("|Phase=DiagnosticsPublish|NestedUnder=Validation|Count=1|");
        System.Text.RegularExpressions.Regex.IsMatch(log, "\\|Phase=Validation\\|[^\\r\\n]*\\|PeakActive=1\\|BusyMilliseconds=").ShouldBeTrue();

        // Phases added after the fact never had a request inside them, so they report no concurrency at all.
        System.Text.RegularExpressions.Regex.IsMatch(log, "\\|Phase=FileStatLoop\\|[^\\r\\n]*PeakActive").ShouldBeFalse();
        log.ShouldContain("|Kind=Buckets|Family=StatClass|Buckets=");
        log.ShouldContain("|Kind=Buckets|Family=StatLatency|Buckets=");
        log.ShouldContain("|Kind=Buckets|Family=Phase.Validation|Buckets=");
        log.ShouldContain("ValidationDetail.RootCacheProbes:");
        log.ShouldContain("ValidationDetail.RootCacheHits:");
        log.ShouldContain("ValidationDetail.GlobsRecorded:1");
        log.ShouldContain("ValidationDetail.GlobsReplayed:1");
        log.ShouldContain("ValidationDetail.ReplaysByDirectoryStamp:1");
        log.ShouldContain("ValidationDetail.ReplayMismatches:1");
        log.ShouldContain("ValidationDetail.ChangedGlobDirectories:");
        log.ShouldContain("ValidationDetail.RecordedFiles:");
        log.ShouldContain("ValidationDetail.ListedDirectories:");
        System.Text.RegularExpressions.Regex.IsMatch(log, "ValidationDetail\\.Driver(Legacy|OptimizedCallback|OptimizedDirect):1").ShouldBeTrue();
    }

    [Fact]
    public void OverlappingScopesOnTwoThreadsReportPeakAndBusyTime()
    {
        var diagnostics = new EvaluationCacheDiagnostics();
        diagnostics.BeginBuild("manager", 1, EvaluationCacheMode.SnapshotFileSystem, []);
        EvaluationCacheDiagnostics.Request first = diagnostics.StartRequest("first.proj", 1, 1, 1);
        EvaluationCacheDiagnostics.Request second = diagnostics.StartRequest("second.proj", 2, 1, 2);
        using var firstEntered = new System.Threading.ManualResetEventSlim();
        using var secondExited = new System.Threading.ManualResetEventSlim();

        // The second request runs entirely inside the first, so the time with someone inside is exactly the first's.
        Task outer = Task.Run(() =>
        {
            using (first.Time(EvaluationCacheDiagnostics.Phase.Validation))
            {
                firstEntered.Set();
                secondExited.Wait();
                System.Threading.Thread.Sleep(20);
            }
        });
        Task inner = Task.Run(() =>
        {
            firstEntered.Wait();
            using (second.Time(EvaluationCacheDiagnostics.Phase.Validation))
            {
                System.Threading.Thread.Sleep(20);
            }

            secondExited.Set();
        });
        Task.WaitAll([outer, inner], TimeSpan.FromSeconds(30)).ShouldBeTrue();

        string log = Flush(diagnostics);
        Dictionary<string, string> validation = PhaseFields(log, "Validation");
        validation["Count"].ShouldBe("2");
        validation["PeakActive"].ShouldBe("2");
        validation["BusyMilliseconds"].ShouldBe(validation["MaxMilliseconds"]);
        double.Parse(validation["TotalMilliseconds"], CultureInfo.InvariantCulture)
            .ShouldBeGreaterThan(double.Parse(validation["BusyMilliseconds"], CultureInfo.InvariantCulture));
        Buckets(log, "Phase.Validation").Values.Sum().ShouldBe(2);
    }

    [Fact]
    public void ManyThreadsEnteringAndLeavingAPhaseStayWithinWallTime()
    {
        var diagnostics = new EvaluationCacheDiagnostics();
        diagnostics.BeginBuild("manager", 1, EvaluationCacheMode.SnapshotFileSystem, []);
        EvaluationCacheDiagnostics.Request request = diagnostics.StartRequest("many.proj", 1, 1, 1);

        long start = Stopwatch.GetTimestamp();
        Parallel.For(0, 4000, new ParallelOptions { MaxDegreeOfParallelism = 8 }, _ =>
        {
            using (request.Time(EvaluationCacheDiagnostics.Phase.Validation))
            {
                System.Threading.Thread.SpinWait(200);
            }
        });
        double wallMilliseconds = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;

        Dictionary<string, string> validation = PhaseFields(Flush(diagnostics), "Validation");
        validation["Count"].ShouldBe("4000");
        int peak = int.Parse(validation["PeakActive"], CultureInfo.InvariantCulture);
        peak.ShouldBeInRange(1, 8);
        double busy = double.Parse(validation["BusyMilliseconds"], CultureInfo.InvariantCulture);
        busy.ShouldBeGreaterThan(0);
        busy.ShouldBeLessThanOrEqualTo(wallMilliseconds + 5);
    }

    [Fact]
    public void StatsThatReachTheFileSystemAreClassifiedByPlaceResultAndRepetition()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        TransientTestFolder root = env.CreateFolder();
        TransientTestFolder repo = env.CreateFolder();
        string underRoot = env.CreateFile(root, "toolset.props", "x").Path;
        string missingUnderRoot = Path.Combine(root.Path, "absent.props");
        string repoFile = env.CreateFile(repo, "repo.props", "x").Path;
        string output = Path.Combine(repo.Path, "obj");
        Directory.CreateDirectory(output);

        var cache = new Microsoft.Build.Evaluation.Context.ImmutableFileStatCache([root.Path]);
        var measurements = new Microsoft.Build.Evaluation.Context.ValidationMeasurements();
        foreach (string path in new[] { underRoot, underRoot, missingUnderRoot, missingUnderRoot, repoFile, repoFile, output })
        {
            cache.TryStat(path, out _, measurements).ShouldBeTrue();
        }

        var diagnostics = new EvaluationCacheDiagnostics();
        measurements.Publish(diagnostics.StartRequest("stats.proj", 1, 1, 1));
        string log = Flush(diagnostics);

        // The second look at the existing file under the root is served from the shared cache and never reaches the file system.
        Buckets(log, "StatClass").ShouldBe(
            new Dictionary<string, long>
            {
                ["Toolset.File.First"] = 1,
                ["Toolset.Missing.First"] = 1,
                ["Toolset.Missing.Repeat"] = 1,
                ["Repo.File.First"] = 1,
                ["Repo.File.Repeat"] = 1,
                ["Output.Directory.First"] = 1,
            },
            ignoreOrder: true);
        Buckets(log, "StatLatency").Values.Sum().ShouldBe(6);
        Buckets(log, "StatLatencyByInFlight").Values.Sum().ShouldBe(6);
        Buckets(log, "StatBurstAll").Values.Sum().ShouldBe(6);
        Buckets(log, "StatGc").Where(bucket => bucket.Key.StartsWith("All.", StringComparison.Ordinal)).Sum(bucket => bucket.Value).ShouldBe(6);
        log.ShouldContain("|Phase=StatClassification|NestedUnder=ManifestValidation|Count=1|");
    }

    [Fact]
    public void SlowStatsAreReportedByPathAndDirectoryAndEveryStatIsBandedByConcurrencyAndTime()
    {
        string directory = Path.Combine(Path.GetTempPath(), "slow-directory");
        string slow = Path.Combine(directory, "missing.props");
        string alsoSlow = Path.Combine(directory, "other.props");
        long slowTicks = 5 * Stopwatch.Frequency / 1000;
        long Milliseconds(long value) => value * Stopwatch.Frequency / 1000;

        var measurements = new Microsoft.Build.Evaluation.Context.ValidationMeasurements();
        measurements.CountStatContext(slow, inFlight: 40, startOffsetTicks: Milliseconds(120), gcOverlapped: false, ticks: slowTicks);
        measurements.CountStatContext(slow, inFlight: 1, startOffsetTicks: 0, gcOverlapped: false, ticks: 1);
        measurements.CountStatContext(alsoSlow, inFlight: 3, startOffsetTicks: Milliseconds(5_000), gcOverlapped: true, ticks: slowTicks);

        var diagnostics = new EvaluationCacheDiagnostics();
        measurements.Publish(diagnostics.StartRequest("slow.proj", 1, 1, 1));
        string log = Flush(diagnostics);

        Buckets(log, "StatLatencyByInFlight").ShouldBe(
            new Dictionary<string, long> { ["FlGt32.Le5ms"] = 1, ["Fl1.Le10us"] = 1, ["Fl4.Le5ms"] = 1 },
            ignoreOrder: true);
        Buckets(log, "StatBurstAll").ShouldBe(
            new Dictionary<string, long> { ["T0100ms"] = 1, ["T0000ms"] = 1, ["Gt2000ms"] = 1 },
            ignoreOrder: true);
        Buckets(log, "StatBurstSlow").ShouldBe(
            new Dictionary<string, long> { ["T0100ms"] = 1, ["Gt2000ms"] = 1 },
            ignoreOrder: true);
        Buckets(log, "StatGc").ShouldBe(
            new Dictionary<string, long> { ["Slow.NoGc"] = 1, ["Slow.GcOverlap"] = 1, ["All.NoGc"] = 2, ["All.GcOverlap"] = 1 },
            ignoreOrder: true);

        log.ShouldContain("|Kind=SlowPathTotals|DistinctPaths=2|DistinctDirectories=1|DroppedStats=0|Count=2|Microseconds=10000");
        log.ShouldContain("|Kind=SlowPath|Rank=1|Count=1|Microseconds=5000|Path=" + EvaluationCacheDiagnostics.Escape(slow));
        log.ShouldContain("|Kind=SlowDirectory|Rank=1|Count=2|DistinctPaths=2|Microseconds=10000|Path=" + EvaluationCacheDiagnostics.Escape(directory));
        Flush(diagnostics).ShouldNotContain("|Kind=SlowPath");
    }

    [Fact]
    public void RealStatsReportTheStatsInFlightAndTheTimeSinceTheBuildsFirstStat()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        TransientTestFolder repo = env.CreateFolder();
        string file = env.CreateFile(repo, "one.props", "x").Path;

        var cache = new Microsoft.Build.Evaluation.Context.ImmutableFileStatCache([]);
        var measurements = new Microsoft.Build.Evaluation.Context.ValidationMeasurements();
        cache.TryStat(file, out _, measurements).ShouldBeTrue();
        cache.TryStat(file, out _, measurements).ShouldBeTrue();

        var diagnostics = new EvaluationCacheDiagnostics();
        measurements.Publish(diagnostics.StartRequest("inflight.proj", 1, 1, 1));
        string log = Flush(diagnostics);

        // Nothing else runs, so each stat is the only one in flight, and both start within the first slice.
        Buckets(log, "StatLatencyByInFlight").Keys.ShouldAllBe(label => label.StartsWith("Fl1.", StringComparison.Ordinal));
        Buckets(log, "StatBurstAll").Keys.ShouldBe(["T0000ms"]);
    }

    [Fact]
    public void FileStatLoopExcludesTheTimeTheDiagnosticsSpentClassifyingStats()
    {
        static (string FileStatLoop, string StatClassification) Publish(long loopTicks, long classificationTicks)
        {
            var measurements = new Microsoft.Build.Evaluation.Context.ValidationMeasurements();
            measurements.CountStat(Path.Combine(Path.GetTempPath(), "x.props"), Microsoft.Build.Evaluation.Context.PathKind.File, underSharedRoot: false, repeat: false, ticks: 1);
            measurements.StatLoopTicks = loopTicks;
            measurements.StatClassificationTicks = classificationTicks;
            var diagnostics = new EvaluationCacheDiagnostics();
            measurements.Publish(diagnostics.StartRequest("loop.proj", 1, 1, 1));
            string log = Flush(diagnostics);
            return (PhaseFields(log, "FileStatLoop")["TotalTicks"], PhaseFields(log, "StatClassification")["TotalTicks"]);
        }

        Publish(100, 30).ShouldBe(("70", "30"));
        Publish(10, 30).ShouldBe(("0", "30"));
    }

    [Fact]
    public void ProcessSummaryReportsGcAndAllocationOnlyWhereTheRuntimeProvidesThem()
    {
        var diagnostics = new EvaluationCacheDiagnostics();
        diagnostics.BeginBuild("manager", 1, EvaluationCacheMode.SnapshotFileSystem, []);
        string log = Flush(diagnostics);

        Dictionary<string, string> process = log.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries)
            .Single(message => message.Contains("|Kind=Process|"))
            .Split('|').Skip(1).Select(field => field.Split(['='], 2)).ToDictionary(pair => pair[0], pair => pair[1]);
        double.Parse(process["WallMilliseconds"], CultureInfo.InvariantCulture).ShouldBeGreaterThanOrEqualTo(0);
        long.Parse(process["WorkingSetBytes"], CultureInfo.InvariantCulture).ShouldBeGreaterThan(0);
#if NET
        double.Parse(process["GcPauseMilliseconds"], CultureInfo.InvariantCulture).ShouldBeGreaterThanOrEqualTo(0);
        long.Parse(process["AllocatedBytes"], CultureInfo.InvariantCulture).ShouldBeGreaterThanOrEqualTo(0);
#else
        process["GcPauseMilliseconds"].ShouldBe("-1.000");
        process["AllocatedBytes"].ShouldBe("-1");
#endif
    }

    [Fact]
    public void LatencyBucketsPlaceADurationAtTheFirstBoundItDoesNotExceed()
    {
        static long Ticks(long microseconds) => microseconds * Stopwatch.Frequency / 1_000_000;
        LatencyBuckets.Index(0).ShouldBe(0);
        LatencyBuckets.Index(Ticks(10)).ShouldBe(0);
        LatencyBuckets.Index(Ticks(10) + 1).ShouldBe(1);
        LatencyBuckets.Index(Ticks(250_000)).ShouldBe(LatencyBuckets.Labels.Length - 2);
        LatencyBuckets.Index(Ticks(250_000) + 1).ShouldBe(LatencyBuckets.Labels.Length - 1);

        // A whole second is exact at any timer frequency.
        var buckets = new TimedBuckets(LatencyBuckets.Labels);
        buckets.Add(LatencyBuckets.Index(Stopwatch.Frequency), Stopwatch.Frequency);
        buckets.Add(LatencyBuckets.Index(Stopwatch.Frequency), Stopwatch.Frequency);
        buckets.Format().ShouldBe("Gt250ms:2:2000000");
    }

    [Fact]
    public void ReplayedDirectoryStateIsRememberedAndNotReplayedAgain()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        Configure(env, true, EvaluationCacheMode.SnapshotFileSystem);
        TransientTestFolder folder = env.CreateFolder();
        env.CreateFile(folder, "a.txt", "a");
        string project = env.CreateFile(folder, "glob.proj", "<Project><ItemGroup><Item Include=\"*.txt\" /></ItemGroup></Project>").Path;
        var cache = new ProjectInstanceSnapshotCache();
        var parameters = new BuildParameters
        {
            EvaluationCacheConfiguration = Traits.Instance.EvaluationCache,
            ProjectInstanceSnapshotCache = cache,
        };
        cache.ConfigureValidator(EvaluationCacheValidationPolicy.FileSystem);
        cache.ConfigureDiagnostics(true, "manager", 1, EvaluationCacheMode.SnapshotFileSystem, parameters.EnvironmentPropertiesInternal);
        var host = new MockHost(parameters) { LoggingService = new MockLoggingService(_output.WriteLine) };
        Load(project, parameters, host);
        Flush(cache.Diagnostics!);

        File.WriteAllText(Path.Combine(folder.Path, "unrelated.log"), "x");
        Directory.SetLastWriteTimeUtc(folder.Path, DateTime.UtcNow.AddSeconds(-30));
        Load(project, parameters, host);
        string first = Flush(cache.Diagnostics!);
        Load(project, parameters, host);
        string second = Flush(cache.Diagnostics!);

        first.ShouldContain("ValidationDetail.GlobsReplayed:1");
        first.ShouldContain("ValidationDetail.DirectoriesRemembered:1");
        second.ShouldContain("ValidationDetail.DirectoriesComparedToRememberedState:1");
        second.ShouldContain("ValidationDetail.DirectoriesQuietSinceRememberedState:1");
        second.ShouldNotContain("ValidationDetail.GlobsReplayed");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CacheOwnershipBoundaryIsVisible(bool legacyActivation)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        Configure(env, true, EvaluationCacheMode.SnapshotFileSystem);
        if (legacyActivation)
        {
            env.SetEnvironmentVariable(EvaluationCacheConfiguration.ModeEnvironmentVariable, null);
            env.SetEnvironmentVariable("MSBUILDRECORDEVALUATIONINPUTS", null);
            env.SetEnvironmentVariable("MSBUILDENABLEPROJECTINSTANCESNAPSHOTCACHE", "1");
            Traits.UpdateFromEnvironment();
        }
        string project = env.CreateFile("worker.proj", "<Project />").Path;
        var parameters = new BuildParameters
        {
            EvaluationCacheConfiguration = Traits.Instance.EvaluationCache,
            EvaluationCacheDiagnostics = new EvaluationCacheDiagnostics(),
        };
        List<string> messages = [];
        var host = new MockHost(parameters) { LoggingService = new MockLoggingService(messages.Add) };
        Load(project, parameters, host);
        messages.ShouldBeEmpty();
        string log = Flush(parameters.EvaluationCacheDiagnostics);
        log.ShouldContain("|Reason=CacheUnavailableOnHost|");
        log.ShouldNotContain("|Reason=CacheDisabled|");
        Examples(log).Single().ShouldContain("|Reason=CacheUnavailableOnHost|");
        if (parameters.EvaluationCacheConfiguration is not { RecordInputs: true })
        {
            AssertPhases(log, ("FreshEvaluation", 1));
        }
        else
        {
            AssertPhases(log, ("RequestKey", 1), ("FreshEvaluation", 1));
        }
    }

    [Fact]
    public void WorkerOwnsBoundedBufferAndFlushesItAtCleanup()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        Configure(env, true, EvaluationCacheMode.SnapshotFileSystem);
        var parameters = new BuildParameters();
        List<string> messages = [];
        var host = new MockHost(parameters) { LoggingService = new MockLoggingService(messages.Add) };
        var engine = (IBuildRequestEngine)host.GetComponent(BuildComponentType.RequestEngine);
        engine.InitializeForBuild(new NodeLoggingContext(host.LoggingService, 2, false));
        EvaluationCacheDiagnostics diagnostics = parameters.EvaluationCacheDiagnostics;
        diagnostics.ShouldNotBeNull();
        parameters.ProjectInstanceSnapshotCache.ShouldBeNull();
        for (int i = 0; i <= EvaluationCacheDiagnostics.MaximumEventsPerBuild; i++)
        {
            diagnostics.StartRequest("worker.proj", i, 1, 2).Record("Bypass", "CacheUnavailableOnHost");
        }
        messages.ShouldNotContain(message => message.StartsWith("EvaluationCacheDiagnostic", StringComparison.Ordinal));
        engine.CleanupForBuild();
        parameters.EvaluationCacheDiagnostics.ShouldBeNull();
        string summary = messages.Single(message => message.StartsWith("EvaluationCacheDiagnosticSummary|", StringComparison.Ordinal));
        summary.ShouldContain("|DroppedEvents=2|");
        summary.ShouldContain("Bypass.CacheUnavailableOnHost:10001");
        messages.Count(message => message.StartsWith("EvaluationCacheTimingExample|", StringComparison.Ordinal))
            .ShouldBe(EvaluationCacheDiagnostics.MaximumExamplesPerReason);
        messages.Single(message => message.Contains("|Kind=Counts|")).ShouldContain("Bypass.CacheUnavailableOnHost:10001");
        engine.InitializeForBuild(new NodeLoggingContext(host.LoggingService, 2, false));
        parameters.EvaluationCacheDiagnostics.ShouldNotBeSameAs(diagnostics);
        engine.CleanupForBuild();
        ((IBuildComponent)engine).ShutdownComponent();
    }

    [Fact]
    public void DiagnosticStateIsSharedOnlyByInProcessClones()
    {
        var parameters = new BuildParameters { EvaluationCacheDiagnostics = new EvaluationCacheDiagnostics() };
        parameters.Clone().EvaluationCacheDiagnostics.ShouldBeSameAs(parameters.EvaluationCacheDiagnostics);
        ((ITranslatable)parameters).Translate(TranslationHelpers.GetWriteTranslator());
        BuildParameters.FactoryForDeserialization(TranslationHelpers.GetReadTranslator())
            .EvaluationCacheDiagnostics.ShouldBeNull();
    }

    [Fact]
    public void KeyComparisonReportsNamesNotValues()
    {
        var cache = new ProjectInstanceSnapshotCache();
        cache.ConfigureDiagnostics(true, "manager", 1, EvaluationCacheMode.SnapshotFileSystem,
            [ProjectPropertyInstance.Create("CHANGED_ENV", "private-env-before")]);
        ProjectInstanceSnapshotCacheKey before = Key(1, "private-global-before");
        ProjectInstanceSnapshotCacheKey after = Key(2, "private-global-after");
        cache.AddOrReplace(before, Entry());
        cache.ConfigureDiagnostics(true, "manager", 2, EvaluationCacheMode.SnapshotFileSystem,
            [ProjectPropertyInstance.Create("CHANGED_ENV", "private-env-after")]);
        EvaluationCacheDiagnostics.Request request = cache.Diagnostics!.StartRequest(after.ProjectFullPath, 1, 2, 1);
        request.BindKey(after);
        cache.TryGet(after, out _, request).ShouldBeFalse();
        string log = Flush(cache.Diagnostics);
        log.ShouldContain("|Reason=KeyMismatch|");
        log.ShouldContain("Fields%3DEnvironmentFingerprint,GlobalProperties");
        log.ShouldContain("GlobalPropertyNames%3DChangedGlobal");
        log.ShouldContain("EnvironmentVariableNames%3DCHANGED_ENV");
        log.ShouldNotContain("private-");
        log.ShouldContain("CandidateKey");
        Examples(log).Single().ShouldContain("|Reason=KeyMismatch|");
    }

    [Fact]
    public void KeyIdentifiersRespectEqualityAndSeparateAmbiguousValues()
    {
        var diagnostics = new EvaluationCacheDiagnostics();
        ProjectInstanceSnapshotCacheKey first = Key(1, "value");
        var equivalent = new ProjectInstanceSnapshotCacheKey(first.ProjectFullPath, "current", false, null,
            ProjectLoadSettings.Default, new Dictionary<string, string> { ["changedglobal"] = "value" }, environmentFingerprint: 1);
        diagnostics.GetKeyId(first).ShouldBe(diagnostics.GetKeyId(equivalent));
        diagnostics.GetKeyId(first).ShouldNotBe(diagnostics.GetKeyId(Key(2, "value")));
        new EvaluationCacheDiagnostics().GetKeyId(first).ShouldNotBe(diagnostics.GetKeyId(first));
        var embedded = new ProjectInstanceSnapshotCacheKey(first.ProjectFullPath, "Current", false, null,
            ProjectLoadSettings.Default, new Dictionary<string, string> { ["A"] = "x\0B=y" });
        var separate = new ProjectInstanceSnapshotCacheKey(first.ProjectFullPath, "Current", false, null,
            ProjectLoadSettings.Default, new Dictionary<string, string> { ["A"] = "x", ["B"] = "y" });
        diagnostics.GetKeyId(embedded).ShouldNotBe(diagnostics.GetKeyId(separate));
    }

    [Theory]
    [InlineData("Evicted")]
    [InlineData("CacheCleared")]
    [InlineData("Oversized")]
    [InlineData("ComponentShutdown")]
    public void MissingEntriesHaveLifecycleReasons(string reason)
    {
        ProjectInstanceSnapshotCacheKey key = Key(1);
        ProjectInstanceSnapshotCacheEntry entry = Entry();
        long size = key.RetainedSizeBytes + entry.RetainedSizeBytes;
        var cache = new ProjectInstanceSnapshotCache(reason == "Oversized" ? 0 : size);
        cache.ConfigureDiagnostics(true, "manager", 1, EvaluationCacheMode.SnapshotFileSystem, []);
        cache.AddOrReplace(key, entry);
        switch (reason)
        {
            case "Evicted":
                cache.AddOrReplace(Key(2), entry).ShouldBeTrue();
                break;
            case "CacheCleared":
                cache.Clear();
                break;
            case "ComponentShutdown":
                cache.ShutdownComponent();
                break;
        }
        EvaluationCacheDiagnostics.Request request = cache.Diagnostics!.StartRequest(key.ProjectFullPath, 1, 1, 1);
        request.BindKey(key);
        cache.TryGet(key, out _, request).ShouldBeFalse();
        Flush(cache.Diagnostics).ShouldContain($"|Event=Lookup|Reason={reason}|");
    }

    [Fact]
    public void DisablingDiagnosticsDropsDiagnosticStateWithoutChangingEntries()
    {
        var cache = new ProjectInstanceSnapshotCache();
        cache.ConfigureDiagnostics(true, "manager", 1, EvaluationCacheMode.SnapshotFileSystem, []);
        cache.AddOrReplace(Key(1), Entry());
        cache.ConfigureDiagnostics(false, "manager", 2, EvaluationCacheMode.SnapshotFileSystem, []);
        cache.Diagnostics.ShouldBeNull();
        cache.TryGet(Key(1), out _).ShouldBeTrue();
    }

    [Fact]
    public void BoundsAreExplicitAndSummaryCountsSurviveTruncation()
    {
        var diagnostics = new EvaluationCacheDiagnostics();
        diagnostics.BeginBuild("manager", 1, EvaluationCacheMode.SnapshotFileSystem, []);
        for (int i = 0; i < EvaluationCacheDiagnostics.MaximumEventsPerBuild + 5; i++)
        {
            diagnostics.Record(null, "Lookup", "NoEntryOrHistory");
        }
        for (int i = 0; i <= EvaluationCacheDiagnostics.MaximumHistoryEntries; i++)
        {
            diagnostics.Remember(i.ToString(), "Evicted");
        }
        diagnostics.GetPreviousDisposition("0").ShouldBeNull();
        string log = Flush(diagnostics);
        log.ShouldContain("|DroppedEvents=6|ForgottenHistory=1|");
        log.ShouldContain("Lookup.NoEntryOrHistory:10005");
        // The events, the detailed and timing count summaries, and the process summary.
        log.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries).Length
            .ShouldBe(EvaluationCacheDiagnostics.MaximumEventsPerBuild + 3);
        Flush(diagnostics).ShouldContain("|DroppedEvents=0|ForgottenHistory=0|Counts=");
    }

    [Fact]
    public void DelimitersCannotInjectDiagnosticFields()
    {
        EvaluationCacheDiagnostics.Escape("x%|=\r\n").ShouldBe("x%25%7C%3D%0D%0A");
        var diagnostics = new EvaluationCacheDiagnostics();
        diagnostics.StartRequest("path|Reason=spoof\nnext", 1, 1, 1).Record("Evaluation", "Cacheable");
        string log = Flush(diagnostics);
        log.ShouldContain("path%7CReason%3Dspoof%0Anext");
        log.ShouldNotContain("|Reason=spoof");
    }

    [Fact]
    public async Task ConcurrentRecordsAreCountedAndLoggingRunsOutsideCacheAndDiagnosticLocks()
    {
        var cache = new ProjectInstanceSnapshotCache();
        cache.ConfigureDiagnostics(true, "manager", 1, EvaluationCacheMode.SnapshotFileSystem, []);
        EvaluationCacheDiagnostics diagnostics = cache.Diagnostics!;
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < 50; i++)
            {
                diagnostics.Record(null, "Reuse", "Materialized");
                diagnostics.RecordTiming(EvaluationCacheDiagnostics.Phase.Materialization, "concurrent.proj", 2);
            }
        })));
        bool checkedCallback = false;
        List<string> messages = [];
        diagnostics.Flush(new MockLoggingService(message =>
        {
            messages.Add(message);
            if (!checkedCallback)
            {
                checkedCallback = true;
                Task.Run(() =>
                {
                    cache.GetStatistics().Count.ShouldBe(0);
                    diagnostics.Record(null, "Lookup", "NoEntryOrHistory");
                    diagnostics.RecordTiming(EvaluationCacheDiagnostics.Phase.CacheLookup, "callback.proj", 3);
                })
                    .Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            }
        }));
        string log = string.Join(Environment.NewLine, messages);
        log.ShouldContain("Reuse.Materialized:200");
        AssertPhases(log, ("Materialization", 200));
        log.ShouldContain("|TotalTicks=400|MaxTicks=2|");
        string next = Flush(diagnostics);
        next.ShouldContain("Lookup.NoEntryOrHistory:1");
        AssertPhases(next, ("CacheLookup", 1));
    }

    [Fact]
    public void TimingTotalsMaximaAndActiveScopesResetAtFlush()
    {
        var diagnostics = new EvaluationCacheDiagnostics();
        EvaluationCacheDiagnostics.Request request = diagnostics.StartRequest("scope.proj", 1, 2, 3);
        diagnostics.RecordTiming(EvaluationCacheDiagnostics.Phase.RequestKey, "fast.proj", Stopwatch.Frequency);
        diagnostics.RecordTiming(EvaluationCacheDiagnostics.Phase.RequestKey, "slow|project.proj", 2 * Stopwatch.Frequency);
        diagnostics.RecordTiming(EvaluationCacheDiagnostics.Phase.RequestKey, "fast.proj", 0);
        using (request.Time(EvaluationCacheDiagnostics.Phase.FreshEvaluation))
        {
            string log = Flush(diagnostics);
            AssertPhases(log, ("RequestKey", 3));
            log.ShouldContain("|TotalMilliseconds=3000.000|MaxMilliseconds=2000.000|SlowestProject=slow%7Cproject.proj");
            log.ShouldContain(FormattableString.Invariant($"|TotalTicks={3 * Stopwatch.Frequency}|MaxTicks={2 * Stopwatch.Frequency}|"));
        }
        AssertPhases(Flush(diagnostics), ("FreshEvaluation", 1));
        AssertPhases(Flush(diagnostics));

        var expected = new OperationCanceledException("private-exception-text");
        Should.Throw<OperationCanceledException>(() =>
        {
            using var timing = request.Time(EvaluationCacheDiagnostics.Phase.Validation);
            throw expected;
        }).ShouldBeSameAs(expected);
        AssertPhases(Flush(diagnostics), ("Validation", 1));
    }

    [Fact]
    public void HighImportanceExamplesAreBoundedIndependentlyOfDetailedEvents()
    {
        var diagnostics = new EvaluationCacheDiagnostics();
        diagnostics.BeginBuild("manager", 1, EvaluationCacheMode.SnapshotFileSystem, []);
        for (int i = 0; i < EvaluationCacheDiagnostics.MaximumEventsPerBuild; i++)
        {
            diagnostics.Record(null, "Lookup", "NoEntryOrHistory");
        }
        string oversizedField = new('|', EvaluationCacheDiagnostics.MaximumExampleFieldLength + 20);
        EvaluationCacheDiagnostics.Request request = diagnostics.StartRequest(oversizedField, 1, 2, 3);
        for (int i = 0; i < 5; i++)
        {
            request.Record("Validation", "FileSystemInputChanged", oversizedField);
        }
        for (int i = 0; i < EvaluationCacheDiagnostics.MaximumExamplesPerBuild; i++)
        {
            request.Record("Validation", "TestReason" + i.ToString(CultureInfo.InvariantCulture));
        }
        List<string> messages = [];
        diagnostics.Flush(new MockLoggingService(messages.Add));
        string log = string.Join(Environment.NewLine, messages);
        string[] examples = Examples(log);
        examples.Length.ShouldBe(EvaluationCacheDiagnostics.MaximumExamplesPerBuild);
        examples.Count(message => message.Contains("|Reason=FileSystemInputChanged|")).ShouldBe(3);
        examples.ShouldAllBe(message => message.Length < 4000);
        examples[0].ShouldContain("<truncated>");
        log.ShouldContain("|SuppressedExamples=5|");
        log.ShouldContain("Validation.FileSystemInputChanged:5");
        examples.ShouldAllBe(message => !message.Contains("|Project=|"));
        request.Record("Validation", "FileSystemInputChanged", "new.proj");
        string next = Flush(diagnostics);
        Examples(next).Length.ShouldBe(1);
        next.ShouldContain("|SuppressedExamples=0|");
    }

    [Theory]
    [InlineData(510, 0, 512, false)]
    [InlineData(510, 1, 512, true)]
    [InlineData(511, 1, 511, true)]
    [InlineData(512, 1, 512, true)]
    public void BoundedSurrogatePairsSurviveBinaryLogReplay(int surrogateIndex, int trailingLength, int retainedLength, bool truncated)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        Configure(env, true, EvaluationCacheMode.Disabled);
        string value = "p|=" + new string('x', surrogateIndex - 3) + "\uD83D\uDE00" + new string('z', trailingLength);
        string expected = EvaluationCacheDiagnostics.Escape(value.Substring(0, retainedLength) + (truncated ? "<truncated>" : string.Empty));
        string binlog = env.CreateFile("surrogates.binlog").Path;
        var logger = new MockLogger(_output);
        using var manager = new BuildManager();
        manager.BeginBuild(new BuildParameters { Loggers = [logger, new BinaryLogger { Parameters = binlog }] });
        EvaluationCacheDiagnostics diagnostics = ((IBuildComponentHost)manager).BuildParameters.EvaluationCacheDiagnostics;
        diagnostics.ShouldNotBeNull();
        diagnostics.StartRequest(value, 1, 2, 3).Record("Validation", "FileSystemInputChanged", value);
        diagnostics.RecordTiming(EvaluationCacheDiagnostics.Phase.FreshEvaluation, value, 1);
        manager.EndBuild();

        string example = logger.BuildMessageEvents.Single(message =>
            message.Message?.StartsWith("EvaluationCacheTimingExample|", StringComparison.Ordinal) == true).Message!;
        example.Split('|').Single(field => field.StartsWith("Project=", StringComparison.Ordinal)).ShouldBe("Project=" + expected);
        example.Split('|').Single(field => field.StartsWith("Detail=", StringComparison.Ordinal)).ShouldBe("Detail=" + expected);
        string phase = logger.BuildMessageEvents.Single(message => message.Message?.Contains("|Kind=Phase|") == true).Message!;
        phase.Split('|').Single(field => field.StartsWith("SlowestProject=", StringComparison.Ordinal)).ShouldBe("SlowestProject=" + expected);

        List<string> replayed = [];
        var replay = new BinaryLogReplayEventSource();
        replay.MessageRaised += (_, args) =>
        {
            if (args.Message?.StartsWith("EvaluationCacheTiming", StringComparison.Ordinal) == true)
            {
                args.Importance.ShouldBe(MessageImportance.High);
                replayed.Add(args.Message);
            }
        };
        replay.Replay(binlog);
        replayed.ShouldBe(logger.BuildMessageEvents.Where(message =>
            message.Message?.StartsWith("EvaluationCacheTiming", StringComparison.Ordinal) == true).Select(message => message.Message));
        replayed.ShouldContain(example);
        replayed.ShouldContain(phase);
    }

    [Theory]
    [InlineData("Recoverable")]
    [InlineData("Cancellation")]
    [InlineData("BuildAbort")]
    [InlineData("NestedCancellation")]
    [InlineData("Critical")]
    public void ValidationFallbackAndExceptionsStopOnlyEnteredPhases(string failure)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        Configure(env, true, EvaluationCacheMode.SnapshotFileSystem);
        string project = env.CreateFile("fallback.proj", "<Project />").Path;
        var cache = new ProjectInstanceSnapshotCache();
        var parameters = new BuildParameters
        {
            EvaluationCacheConfiguration = Traits.Instance.EvaluationCache,
            ProjectInstanceSnapshotCache = cache,
        };
        cache.ConfigureValidator(EvaluationCacheValidationPolicy.FileSystem);
        cache.ConfigureDiagnostics(true, "manager", 1, EvaluationCacheMode.SnapshotFileSystem, parameters.EnvironmentPropertiesInternal);
        var host = new MockHost(parameters) { LoggingService = new MockLoggingService(_output.WriteLine) };
        Load(project, parameters, host);
        Flush(cache.Diagnostics!);
        Exception expected = failure switch
        {
            "Cancellation" => new OperationCanceledException("private-exception-text"),
            "BuildAbort" => new BuildAbortedException("private-exception-text"),
            "NestedCancellation" => new InvalidOperationException("private-exception-text", new OperationCanceledException()),
            "Critical" => new OutOfMemoryException("private-exception-text"),
            _ => new InvalidOperationException("private-exception-text"),
        };
        var validator = new ThrowingValidator(expected);
        cache.Validator = validator;
        if (failure == "Recoverable")
        {
            Load(project, parameters, host);
        }
        else
        {
            Should.Throw<Exception>(() => Load(project, parameters, host)).ShouldBeSameAs(expected);
        }
        validator.Calls.ShouldBe(1);
        string log = Flush(cache.Diagnostics!);
        log.ShouldNotContain("private-exception-text");
        if (failure == "Recoverable")
        {
            AssertPhases(log, ("RequestKey", 1), ("CacheLookup", 1), ("Validation", 1), ("RootElementCheck", 1),
                ("FreshEvaluation", 1), ("SnapshotCreation", 1), ("CacheAdmission", 2), ("FallbackPreparation", 1));
            Examples(log).Single().ShouldContain("|Event=Fallback|Reason=ValidationError|Detail=System.InvalidOperationException");
        }
        else
        {
            AssertPhases(log, ("RequestKey", 1), ("CacheLookup", 1), ("Validation", 1), ("RootElementCheck", 1));
            Examples(log).ShouldBeEmpty();
            cache.GetStatistics().FreshEvaluations.ShouldBe(1);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FallbackPreparationExceptionsStopTimerAndPreserveBehavior(bool cancellation)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        Configure(env, true, EvaluationCacheMode.SnapshotFileSystem);
        string project = env.CreateFile("cleanup.proj", "<Project />").Path;
        var cache = new ProjectInstanceSnapshotCache();
        var parameters = new BuildParameters
        {
            EvaluationCacheConfiguration = Traits.Instance.EvaluationCache,
            ProjectInstanceSnapshotCache = cache,
        };
        cache.ConfigureDiagnostics(true, "manager", 1, EvaluationCacheMode.SnapshotFileSystem, parameters.EnvironmentPropertiesInternal);
        var host = new MockHost(parameters) { LoggingService = new MockLoggingService(_output.WriteLine) };
        var rootCache = new ThrowingProjectRootElementCache();
        parameters.ProjectRootElementCache = rootCache;
        Load(project, parameters, host);
        Flush(cache.Diagnostics!);
        rootCache.Failure = cancellation
            ? new OperationCanceledException("private-cleanup-text")
            : new InvalidOperationException("private-cleanup-text");
        if (cancellation)
        {
            Should.Throw<OperationCanceledException>(() => Load(project, parameters, host)).ShouldBeSameAs(rootCache.Failure);
        }
        else
        {
            Load(project, parameters, host);
        }

        rootCache.DiscardCalls.ShouldBe(1);
        cache.Count.ShouldBe(0);
        string log = Flush(cache.Diagnostics!);
        log.ShouldNotContain("private-cleanup-text");
        if (cancellation)
        {
            AssertPhases(log, ("RequestKey", 1), ("CacheLookup", 1), ("Validation", 1), ("RootElementCheck", 1),
                ("CacheAdmission", 1), ("FallbackPreparation", 1));
            cache.GetStatistics().FreshEvaluations.ShouldBe(1);
        }
        else
        {
            AssertPhases(log, ("RequestKey", 1), ("CacheLookup", 1), ("Validation", 1), ("RootElementCheck", 1),
                ("CacheAdmission", 1), ("FallbackPreparation", 1), ("FreshEvaluation", 1));
            cache.GetStatistics().FreshEvaluations.ShouldBe(2);
            Examples(log).ShouldContain(message => message.Contains("|Event=Fallback|Reason=KeyOrLookupError|Detail=System.InvalidOperationException"));
        }
    }

    [Fact]
    public void KeyFailureAndFailingFreshEvaluationAreTimedWithoutAdmission()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        Configure(env, true, EvaluationCacheMode.SnapshotFileSystem);
        string project = env.CreateFile("invalid.proj", "<Project>").Path;
        var cache = new ProjectInstanceSnapshotCache();
        var parameters = new BuildParameters
        {
            EvaluationCacheConfiguration = Traits.Instance.EvaluationCache,
            ProjectInstanceSnapshotCache = cache,
        };
        cache.ConfigureDiagnostics(true, "manager", 1, EvaluationCacheMode.SnapshotFileSystem, parameters.EnvironmentPropertiesInternal);
        var host = new MockHost(parameters) { LoggingService = new MockLoggingService(_output.WriteLine) };
        Should.Throw<InvalidProjectFileException>(() => Load(project, parameters, host));
        string log = Flush(cache.Diagnostics!);
        AssertPhases(log, ("RequestKey", 1), ("FreshEvaluation", 1));
        Examples(log).Single().ShouldContain("|Reason=KeyOrLookupError|");
        cache.StoredEntries.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AdmissionRejectionsAreTimedAndVisibleAtMinimalVerbosity(bool nonCacheable)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        Configure(env, true, EvaluationCacheMode.SnapshotFileSystem);
        if (!nonCacheable)
        {
            env.SetEnvironmentVariable(ProjectInstanceSnapshotCache.MaximumSizeEnvironmentVariable, "0");
        }
        string project = env.CreateFile("rejected.proj", nonCacheable
            ? """<Project><PropertyGroup><Value>$([System.Guid]::NewGuid())</Value></PropertyGroup><Target Name="Build" /></Project>"""
            : """<Project><Target Name="Build" /></Project>""").Path;
        var logger = new MockLogger(_output);
        var console = new StringBuilder();
        using var manager = new BuildManager();
        manager.Build(new BuildParameters
        {
            Loggers = [logger, new ConsoleLogger(LoggerVerbosity.Minimal, text => console.Append(text), null, null)],
        }, new BuildRequestData(project, new Dictionary<string, string?>(), null, ["Build"], null)).ShouldHaveSucceeded();
        string log = console.ToString();
        log.ShouldContain("EvaluationCacheTimingSummary|Version=1|");
        log.ShouldContain("EvaluationCacheExperimentStatus|");
        log.ShouldNotContain("EvaluationCacheDiagnostic|");
        string reason = nonCacheable ? "NonCacheable" : "Oversized";
        Examples(log).Single().ShouldContain($"|Event=Admission|Reason={reason}|");
        log.ShouldContain($"Admission.{reason}:1");
        if (nonCacheable)
        {
            AssertPhases(log, ("RequestKey", 1), ("CacheLookup", 1), ("FreshEvaluation", 1), ("SnapshotCreation", 1));
        }
        else
        {
            log.ShouldContain("|MaximumSizeBytes=0|");
            AssertPhases(log, ("RequestKey", 1), ("CacheLookup", 1), ("FreshEvaluation", 1), ("SnapshotCreation", 1), ("CacheAdmission", 1));
        }
    }

    [Fact]
    public void OrdinaryDiagnosticMessagesSurviveBinaryLogReplay()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        Configure(env, true, EvaluationCacheMode.SnapshotFileSystem);
        env.SetEnvironmentVariable(ProjectInstanceSnapshotCache.MaximumSizeEnvironmentVariable, "0");
        string project = env.CreateFile("binary.proj", """<Project><Target Name="Build" /></Project>""").Path;
        string binlog = env.CreateFile("diagnostics.binlog").Path;
        var logger = new MockLogger(_output);
        using var manager = new BuildManager();
        manager.Build(
            new BuildParameters { Loggers = [logger, new BinaryLogger { Parameters = binlog }] },
            new BuildRequestData(project, new Dictionary<string, string?>(), null, ["Build"], null))
            .ShouldHaveSucceeded();
        List<string> messages = [];
        List<string> timings = [];
        var replay = new BinaryLogReplayEventSource();
        replay.MessageRaised += (_, args) =>
        {
            if (args.Message?.StartsWith("EvaluationCacheDiagnostic", StringComparison.Ordinal) == true)
            {
                args.Importance.ShouldBe(MessageImportance.Low);
                messages.Add(args.Message);
            }
            else if (args.Message?.StartsWith("EvaluationCacheTiming", StringComparison.Ordinal) == true)
            {
                args.Importance.ShouldBe(MessageImportance.High);
                timings.Add(args.Message);
            }
        };
        replay.Replay(binlog);
        messages.ShouldNotBeEmpty();
        messages.ShouldAllBe(message => logger.FullLog.Contains(message));
        messages.ShouldContain(message => message.StartsWith("EvaluationCacheDiagnosticSummary|", StringComparison.Ordinal));
        timings.ShouldAllBe(message => logger.FullLog.Contains(message));
        timings.ShouldContain(message => message.StartsWith("EvaluationCacheTimingExample|", StringComparison.Ordinal));
        AssertPhases(string.Join(Environment.NewLine, timings), ("RequestKey", 1), ("CacheLookup", 1), ("FreshEvaluation", 1),
            ("SnapshotCreation", 1), ("CacheAdmission", 1));
    }

    internal static void AssertPhases(string log, params (string Phase, long Count)[] expected)
    {
        Dictionary<string, Dictionary<string, string>> phases = log.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries)
            .Where(message => message.Contains("EvaluationCacheTimingSummary|") && message.Contains("|Kind=Phase|"))
            .Select(message => message.Split('|').Skip(1).ToDictionary(
                field => field.Substring(0, field.IndexOf('=')),
                field => field.Substring(field.IndexOf('=') + 1)))
            .ToDictionary(fields => fields["Phase"]);
        phases.Keys.OrderBy(name => name).ShouldBe(expected.Select(item => item.Phase).OrderBy(name => name));
        foreach ((string phase, long count) in expected)
        {
            Dictionary<string, string> fields = phases[phase];
            long.Parse(fields["Count"], CultureInfo.InvariantCulture).ShouldBe(count);
            long total = long.Parse(fields["TotalTicks"], CultureInfo.InvariantCulture);
            long maximum = long.Parse(fields["MaxTicks"], CultureInfo.InvariantCulture);
            total.ShouldBeGreaterThanOrEqualTo(0);
            maximum.ShouldBeInRange(0, total);
            fields["SlowestProject"].ShouldNotBeNullOrEmpty();
            if (phase is "ManifestValidation" or "SdkValidation" or "RootElementCheck" or "KeyCheck" or "DiagnosticsPublish")
            {
                fields["NestedUnder"].ShouldBe("Validation");
                total.ShouldBeLessThanOrEqualTo(long.Parse(phases["Validation"]["TotalTicks"], CultureInfo.InvariantCulture));
            }
            else if (phase is "FileStatLoop" or "GlobReplay" or "EnvironmentCheck" or "GlobBookkeeping" or "StatClassification")
            {
                fields["NestedUnder"].ShouldBe("ManifestValidation");
                total.ShouldBeLessThanOrEqualTo(long.Parse(phases["ManifestValidation"]["TotalTicks"], CultureInfo.InvariantCulture));
            }
            else
            {
                fields["NestedUnder"].ShouldBeEmpty();
            }
        }
    }

    private static string[] Examples(string log) =>
        log.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries)
            .Where(message => message.Contains("EvaluationCacheTimingExample|")).ToArray();

    private static Dictionary<string, string> PhaseFields(string log, string phase) =>
        log.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries)
            .Single(message => message.Contains("|Kind=Phase|Phase=" + phase + "|"))
            .Split('|').Skip(1).Select(field => field.Split(['='], 2)).ToDictionary(pair => pair[0], pair => pair[1]);

    // label -> count for one bucket family of a flush
    private static Dictionary<string, long> Buckets(string log, string family)
    {
        string line = log.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries)
            .Single(message => message.Contains("|Kind=Buckets|Family=" + family + "|"));
        return line.Substring(line.IndexOf("|Buckets=", StringComparison.Ordinal) + "|Buckets=".Length)
            .Split(',').Select(bucket => bucket.Split(':'))
            .ToDictionary(parts => parts[0], parts => long.Parse(parts[1], CultureInfo.InvariantCulture));
    }

    private static void Configure(TestEnvironment env, bool enabled, EvaluationCacheMode mode)
    {
        env.WithTransientTestState(new ResetTraits());
        env.SetEnvironmentVariable(EvaluationCacheDiagnostics.EnvironmentVariable, enabled ? "1" : null);
        env.SetEnvironmentVariable(EvaluationCacheConfiguration.ModeEnvironmentVariable, mode.ToString());
        Traits.UpdateFromEnvironment();
    }

    private static void Load(string project, BuildParameters parameters, MockHost host, Dictionary<string, string?>? globals = null)
    {
        var data = new BuildRequestData(project, globals ?? new Dictionary<string, string?>(), null, [], null);
        new BuildRequestConfiguration(data, parameters.DefaultToolsVersion)
            .LoadProjectIntoConfiguration(host, BuildRequestDataFlags.None, 1, 1);
    }

    private static ProjectInstanceSnapshotCacheKey Key(long environment, string value = "value") =>
        new(Path.Combine(Path.GetTempPath(), "diagnostic-key.proj"), "Current", false, null,
            ProjectLoadSettings.Default, new Dictionary<string, string> { ["ChangedGlobal"] = value },
            environmentFingerprint: environment);

    private static ProjectInstanceSnapshotCacheEntry Entry()
    {
        using var collection = new ProjectCollection();
        using var project = new ProjectRootElementFromString("<Project />", collection);
        return new(ProjectInstanceSnapshot.Create(new ProjectInstance(project.Project)),
            EmptyProjectInstanceSnapshotValidationData.Instance);
    }

    private static string Flush(EvaluationCacheDiagnostics diagnostics)
    {
        List<string> messages = [];
        diagnostics.Flush(new MockLoggingService(messages.Add));
        return string.Join(Environment.NewLine, messages);
    }

    private sealed class ResetTraits : TransientTestState
    {
        public override void Revert() => Traits.UpdateFromEnvironment();
    }

    private sealed class ThrowingValidator(Exception exception) : IProjectInstanceSnapshotValidator
    {
        internal int Calls { get; private set; }

        public ProjectInstanceSnapshotValidationResult Validate(ProjectInstanceSnapshotCacheKey key, ProjectInstanceSnapshotCacheEntry entry)
        {
            Calls++;
            throw exception;
        }
    }

    private sealed class ThrowingProjectRootElementCache() : ProjectRootElementCache(autoReloadFromDisk: false)
    {
        internal Exception? Failure { get; set; }
        internal int DiscardCalls { get; private set; }

        internal override void DiscardImplicitReferences()
        {
            DiscardCalls++;
            if (Failure is not null)
            {
                throw Failure;
            }
            base.DiscardImplicitReferences();
        }
    }
}
