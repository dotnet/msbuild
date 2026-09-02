// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Threading;
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
using Shouldly;
using Xunit;

namespace Microsoft.Build.Engine.UnitTests;

[CollectionDefinition(CollectionName, DisableParallelization = true)]
public sealed class EvaluationMetricsTestCollection
{
    public const string CollectionName = nameof(EvaluationMetricsTestCollection);
}

[Collection(EvaluationMetricsTestCollection.CollectionName)]
public sealed class EvaluationMetrics_Tests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly TestEnvironment _testEnvironment;

    public EvaluationMetrics_Tests(ITestOutputHelper output)
    {
        _output = output;
        _testEnvironment = TestEnvironment.Create(output);
        _testEnvironment.SetEnvironmentVariable(
            EvaluationMetrics.IncludeSubmissionIdEnvironmentVariable,
            "1");
        EvaluationMetrics.ResetForTests();
    }

    public void Dispose()
    {
        _testEnvironment.Dispose();
        EvaluationMetrics.ResetForTests();
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
        using EventSourceTestHelper eventSourceListener = new();
        using ProjectCollection collection = new();

        _ = ProjectInstance.FromProjectRootElement(
            CreateRootElement("<Project />"),
            new ProjectOptions
            {
                EvaluationStage = stage,
                ProjectCollection = collection,
            });

        collector.Measurements.ShouldContain(measurement =>
            measurement.InstrumentName == EvaluationMetrics.ProjectEvaluationCountName &&
            measurement.Value == 1 &&
            measurement.HasTag(EvaluationMetrics.StageTagName, expectedStage) &&
            measurement.HasTag(EvaluationMetrics.OriginTagName, EvaluationMetrics.OutsideBuildSubmissionOrigin) &&
            measurement.HasTag(EvaluationMetrics.SubmissionIdTagName, BuildEventContext.InvalidSubmissionId) &&
            measurement.HasTag(EvaluationMetrics.SucceededTagName, true));

        collector.Measurements.ShouldContain(measurement =>
            measurement.InstrumentName == EvaluationMetrics.ProjectEvaluationDurationName &&
            measurement.Value >= 0 &&
            measurement.HasTag(EvaluationMetrics.StageTagName, expectedStage) &&
            measurement.HasTag(EvaluationMetrics.OriginTagName, EvaluationMetrics.OutsideBuildSubmissionOrigin) &&
            measurement.HasTag(EvaluationMetrics.SubmissionIdTagName, BuildEventContext.InvalidSubmissionId) &&
            measurement.HasTag(EvaluationMetrics.SucceededTagName, true));

        string[] expectedPasses = GetExpectedPasses(stage);
        List<string> metricPasses = [];
        foreach (MetricMeasurement measurement in collector.Measurements)
        {
            if (measurement.InstrumentName == EvaluationMetrics.ProjectEvaluationPassDurationName)
            {
                measurement.Value.ShouldBeGreaterThanOrEqualTo(0);
                measurement.HasTag(EvaluationMetrics.StageTagName, expectedStage).ShouldBeTrue();
                measurement.HasTag(EvaluationMetrics.OriginTagName, EvaluationMetrics.OutsideBuildSubmissionOrigin).ShouldBeTrue();
                measurement.HasTag(EvaluationMetrics.SubmissionIdTagName, BuildEventContext.InvalidSubmissionId).ShouldBeTrue();
                metricPasses.Add(measurement.Tags[EvaluationMetrics.PassTagName].ShouldBeOfType<string>());
            }
        }

        List<string> eventSourcePasses = [];
        foreach (EventWrittenEventArgs eventData in eventSourceListener.GetEvents())
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
                eventSourcePasses.Add(pass);
            }
        }

        metricPasses.ShouldBe(expectedPasses);
        eventSourcePasses.ShouldBe(expectedPasses);
        metricPasses.ShouldBe(eventSourcePasses);
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
            measurement.InstrumentName == EvaluationMetrics.ProjectEvaluationCountName &&
            measurement.HasTag(EvaluationMetrics.StageTagName, "full") &&
            measurement.HasTag(EvaluationMetrics.OriginTagName, EvaluationMetrics.BuildSubmissionOrigin) &&
            measurement.Tags[EvaluationMetrics.SubmissionIdTagName].ShouldBeOfType<int>() >= 0 &&
            measurement.HasTag(EvaluationMetrics.SucceededTagName, true));

        collector.Measurements.ShouldContain(measurement =>
            measurement.InstrumentName == EvaluationMetrics.ProjectEvaluationPassDurationName &&
            measurement.HasTag(EvaluationMetrics.PassTagName, "targets") &&
            measurement.HasTag(EvaluationMetrics.StageTagName, "full") &&
            measurement.HasTag(EvaluationMetrics.OriginTagName, EvaluationMetrics.BuildSubmissionOrigin) &&
            measurement.Tags[EvaluationMetrics.SubmissionIdTagName].ShouldBeOfType<int>() >= 0);

        int[] submissionIds = collector.Measurements
            .Where(measurement =>
                measurement.HasTag(EvaluationMetrics.OriginTagName, EvaluationMetrics.BuildSubmissionOrigin))
            .Select(measurement => measurement.Tags[EvaluationMetrics.SubmissionIdTagName].ShouldBeOfType<int>())
            .Distinct()
            .ToArray();
        submissionIds.Length.ShouldBe(1);
    }

    [Fact]
    public void EvaluationMetricsPreservePerManagerBuildSubmissionIds()
    {
        using MetricCollector collector = new();
        using TestEnvironment env = TestEnvironment.Create(_output);

        TransientTestFile firstProject = env.CreateFile(
            "evaluation-metrics-first.proj",
            """
            <Project>
              <Target Name="Build" />
            </Project>
            """);
        TransientTestFile secondProject = env.CreateFile(
            "evaluation-metrics-second.proj",
            """
            <Project>
              <Target Name="Build" />
            </Project>
            """);
        MockLogger logger = new(_output);

        foreach (string projectPath in new[] { firstProject.Path, secondProject.Path })
        {
            using BuildManager buildManager = new();
            BuildResult result = buildManager.Build(
                new BuildParameters { Loggers = [logger] },
                new BuildRequestData(
                    projectPath,
                    new Dictionary<string, string?>(),
                    null,
                    ["Build"],
                    null));
            result.ShouldHaveSucceeded();
        }

        int[] submissionIds = collector.Measurements
            .Where(measurement =>
                measurement.InstrumentName == EvaluationMetrics.ProjectEvaluationCountName &&
                measurement.HasTag(EvaluationMetrics.OriginTagName, EvaluationMetrics.BuildSubmissionOrigin))
            .Select(measurement => measurement.Tags[EvaluationMetrics.SubmissionIdTagName].ShouldBeOfType<int>())
            .ToArray();

        submissionIds.ShouldBe([0, 0]);
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
            measurement.InstrumentName == EvaluationMetrics.ProjectEvaluationCountName &&
            measurement.HasTag(EvaluationMetrics.StageTagName, "full") &&
            measurement.HasTag(EvaluationMetrics.OriginTagName, EvaluationMetrics.OutsideBuildSubmissionOrigin) &&
            measurement.HasTag(EvaluationMetrics.SubmissionIdTagName, BuildEventContext.InvalidSubmissionId) &&
            measurement.HasTag(EvaluationMetrics.SucceededTagName, false));
    }

    [Fact]
    public void SubmissionIdTagIsOptIn()
    {
        _testEnvironment.SetEnvironmentVariable(
            EvaluationMetrics.IncludeSubmissionIdEnvironmentVariable,
            null);
        EvaluationMetrics.ResetForTests();

        using MetricCollector collector = new();
        using ProjectCollection collection = new();

        _ = ProjectInstance.FromProjectRootElement(
            CreateRootElement("<Project />"),
            new ProjectOptions { ProjectCollection = collection });

        collector.Measurements.ShouldAllBe(measurement =>
            !measurement.Tags.ContainsKey(EvaluationMetrics.SubmissionIdTagName));
    }

    [Fact]
    public void ThrowingMetricsListenerDoesNotBreakEvaluation()
    {
        using ResetMetricsOnDispose reset = new();
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == EvaluationMetrics.MeterName &&
                instrument.Name == EvaluationMetrics.ProjectEvaluationCountName)
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

    private sealed class MetricCollector : IDisposable
    {
        private readonly MeterListener _listener = new();

        public MetricCollector()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == EvaluationMetrics.MeterName)
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
            EvaluationMetrics.ResetForTests();
        }
    }
}
