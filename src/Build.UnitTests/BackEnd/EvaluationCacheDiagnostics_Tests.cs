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
                        ("ManifestValidation", 1), ("FileStatLoop", 1), ("Materialization", 1));
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
        AssertPhases(log, ("RequestKey", 1), ("CacheLookup", 1), ("Validation", 1), ("ManifestValidation", 1), ("FileStatLoop", 1),
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
        log.ShouldContain("ValidationDetail.GlobsRecorded:1");
        log.ShouldContain("ValidationDetail.GlobsReplayed:1");
        log.ShouldContain("ValidationDetail.ReplaysByDirectoryStamp:1");
        log.ShouldContain("ValidationDetail.ReplayMismatches:1");
        log.ShouldContain("ValidationDetail.ChangedGlobDirectories:");
        log.ShouldContain("ValidationDetail.RecordedFiles:");
        log.ShouldContain("ValidationDetail.ListedDirectories:");
        System.Text.RegularExpressions.Regex.IsMatch(log, "ValidationDetail\\.Driver(Legacy|OptimizedCallback|OptimizedDirect):1").ShouldBeTrue();
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
        log.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries).Length
            .ShouldBe(EvaluationCacheDiagnostics.MaximumEventsPerBuild + 2);
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
            AssertPhases(log, ("RequestKey", 1), ("CacheLookup", 1), ("Validation", 1),
                ("FreshEvaluation", 1), ("SnapshotCreation", 1), ("CacheAdmission", 2), ("FallbackPreparation", 1));
            Examples(log).Single().ShouldContain("|Event=Fallback|Reason=ValidationError|Detail=System.InvalidOperationException");
        }
        else
        {
            AssertPhases(log, ("RequestKey", 1), ("CacheLookup", 1), ("Validation", 1));
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
            AssertPhases(log, ("RequestKey", 1), ("CacheLookup", 1), ("Validation", 1),
                ("CacheAdmission", 1), ("FallbackPreparation", 1));
            cache.GetStatistics().FreshEvaluations.ShouldBe(1);
        }
        else
        {
            AssertPhases(log, ("RequestKey", 1), ("CacheLookup", 1), ("Validation", 1),
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
            if (phase is "ManifestValidation" or "SdkValidation")
            {
                fields["NestedUnder"].ShouldBe("Validation");
                total.ShouldBeLessThanOrEqualTo(long.Parse(phases["Validation"]["TotalTicks"], CultureInfo.InvariantCulture));
            }
            else if (phase is "FileStatLoop" or "GlobReplay")
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
