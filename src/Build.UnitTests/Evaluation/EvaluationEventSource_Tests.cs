// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Eventing;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Evaluation;

public sealed class EvaluationEventSource_Tests : IDisposable
{
    private const string ProjectXml = """
        <Project>
          <PropertyGroup>
            <Property>value</Property>
          </PropertyGroup>
          <ItemDefinitionGroup>
            <Compile>
              <Kind>source</Kind>
            </Compile>
          </ItemDefinitionGroup>
          <ItemGroup>
            <Compile Include="a.cs;b.cs" />
          </ItemGroup>
          <UsingTask TaskName="UnusedTask" AssemblyName="Unused.Assembly" />
          <Target Name="Build" />
        </Project>
        """;

    private readonly ProjectCollection _collection = new();
    private readonly MockLogger _logger;
    private readonly ITestOutputHelper _output;

    public EvaluationEventSource_Tests(ITestOutputHelper output)
    {
        _output = output;
        _logger = new MockLogger(output);
        _collection.RegisterLogger(_logger);
    }

    public void Dispose() => _collection.Dispose();

    [Fact]
    public void MeasurementEventsHaveStableOptInContract()
    {
        AssertEventContract(
            nameof(MSBuildEventSource.ProjectEvaluationCompleted),
            113,
            ["durationSeconds", "stage", "origin", "succeeded", "projectFile", "evaluationId"],
            [typeof(double), typeof(string), typeof(string), typeof(bool), typeof(string), typeof(int)]);
        AssertEventContract(
            nameof(MSBuildEventSource.ProjectEvaluationPassCompleted),
            114,
            ["durationSeconds", "stage", "pass", "origin", "projectFile", "evaluationId"],
            [typeof(double), typeof(string), typeof(string), typeof(string), typeof(string), typeof(int)]);
        AssertEventContract(
            nameof(MSBuildEventSource.ProjectEvaluationGlobbingCompleted),
            117,
            ["durationSeconds", "stage", "origin", "succeeded", "projectFile", "evaluationId"],
            [typeof(double), typeof(string), typeof(string), typeof(bool), typeof(string), typeof(int)]);

        using var listener = new EvaluationEventListener();
        listener.Enable(MSBuildEventSource.Keywords.All);
        MSBuildEventSource.Log.ProjectEvaluationCompleted(1, "full", "outside_build_submission", succeeded: true, "", 1);
        MSBuildEventSource.Log.ProjectEvaluationGlobbingCompleted(0, "full", "outside_build_submission", succeeded: true, "", 1);
        listener.Events.ShouldBeEmpty();

        listener.Enable(MSBuildEventSource.Keywords.EvaluationMeasurements);
        MSBuildEventSource.Log.ProjectEvaluationCompleted(1, "full", "outside_build_submission", succeeded: true, "", 1);

        EvaluationEvent observed = listener.Events.ShouldHaveSingleItem();
        observed.Id.ShouldBe(113);
        observed.Level.ShouldBe(EventLevel.Informational);
        observed.Opcode.ShouldBe(EventOpcode.Info);
        observed.PayloadNames.ShouldBe(["durationSeconds", "stage", "origin", "succeeded", "projectFile", "evaluationId"]);
        observed.Payload.Select(value => value?.GetType()).ShouldBe(
            [typeof(double), typeof(string), typeof(string), typeof(bool), typeof(string), typeof(int)]);

        MSBuildEventSource.Log.ProjectEvaluationGlobbingCompleted(0, "full", "outside_build_submission", succeeded: true, "", 1);
        EvaluationEvent globbing = listener.Events.Where(e => e.Id == 117).ShouldHaveSingleItem();
        globbing.PayloadNames.ShouldBe(observed.PayloadNames);
        globbing.Payload.Select(value => value?.GetType()).ShouldBe(
            [typeof(double), typeof(string), typeof(string), typeof(bool), typeof(string), typeof(int)]);
    }

    [Fact]
    public void FullEvaluationPreservesLegacyEventOrderAndRecordsDeferredItems()
    {
        using var listener = new EvaluationEventListener();
        listener.Enable(MSBuildEventSource.Keywords.All | MSBuildEventSource.Keywords.EvaluationMeasurements);

        ProjectInstance instance = Evaluate(ProjectEvaluationStage.Full);

        instance.GetItems("Compile").Count.ShouldBe(2);

        int[] relevantIds = listener.Events
            .Where(e => e.Id is 11 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19 or 20 or 21 or 22 or 23 or 24 or 113 or 114)
            .Select(e => e.Id)
            .ToArray();

        relevantIds.ShouldBe(
        [
            11,
            13, 14, 114,
            15, 16, 114,
            17, 18, 114,
            19, 20, 114,
            21, 22, 114,
            23, 24, 114,
            113,
            12,
        ]);

        listener.Events.Where(e => e.Id == 114).Select(e => e.Payload[2]).ShouldBe(
        [
            "initial_properties",
            "properties",
            "item_definitions",
            "items",
            "using_tasks",
            "targets",
        ]);

        EvaluationEvent total = listener.Events.Where(e => e.Id == 113).ShouldHaveSingleItem();
        ProjectEvaluationStartedEventArgs evaluationStarted = _logger.AllBuildEvents
            .OfType<ProjectEvaluationStartedEventArgs>()
            .ShouldHaveSingleItem();
        int evaluationId = evaluationStarted.BuildEventContext!.EvaluationId;
        total.Payload[1].ShouldBe("full");
        total.Payload[2].ShouldBe("outside_build_submission");
        total.Payload[3].ShouldBe(true);
        total.Payload[4].ShouldBe(string.Empty);
        total.Payload[5].ShouldBe(evaluationId);
        ((double)total.Payload[0]!).ShouldBeGreaterThanOrEqualTo(0);
        listener.Events
            .Where(e => e.Id == 114)
            .ShouldAllBe(e => (string)e.Payload[4]! == string.Empty
                && (int)e.Payload[5]! == evaluationId);
        AssertGlobbingMeasurement(listener, total, 0);
    }

    [Theory]
    [InlineData(ProjectEvaluationStage.Properties, "properties", 2)]
    [InlineData(ProjectEvaluationStage.ItemDefinitions, "item_definitions", 3)]
    [InlineData(ProjectEvaluationStage.Items, "items", 4)]
    [InlineData(ProjectEvaluationStage.UsingTasks, "using_tasks", 5)]
    public void PartialEvaluationRecordsOnlyCompletedPassPrefix(ProjectEvaluationStage stage, string stageName, int passCount)
    {
        using var listener = new EvaluationEventListener();
        listener.Enable(MSBuildEventSource.Keywords.EvaluationMeasurements);

        Evaluate(stage);

        listener.Events.Count(e => e.Id == 114).ShouldBe(passCount);
        EvaluationEvent total = listener.Events.Where(e => e.Id == 113).ShouldHaveSingleItem();
        total.Payload[1].ShouldBe(stageName);
        total.Payload[3].ShouldBe(true);
        AssertEvaluationIdentity(listener, total);
        AssertGlobbingMeasurement(listener, total, 0);
    }

    [Fact]
    public void FailedEvaluationRecordsOneFailedTotalAndNoIncompletePass()
    {
        using var listener = new EvaluationEventListener();
        listener.Enable(MSBuildEventSource.Keywords.EvaluationMeasurements);

        Should.Throw<InvalidProjectFileException>(() => Evaluate("""
            <Project>
              <Import Project="missing.targets" />
            </Project>
            """));

        listener.Events.Count(e => e.Id == 114).ShouldBe(1);
        EvaluationEvent total = listener.Events.Where(e => e.Id == 113).ShouldHaveSingleItem();
        total.Payload[3].ShouldBe(false);
        AssertEvaluationIdentity(listener, total);
        AssertGlobbingMeasurement(listener, total, double.NaN);
    }

    [Fact]
    public void MissingStartTimestampRetainsTotalCountButNotPartialPass()
    {
        using var listener = new EvaluationEventListener();
        listener.Enable(MSBuildEventSource.Keywords.EvaluationMeasurements);

        EvaluationInstrumentation.RecordPass(
            double.NaN,
            ProjectEvaluationStage.Full,
            "properties",
            BuildEventContext.InvalidSubmissionId,
            "",
            BuildEventContext.InvalidEvaluationId);
        EvaluationInstrumentation.RecordEvaluation(
            0,
            ProjectEvaluationStage.Full,
            BuildEventContext.InvalidSubmissionId,
            succeeded: true,
            "",
            BuildEventContext.InvalidEvaluationId);

        listener.Events.ShouldNotContain(e => e.Id == 114);
        EvaluationEvent total = listener.Events.Where(e => e.Id == 113).ShouldHaveSingleItem();
        double.IsNaN((double)total.Payload[0]!).ShouldBeTrue();
        total.Payload[4].ShouldBe(string.Empty);
        total.Payload[5].ShouldBe(BuildEventContext.InvalidEvaluationId);
        AssertGlobbingMeasurement(listener, total, double.NaN);
    }

    [Fact]
    public void GlobbingDurationAccumulatesSearchesButNotCachedOrLiteralResults()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.CreateFile("a.cs", "");
        env.CreateFile("b.cs", "");
        env.CreateFile("a.txt", "");
        var context = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared);
        var measurement = new EvaluationInstrumentation.GlobbingMeasurement();
        using var listener = new EvaluationEventListener();
        listener.Enable(MSBuildEventSource.Keywords.EvaluationMeasurements);

        string[] Expand(string pattern) => EngineFileUtilities.GetFileListEscaped(
            env.DefaultTestDirectory.Path,
            pattern,
            fileMatcher: context.FileMatcher,
            globbingMeasurement: measurement);

        Expand("literal.cs").ShouldBe(["literal.cs"]);
        Expand("escaped%2A.cs").ShouldBe(["escaped%2A.cs"]);
        measurement.DurationSeconds.ShouldBe(0);

        Expand("*.cs").ShouldBe(["a.cs", "b.cs"]);
        double firstSearch = measurement.DurationSeconds;
        firstSearch.ShouldBeGreaterThan(0);

        Expand("*.cs").ShouldBe(["a.cs", "b.cs"]);
        measurement.DurationSeconds.ShouldBe(firstSearch);

        Expand("*.txt").ShouldBe(["a.txt"]);
        measurement.DurationSeconds.ShouldBeGreaterThan(firstSearch);
    }

    [Theory]
    [InlineData(ProjectEvaluationStage.Properties, "properties")]
    [InlineData(ProjectEvaluationStage.Items, "items")]
    [InlineData(ProjectEvaluationStage.Full, "full")]
    public void EvaluationAggregatesItemAndImportSearchesAndReportsZeroForWarmCache(ProjectEvaluationStage stage, string stageName)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.CreateFile("import.targets", "<Project />");
        env.CreateFile("a.cs", "");
        env.CreateFile("b.cs", "");
        string projectPath = env.CreateFile("globbing.proj", """
            <Project>
              <Import Project="*.targets" />
              <ItemGroup>
                <Compile Include="*.cs" Exclude="b.cs" />
                <Copy Include="@(Compile);@(Compile)" />
                <Literal Include="literal.cs;escaped%2A.cs" />
                <Skipped Include="*.txt" Condition="false" />
              </ItemGroup>
            </Project>
            """).Path;
        var context = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared);
        using var listener = new EvaluationEventListener();
        listener.Enable(MSBuildEventSource.Keywords.EvaluationMeasurements);

        for (int i = 0; i < 2; i++)
        {
            ProjectInstance instance = ProjectInstance.FromFile(projectPath, new ProjectOptions
            {
                ProjectCollection = _collection,
                EvaluationContext = context,
                EvaluationStage = stage,
            });
            if (stage >= ProjectEvaluationStage.Items)
            {
                instance.GetItems("Compile").ShouldHaveSingleItem().EvaluatedInclude.ShouldBe("a.cs");
                instance.GetItems("Copy").Count.ShouldBe(2);
                instance.GetItems("Literal").Count.ShouldBe(2);
                instance.GetItems("Skipped").ShouldBeEmpty();
            }
        }

        EvaluationEvent[] totals = listener.Events.Where(e => e.Id == 113).ToArray();
        EvaluationEvent[] globbing = listener.Events.Where(e => e.Id == 117).ToArray();
        globbing.Length.ShouldBe(2);
        ((double)globbing[0].Payload[0]!).ShouldBeGreaterThan(0);
        ((double)globbing[0].Payload[0]!).ShouldBeLessThanOrEqualTo((double)totals[0].Payload[0]!);
        globbing[1].Payload[0].ShouldBe(0d);
        for (int i = 0; i < 2; i++)
        {
            globbing[i].Payload.Skip(1).ShouldBe(totals[i].Payload.Skip(1));
            globbing[i].Payload[1].ShouldBe(stageName);
            globbing[i].Payload[2].ShouldBe("outside_build_submission");
            globbing[i].Payload[3].ShouldBe(true);
            globbing[i].Payload[4].ShouldBe(projectPath);
        }
        globbing.Select(e => (int)e.Payload[5]!).Distinct().Count().ShouldBe(2);
    }

    [Fact]
    public void MissingGlobTimestampInvalidatesAggregateRatherThanReportingZero()
    {
        using var listener = new EvaluationEventListener();
        listener.Enable(MSBuildEventSource.Keywords.EvaluationMeasurements);
        var measurement = new EvaluationInstrumentation.GlobbingMeasurement();
        measurement.AddMeasurement(Stopwatch.GetTimestamp());
        measurement.AddMeasurement(0);

        EvaluationInstrumentation.RecordEvaluation(
            EvaluationInstrumentation.StartMeasurement(),
            ProjectEvaluationStage.Full,
            BuildEventContext.InvalidSubmissionId,
            succeeded: true,
            "",
            BuildEventContext.InvalidEvaluationId,
            measurement);

        EvaluationEvent total = listener.Events.Where(e => e.Id == 113).ShouldHaveSingleItem();
        AssertGlobbingMeasurement(listener, total, double.NaN);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LazyWildcardsAreMeasuredOnlyWhenATransformExpandsThem(bool expand)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MsBuildSkipEagerWildCardEvaluationRegexes", ".*");
        EngineFileUtilities.CaptureLazyWildcardRegexes();
        env.CreateFile("a.cs", "");
        string projectPath = env.CreateFile("lazy-globbing.proj", """
            <Project>
              <ItemGroup>
                <Deferred Include="*.cs" />
                <Expanded Include="@(Deferred->'%(Identity)')" Condition="'$(Expand)' == 'true'" />
              </ItemGroup>
            </Project>
            """).Path;
        using var listener = new EvaluationEventListener();
        listener.Enable(MSBuildEventSource.Keywords.EvaluationMeasurements);

        ProjectInstance instance = ProjectInstance.FromFile(projectPath, new ProjectOptions
        {
            ProjectCollection = _collection,
            GlobalProperties = new Dictionary<string, string> { ["Expand"] = expand ? "true" : "false" },
        });

        instance.GetItems("Deferred").ShouldHaveSingleItem().EvaluatedInclude.ShouldBe("*.cs");
        EvaluationEvent total = listener.Events.Where(e => e.Id == 113).ShouldHaveSingleItem();
        if (expand)
        {
            instance.GetItems("Expanded").ShouldHaveSingleItem().EvaluatedInclude.ShouldBe("a.cs");
            EvaluationEvent globbing = listener.Events.Where(e => e.Id == 117).ShouldHaveSingleItem();
            ((double)globbing.Payload[0]!).ShouldBeGreaterThan(0);
            ((double)globbing.Payload[0]!).ShouldBeLessThanOrEqualTo((double)total.Payload[0]!);
            globbing.Payload.Skip(1).ShouldBe(total.Payload.Skip(1));
        }
        else
        {
            instance.GetItems("Expanded").ShouldBeEmpty();
            AssertGlobbingMeasurement(listener, total, 0);
        }
    }

    [Fact]
    public void GeneralTracingPreservesGlobPairsWithoutEnablingMeasurements()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.CreateFile("a.cs", "");
        string projectPath = env.CreateFile("globbing.proj", """
            <Project>
              <ItemGroup>
                <Compile Include="*.cs" />
              </ItemGroup>
            </Project>
            """).Path;
        using var listener = new EvaluationEventListener();
        listener.Enable(MSBuildEventSource.Keywords.All);

        ProjectInstance.FromFile(projectPath, new ProjectOptions { ProjectCollection = _collection })
            .GetItems("Compile").ShouldHaveSingleItem();

        listener.Events.ShouldNotContain(e => e.Id == 113 || e.Id == 114 || e.Id == 117);
        listener.Events.Where(e => e.Id is 41 or 42).Select(e => e.Id).ShouldBe([41, 42]);
    }

    [Fact]
    public void FailedEvaluationDoesNotReportAPartialGlobAggregate()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.CreateFile("a.cs", "");
        string projectPath = env.CreateFile("failed-globbing.proj", """
            <Project>
              <ItemGroup>
                <Compile Include="*.cs">
                  <Invalid>$([System.Int32]::Parse('not_an_integer'))</Invalid>
                </Compile>
              </ItemGroup>
            </Project>
            """).Path;
        using var listener = new EvaluationEventListener();
        listener.Enable(MSBuildEventSource.Keywords.All | MSBuildEventSource.Keywords.EvaluationMeasurements);

        Should.Throw<InvalidProjectFileException>(() => ProjectInstance.FromFile(
            projectPath,
            new ProjectOptions { ProjectCollection = _collection }));

        listener.Events.Where(e => e.Id is 41 or 42).Select(e => e.Id).ShouldBe([41, 42]);
        EvaluationEvent total = listener.Events.Where(e => e.Id == 113).ShouldHaveSingleItem();
        total.Payload[3].ShouldBe(false);
        AssertGlobbingMeasurement(listener, total, double.NaN);
    }

    [Fact]
    public void BuildEvaluationRecordsBuildSubmissionOrigin()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string projectPath = env.CreateFile("event-source.proj", ProjectXml).Path;
        using var listener = new EvaluationEventListener();
        listener.Enable(MSBuildEventSource.Keywords.EvaluationMeasurements);
        using var buildManager = new BuildManager();

        BuildResult result = buildManager.Build(
            new BuildParameters(_collection) { Loggers = [_logger] },
            new BuildRequestData(projectPath, new Dictionary<string, string?>(), null, ["Build"], null));

        result.OverallResult.ShouldBe(BuildResultCode.Success);
        listener.Events.ShouldContain(e => e.Id == 113 && (string)e.Payload[2]! == "build_submission");
        EvaluationEvent total = listener.Events.Where(e => e.Id == 113).ShouldHaveSingleItem();
        total.Payload[4].ShouldBe(projectPath);
        AssertEvaluationIdentity(listener, total);
        AssertGlobbingMeasurement(listener, total, 0);
    }

    [Fact]
    public void ReevaluationRecordsAnotherCompleteEvaluation()
    {
        using var listener = new EvaluationEventListener();
        listener.Enable(MSBuildEventSource.Keywords.EvaluationMeasurements);
        using XmlReader reader = XmlReader.Create(new StringReader(ProjectXml));
        var project = new Project(ProjectRootElement.Create(reader), null, null, _collection);

        project.SetProperty("ReevaluationTrigger", "changed");
        project.ReevaluateIfNecessary();

        EvaluationEvent[] totals = listener.Events.Where(e => e.Id == 113).ToArray();
        totals.Length.ShouldBe(2);
        listener.Events.Count(e => e.Id == 114).ShouldBe(12);

        int[] totalEvaluationIds = totals.Select(e => (int)e.Payload[5]!).ToArray();
        totalEvaluationIds.Distinct().Count().ShouldBe(2);
        totalEvaluationIds.ShouldBe(
            _logger.AllBuildEvents
                .OfType<ProjectEvaluationStartedEventArgs>()
                .Select(e => e.BuildEventContext!.EvaluationId));

        listener.Events
            .Where(e => e.Id == 114)
            .GroupBy(e => (int)e.Payload[5]!)
            .ToDictionary(group => group.Key, group => group.Count())
            .ShouldBe(totalEvaluationIds.ToDictionary(id => id, _ => 6));

        listener.Events
            .Where(e => e.Id is 113 or 114 or 117)
            .ShouldAllBe(e => (string)e.Payload[4]! == string.Empty);
        listener.Events.Where(e => e.Id == 117).Select(e => (int)e.Payload[5]!).ShouldBe(totalEvaluationIds);
        listener.Events.Where(e => e.Id == 117).ShouldAllBe(e => (double)e.Payload[0]! == 0);
    }

    private ProjectInstance Evaluate(ProjectEvaluationStage stage)
    {
        using XmlReader reader = XmlReader.Create(new StringReader(ProjectXml));
        ProjectRootElement root = ProjectRootElement.Create(reader);
        return ProjectInstance.FromProjectRootElement(root, new ProjectOptions
        {
            EvaluationStage = stage,
            ProjectCollection = _collection,
        });
    }

    private ProjectInstance Evaluate(string projectXml)
    {
        using XmlReader reader = XmlReader.Create(new StringReader(projectXml));
        ProjectRootElement root = ProjectRootElement.Create(reader);
        return ProjectInstance.FromProjectRootElement(root, new ProjectOptions { ProjectCollection = _collection });
    }

    private void AssertEvaluationIdentity(EvaluationEventListener listener, EvaluationEvent total)
    {
        ProjectEvaluationStartedEventArgs evaluationStarted = _logger.AllBuildEvents
            .OfType<ProjectEvaluationStartedEventArgs>()
            .ShouldHaveSingleItem();
        int evaluationId = evaluationStarted.BuildEventContext!.EvaluationId;

        total.Payload[5].ShouldBe(evaluationId);
        listener.Events
            .Where(e => e.Id is 114 or 117)
            .ShouldAllBe(e => (int)e.Payload[5]! == evaluationId);
    }

    private static void AssertGlobbingMeasurement(EvaluationEventListener listener, EvaluationEvent total, double durationSeconds)
    {
        EvaluationEvent globbing = listener.Events.Where(e => e.Id == 117).ShouldHaveSingleItem();
        if (double.IsNaN(durationSeconds))
        {
            double.IsNaN((double)globbing.Payload[0]!).ShouldBeTrue();
        }
        else
        {
            ((double)globbing.Payload[0]!).ShouldBe(durationSeconds);
        }
        globbing.Payload.Skip(1).ShouldBe(total.Payload.Skip(1));
    }

    private static void AssertEventContract(string methodName, int id, string[] parameterNames, Type[] parameterTypes)
    {
        MethodInfo method = typeof(MSBuildEventSource).GetMethod(methodName)!;
        EventAttribute attribute = method.GetCustomAttribute<EventAttribute>()!;

        attribute.EventId.ShouldBe(id);
        attribute.Level.ShouldBe(EventLevel.Informational);
        attribute.Opcode.ShouldBe(EventOpcode.Info);
        attribute.Keywords.ShouldBe(MSBuildEventSource.Keywords.EvaluationMeasurements);
        attribute.Version.ShouldBe((byte)0);
        method.GetParameters().Select(p => p.Name).ShouldBe(parameterNames);
        method.GetParameters().Select(p => p.ParameterType).ShouldBe(parameterTypes);
    }

    private sealed class EvaluationEventListener : EventListener
    {
        private readonly List<EvaluationEvent> _events = [];

        internal IReadOnlyList<EvaluationEvent> Events
        {
            get
            {
                lock (_events)
                {
                    return _events.ToArray();
                }
            }
        }

        internal void Enable(EventKeywords keywords)
            => EnableEvents(MSBuildEventSource.Log, EventLevel.Informational, keywords);

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.EventSource.Name != "Microsoft-Build")
            {
                return;
            }

            lock (_events)
            {
                _events.Add(new EvaluationEvent(
                    eventData.EventId,
                    eventData.Level,
                    eventData.Opcode,
                    eventData.PayloadNames?.ToArray() ?? [],
                    eventData.Payload?.ToArray() ?? []));
            }
        }
    }

    private sealed class EvaluationEvent(
        int id,
        EventLevel level,
        EventOpcode opcode,
        string[] payloadNames,
        object?[] payload)
    {
        internal int Id { get; } = id;
        internal EventLevel Level { get; } = level;
        internal EventOpcode Opcode { get; } = opcode;
        internal string[] PayloadNames { get; } = payloadNames;
        internal object?[] Payload { get; } = payload;
    }
}
