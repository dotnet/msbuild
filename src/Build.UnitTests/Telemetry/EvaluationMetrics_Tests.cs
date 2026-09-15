// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Engine.UnitTests.BackEnd;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Eventing;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.TelemetryInfra;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Engine.UnitTests;

[CollectionDefinition(CollectionName, DisableParallelization = true)]
public sealed class EvaluationMetricsTestCollection
{
    public const string CollectionName = nameof(EvaluationMetricsTestCollection);
}

[Collection(EvaluationMetricsTestCollection.CollectionName)]
public sealed class EvaluationMetrics_Tests
{
    private readonly ITestOutputHelper _output;

    public EvaluationMetrics_Tests(ITestOutputHelper output)
    {
        _output = output;
        EvaluationInstrumentation.ResetForTests();
    }

    [Theory]
    [InlineData(ProjectEvaluationStage.Properties)]
    [InlineData(ProjectEvaluationStage.ItemDefinitions)]
    [InlineData(ProjectEvaluationStage.Items)]
    [InlineData(ProjectEvaluationStage.UsingTasks)]
    [InlineData(ProjectEvaluationStage.Full)]
    public void EvaluationEventSourcePassesMatchRequestedStage(ProjectEvaluationStage stage)
    {
        using EventSourceTestHelper eventSourceListener = new();
        using ProjectCollection collection = new();

        _ = ProjectInstance.FromProjectRootElement(
            CreateRootElement("<Project />"),
            new ProjectOptions
            {
                EvaluationStage = stage,
                ProjectCollection = collection,
            });

        List<EventWrittenEventArgs> events = eventSourceListener.GetEvents();
        events.Count(eventData => eventData.EventName == nameof(MSBuildEventSource.EvaluateStart)).ShouldBe(1);
        events.Count(eventData => eventData.EventName == nameof(MSBuildEventSource.EvaluateStop)).ShouldBe(1);
        GetCompletedPasses(events).ShouldBe(GetExpectedPasses(stage));
    }

    [Fact]
    public void FailedEvaluationPairsTotalEventSourceWithoutCompletingFailedPass()
    {
        using EventSourceTestHelper eventSourceListener = new();
        using ProjectCollection collection = new();

        Should.Throw<InvalidProjectFileException>(() =>
            ProjectInstance.FromProjectRootElement(
                CreateRootElement(
                    """
                    <Project>
                      <PropertyGroup Condition="'invalid' ==">
                        <Value>1</Value>
                      </PropertyGroup>
                    </Project>
                    """),
                new ProjectOptions { ProjectCollection = collection }));

        List<EventWrittenEventArgs> events = eventSourceListener.GetEvents();
        events.Count(eventData => eventData.EventName == nameof(MSBuildEventSource.EvaluateStart)).ShouldBe(1);
        events.Count(eventData => eventData.EventName == nameof(MSBuildEventSource.EvaluateStop)).ShouldBe(1);
        events.Count(eventData => eventData.EventName == nameof(MSBuildEventSource.EvaluatePass1Start)).ShouldBe(1);
        events.Count(eventData => eventData.EventName == nameof(MSBuildEventSource.EvaluatePass1Stop)).ShouldBe(0);
    }

    [Fact]
    public void InMemoryEvaluationPreservesPassProjectFilePayloads()
    {
        using EventSourceTestHelper eventSourceListener = new();
        using ProjectCollection collection = new();

        _ = ProjectInstance.FromProjectRootElement(
            CreateRootElement("<Project />"),
            new ProjectOptions { ProjectCollection = collection });

        List<EventWrittenEventArgs> events = eventSourceListener.GetEvents();
        GetProjectFilePayload(events, nameof(MSBuildEventSource.EvaluatePass1Start)).ShouldBe("(null)");
        GetProjectFilePayload(events, nameof(MSBuildEventSource.EvaluatePass1Stop)).ShouldBe("(null)");
        GetProjectFilePayload(events, nameof(MSBuildEventSource.EvaluatePass2Start)).ShouldBe("(null)");
        GetProjectFilePayload(events, nameof(MSBuildEventSource.EvaluatePass2Stop)).ShouldBe("(null)");
        GetProjectFilePayload(events, nameof(MSBuildEventSource.EvaluatePass3Start)).ShouldBe("(null)");
        GetProjectFilePayload(events, nameof(MSBuildEventSource.EvaluatePass3Stop)).ShouldBe("(null)");
        GetProjectFilePayload(events, nameof(MSBuildEventSource.EvaluatePass4Start)).ShouldBe("(null)");
        GetProjectFilePayload(events, nameof(MSBuildEventSource.EvaluatePass4Stop)).ShouldBe("(null)");
        GetProjectFilePayload(events, nameof(MSBuildEventSource.EvaluatePass5Start)).ShouldBe("(null)");
        GetProjectFilePayload(events, nameof(MSBuildEventSource.EvaluatePass5Stop)).ShouldBe("(null)");
    }

    [Fact]
    public void EvaluationFinishedLoggingPrecedesEventSourceStop()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        TransientTestFile projectFile = env.CreateFile("evaluation-ordering.proj", "<Project />");
        List<string> ordering = [];
        using MetricCollector collector = new(instrument =>
        {
            if (instrument.Name == EvaluationInstrumentation.ProjectEvaluationDurationName)
            {
                ordering.Add("evaluation-metric");
            }
        });
        EvaluationFinishedOrderingLogger logger = new(projectFile.Path, () => ordering.Add("evaluation-finished"));
        using EvaluationStopEventListener eventSourceListener =
            new(projectFile.Path, () => ordering.Add("event-source-stop"));
        using ProjectCollection collection = new(
            new Dictionary<string, string>(),
            [logger],
            ToolsetDefinitionLocations.Default);

        _ = collection.LoadProject(projectFile.Path);

        ordering.ShouldBe(["evaluation-metric", "evaluation-finished", "event-source-stop"]);
    }

    [Theory]
    [InlineData(ProjectEvaluationStage.Properties, "properties")]
    [InlineData(ProjectEvaluationStage.ItemDefinitions, "item_definitions")]
    [InlineData(ProjectEvaluationStage.Items, "items")]
    [InlineData(ProjectEvaluationStage.UsingTasks, "using_tasks")]
    [InlineData(ProjectEvaluationStage.Full, "full")]
    public void EvaluationMetricsCaptureStageAndDuration(ProjectEvaluationStage stage, string expectedStage)
    {
        using MetricCollector collector = new();
        using ProjectCollection collection = new();

        _ = ProjectInstance.FromProjectRootElement(
            CreateRootElement("<Project />"),
            new ProjectOptions
            {
                EvaluationStage = stage,
                ProjectCollection = collection,
            });

        List<MetricMeasurement> evaluationCounts = collector.Measurements
            .Where(measurement => measurement.InstrumentName == EvaluationInstrumentation.ProjectEvaluationCountName)
            .ToList();
        evaluationCounts.Count.ShouldBe(1);
        evaluationCounts[0].Value.ShouldBe(1);
        evaluationCounts[0].HasTag(EvaluationInstrumentation.StageTagName, expectedStage).ShouldBeTrue();
        evaluationCounts[0].HasTag(EvaluationInstrumentation.OriginTagName, EvaluationInstrumentation.OutsideBuildSubmissionOrigin).ShouldBeTrue();
        evaluationCounts[0].HasTag(EvaluationInstrumentation.SucceededTagName, true).ShouldBeTrue();

        List<MetricMeasurement> evaluationDurations = collector.Measurements
            .Where(measurement => measurement.InstrumentName == EvaluationInstrumentation.ProjectEvaluationDurationName)
            .ToList();
        evaluationDurations.Count.ShouldBe(1);
        evaluationDurations[0].Value.ShouldBeGreaterThanOrEqualTo(0);
        evaluationDurations[0].HasTag(EvaluationInstrumentation.StageTagName, expectedStage).ShouldBeTrue();
        evaluationDurations[0].HasTag(EvaluationInstrumentation.OriginTagName, EvaluationInstrumentation.OutsideBuildSubmissionOrigin).ShouldBeTrue();
        evaluationDurations[0].HasTag(EvaluationInstrumentation.SucceededTagName, true).ShouldBeTrue();

        List<string> metricPasses = [];
        foreach (MetricMeasurement measurement in collector.Measurements)
        {
            if (measurement.InstrumentName == EvaluationInstrumentation.ProjectEvaluationPassDurationName)
            {
                measurement.Value.ShouldBeGreaterThanOrEqualTo(0);
                measurement.HasTag(EvaluationInstrumentation.StageTagName, expectedStage).ShouldBeTrue();
                measurement.HasTag(EvaluationInstrumentation.OriginTagName, EvaluationInstrumentation.OutsideBuildSubmissionOrigin).ShouldBeTrue();
                metricPasses.Add(measurement.Tags[EvaluationInstrumentation.PassTagName].ShouldBeOfType<string>());
            }
        }

        metricPasses.ShouldBe(GetExpectedPasses(stage));
    }

    [Fact]
    public void ItemsMetricCoversDeferredItemRealizationWithoutSeparateSeries()
    {
        using MetricCollector collector = new();
        using ProjectCollection collection = new();

        ProjectInstance instance = ProjectInstance.FromProjectRootElement(
            CreateRootElement(
                """
                <Project>
                  <ItemGroup>
                    <Input Include="a;b" />
                    <Result Include="@(Input)" />
                  </ItemGroup>
                </Project>
                """),
            new ProjectOptions { ProjectCollection = collection });

        instance.GetItems("Result").Count.ShouldBe(2);

        List<MetricMeasurement> itemPassMeasurements = collector.Measurements
            .Where(measurement =>
                measurement.InstrumentName == EvaluationInstrumentation.ProjectEvaluationPassDurationName &&
                measurement.HasTag(EvaluationInstrumentation.PassTagName, "items"))
            .ToList();
        itemPassMeasurements.Count.ShouldBe(1);
        collector.Measurements.ShouldNotContain(measurement =>
            measurement.InstrumentName == EvaluationInstrumentation.ProjectEvaluationPassDurationName &&
            measurement.HasTag(EvaluationInstrumentation.PassTagName, "lazy_items"));
    }

    [Fact]
    public void EvaluationMetricsCaptureBuildSubmissionOrigin()
    {
        using MetricCollector collector = new();
        using TestEnvironment env = TestEnvironment.Create(_output);

        TransientTestFile buildProject = env.CreateFile(
            "evaluation-metrics.proj",
            """
            <Project>
              <Target Name="Build" />
            </Project>
            """);
        MockLogger logger = new(_output);
        using (BuildManager buildManager = new())
        {
            BuildResult result = buildManager.Build(
                new BuildParameters { Loggers = [logger] },
                new BuildRequestData(
                    buildProject.Path,
                    new Dictionary<string, string?>(),
                    null,
                    ["Build"],
                    null));
            result.ShouldHaveSucceeded();
        }

        collector.Measurements.ShouldContain(measurement =>
            measurement.InstrumentName == EvaluationInstrumentation.ProjectEvaluationCountName &&
            measurement.HasTag(EvaluationInstrumentation.StageTagName, "full") &&
            measurement.HasTag(EvaluationInstrumentation.OriginTagName, EvaluationInstrumentation.BuildSubmissionOrigin) &&
            measurement.HasTag(EvaluationInstrumentation.SucceededTagName, true));

        collector.Measurements.ShouldContain(measurement =>
            measurement.InstrumentName == EvaluationInstrumentation.ProjectEvaluationPassDurationName &&
            measurement.HasTag(EvaluationInstrumentation.PassTagName, "targets") &&
            measurement.HasTag(EvaluationInstrumentation.StageTagName, "full") &&
            measurement.HasTag(EvaluationInstrumentation.OriginTagName, EvaluationInstrumentation.BuildSubmissionOrigin));
    }

    [Fact]
    public void EvaluationMetricsCaptureFailedEvaluation()
    {
        using MetricCollector collector = new();
        using ProjectCollection collection = new();

        Should.Throw<InvalidProjectFileException>(() =>
            ProjectInstance.FromProjectRootElement(
                CreateRootElement(
                    """
                    <Project>
                      <PropertyGroup Condition="'invalid' ==">
                        <Value>1</Value>
                      </PropertyGroup>
                    </Project>
                    """),
                new ProjectOptions { ProjectCollection = collection }));

        collector.Measurements.ShouldContain(measurement =>
            measurement.InstrumentName == EvaluationInstrumentation.ProjectEvaluationCountName &&
            measurement.HasTag(EvaluationInstrumentation.StageTagName, "full") &&
            measurement.HasTag(EvaluationInstrumentation.OriginTagName, EvaluationInstrumentation.OutsideBuildSubmissionOrigin) &&
            measurement.HasTag(EvaluationInstrumentation.SucceededTagName, false));
    }

    [Fact]
    public void ThrowingMetricsListenerDoesNotBreakEvaluation()
    {
        using ResetMetricsOnDispose reset = new();
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == EvaluationInstrumentation.MeterName &&
                instrument.Name == EvaluationInstrumentation.ProjectEvaluationCountName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, _, _) => throw new InvalidOperationException("Test listener failure"));
        listener.Start();

        using ProjectCollection collection = new();
        Should.NotThrow(() =>
            ProjectInstance.FromProjectRootElement(
                CreateRootElement("<Project />"),
                new ProjectOptions { ProjectCollection = collection }));
    }

    [WindowsFullFrameworkOnlyFact]
    public void MissingDiagnosticSourceDoesNotBreakEvaluation()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        TransientTestFolder isolatedMSBuild = env.CreateFolder(createFolder: true);
        string bootstrapDirectory = Path.GetDirectoryName(RunnerUtilities.BootstrapMSBuildExecutablePath).ShouldNotBeNull();
        string diagnosticSourceFileName = "System.Diagnostics.DiagnosticSource.dll";

        File.Exists(Path.Combine(bootstrapDirectory, diagnosticSourceFileName)).ShouldBeTrue();
        foreach (string file in Directory.EnumerateFiles(bootstrapDirectory))
        {
            if (!string.Equals(Path.GetFileName(file), diagnosticSourceFileName, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(file, Path.Combine(isolatedMSBuild.Path, Path.GetFileName(file)));
            }
        }

        TransientTestFile project = env.CreateFile(
            isolatedMSBuild,
            "evaluation-metrics.proj",
            """
            <Project>
              <Target Name="Build" />
            </Project>
            """);

        string output = RunnerUtilities.ExecMSBuild(
            Path.Combine(isolatedMSBuild.Path, "MSBuild.exe"),
            $"\"{project.Path}\" -nologo -v:q -m:1 -nr:false",
            out bool success,
            outputHelper: _output);

        success.ShouldBeTrue(output);
    }

    private static List<string> GetCompletedPasses(IEnumerable<EventWrittenEventArgs> events)
    {
        List<string> passes = [];
        foreach (EventWrittenEventArgs eventData in events)
        {
            string? pass = eventData.EventName switch
            {
                nameof(MSBuildEventSource.EvaluatePass0Stop) => "initial_properties",
                nameof(MSBuildEventSource.EvaluatePass1Stop) => "properties",
                nameof(MSBuildEventSource.EvaluatePass2Stop) => "item_definitions",
                nameof(MSBuildEventSource.EvaluatePass3Stop) => "items",
                nameof(MSBuildEventSource.EvaluatePass4Stop) => "using_tasks",
                nameof(MSBuildEventSource.EvaluatePass5Stop) => "targets",
                _ => null,
            };

            if (pass is not null)
            {
                passes.Add(pass);
            }
        }

        return passes;
    }

    private static object? GetProjectFilePayload(IEnumerable<EventWrittenEventArgs> events, string eventName) =>
        events.Single(eventData => eventData.EventName == eventName).Payload?[0];

    private static string[] GetExpectedPasses(ProjectEvaluationStage stage) => stage switch
    {
        ProjectEvaluationStage.Properties => ["initial_properties", "properties"],
        ProjectEvaluationStage.ItemDefinitions => ["initial_properties", "properties", "item_definitions"],
        ProjectEvaluationStage.Items => ["initial_properties", "properties", "item_definitions", "items"],
        ProjectEvaluationStage.UsingTasks => ["initial_properties", "properties", "item_definitions", "items", "using_tasks"],
        ProjectEvaluationStage.Full => ["initial_properties", "properties", "item_definitions", "items", "using_tasks", "targets"],
        _ => [],
    };

    private static ProjectRootElement CreateRootElement(string projectXml)
    {
        using StringReader stringReader = new(projectXml);
        using XmlReader xmlReader = XmlReader.Create(stringReader);
        return ProjectRootElement.Create(xmlReader);
    }

    private sealed class EvaluationFinishedOrderingLogger : ILogger
    {
        private readonly string _projectFile;
        private readonly Action _onEvaluationFinished;

        internal EvaluationFinishedOrderingLogger(string projectFile, Action onEvaluationFinished)
        {
            _projectFile = projectFile;
            _onEvaluationFinished = onEvaluationFinished;
        }

        public LoggerVerbosity Verbosity { get; set; }

        public string? Parameters { get; set; }

        public void Initialize(IEventSource eventSource)
        {
            eventSource.AnyEventRaised += OnAnyEventRaised;
        }

        public void Shutdown()
        {
        }

        private void OnAnyEventRaised(object sender, BuildEventArgs eventArgs)
        {
            if (eventArgs is ProjectEvaluationFinishedEventArgs evaluationFinished &&
                string.Equals(evaluationFinished.ProjectFile, _projectFile, StringComparison.OrdinalIgnoreCase))
            {
                _onEvaluationFinished();
            }
        }
    }

    private sealed class EvaluationStopEventListener : EventListener
    {
        private const string EventSourceName = "Microsoft-Build";

        private readonly string _projectFile;
        private readonly Action _onEvaluationStop;
        private EventSource? _eventSource;

        internal EvaluationStopEventListener(string projectFile, Action onEvaluationStop)
        {
            _projectFile = projectFile;
            _onEvaluationStop = onEvaluationStop;
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == EventSourceName)
            {
                EnableEvents(eventSource, EventLevel.LogAlways);
                _eventSource = eventSource;
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.EventName == nameof(MSBuildEventSource.EvaluateStop) &&
                eventData.Payload is not null &&
                eventData.Payload.Count > 0 &&
                string.Equals(eventData.Payload[0] as string, _projectFile, StringComparison.OrdinalIgnoreCase))
            {
                _onEvaluationStop();
            }
        }

        public override void Dispose()
        {
            if (_eventSource is not null)
            {
                DisableEvents(_eventSource);
            }

            base.Dispose();
        }
    }

    private sealed class MetricCollector : IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly Action<Instrument>? _onMeasurement;

        public MetricCollector(Action<Instrument>? onMeasurement = null)
        {
            _onMeasurement = onMeasurement;
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == EvaluationInstrumentation.MeterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Add(instrument, value, tags));
            _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Add(instrument, value, tags));
            _listener.Start();
        }

        public ConcurrentQueue<MetricMeasurement> Measurements { get; } = new();

        public void Dispose()
        {
            _listener.Dispose();
        }

        private void Add<T>(
            Instrument instrument,
            T value,
            ReadOnlySpan<KeyValuePair<string, object?>> tags)
            where T : struct
        {
            Dictionary<string, object?> copiedTags = new(tags.Length, StringComparer.Ordinal);
            foreach (KeyValuePair<string, object?> tag in tags)
            {
                copiedTags.Add(tag.Key, tag.Value);
            }

            _onMeasurement?.Invoke(instrument);
            Measurements.Enqueue(new MetricMeasurement(
                instrument.Name,
                Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture),
                copiedTags));
        }
    }

    private sealed record MetricMeasurement(
        string InstrumentName,
        double Value,
        Dictionary<string, object?> Tags)
    {
        public bool HasTag(string name, object expected) =>
            Tags.TryGetValue(name, out object? actual) && Equals(actual, expected);
    }

    private sealed class ResetMetricsOnDispose : IDisposable
    {
        public void Dispose()
        {
            EvaluationInstrumentation.ResetForTests();
        }
    }
}
