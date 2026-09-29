// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Evaluation;
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
            manager.BeginBuild(parameters);
            cache = ((IBuildComponentHost)manager).BuildParameters.ProjectInstanceSnapshotCache;
            manager.BuildRequest(new BuildRequestData(project, new Dictionary<string, string?>(), null, ["Build"], null))
                .ShouldHaveSucceeded();
            manager.EndBuild();
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
        }
        else
        {
            cache.Diagnostics.ShouldBeNull();
            logger.FullLog.ShouldNotContain("EvaluationCacheDiagnostic");
        }
    }

    [Fact]
    public void DiagnosticsAloneDoNotActivateCache()
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
        manager.BuildRequest(new BuildRequestData(project, new Dictionary<string, string?>(), null, ["Build"], null))
            .ShouldHaveSucceeded();
        manager.EndBuild();
        logger.FullLog.ShouldContain("|Reason=CacheDisabled");
        logger.FullLog.ShouldNotContain("|Reason=Stored");
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
        Load(project, parameters, host, new Dictionary<string, string?> { ["MSBuildRestoreSessionId"] = "private-restore-value" });
        File.WriteAllText(project, "<Project><PropertyGroup><Changed>yes</Changed></PropertyGroup></Project>");
        Load(project, parameters, host);
        string log = Flush(cache.Diagnostics!);
        log.ShouldContain("|Event=Bypass|Reason=RestoreRequest|");
        log.ShouldContain("|Event=Validation|Reason=FileSystemInputChanged|");
        log.ShouldContain(EvaluationCacheDiagnostics.Escape(project));
        log.ShouldNotContain("private-restore-value");
        cache.GetStatistics().FreshEvaluations.ShouldBe(3);
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
            .ShouldBe(EvaluationCacheDiagnostics.MaximumEventsPerBuild + 1);
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
    public async Task ConcurrentRecordsAreCountedAndLoggingRunsOutsideDiagnosticLock()
    {
        var diagnostics = new EvaluationCacheDiagnostics();
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < 50; i++)
            {
                diagnostics.Record(null, "Reuse", "Materialized");
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
                Task.Run(() => diagnostics.Record(null, "Lookup", "NoEntryOrHistory"))
                    .Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            }
        }));
        messages.Last().ShouldContain("Reuse.Materialized:200");
        Flush(diagnostics).ShouldContain("Lookup.NoEntryOrHistory:1");
    }

    [Fact]
    public void OrdinaryDiagnosticMessagesSurviveBinaryLogReplay()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        Configure(env, true, EvaluationCacheMode.SnapshotFileSystem);
        string project = env.CreateFile("binary.proj", """<Project><Target Name="Build" /></Project>""").Path;
        string binlog = env.CreateFile("diagnostics.binlog").Path;
        var logger = new MockLogger(_output);
        using var manager = new BuildManager();
        manager.Build(
            new BuildParameters { Loggers = [logger, new BinaryLogger { Parameters = binlog }] },
            new BuildRequestData(project, new Dictionary<string, string?>(), null, ["Build"], null))
            .ShouldHaveSucceeded();
        List<string> messages = [];
        var replay = new BinaryLogReplayEventSource();
        replay.MessageRaised += (_, args) =>
        {
            if (args.Message?.StartsWith("EvaluationCacheDiagnostic", StringComparison.Ordinal) == true)
            {
                args.Importance.ShouldBe(MessageImportance.Low);
                messages.Add(args.Message);
            }
        };
        replay.Replay(binlog);
        messages.ShouldNotBeEmpty();
        messages.ShouldAllBe(message => logger.FullLog.Contains(message));
        messages.ShouldContain(message => message.StartsWith("EvaluationCacheDiagnosticSummary|", StringComparison.Ordinal));
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
}
