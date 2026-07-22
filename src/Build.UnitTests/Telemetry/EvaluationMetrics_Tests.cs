// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
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
public sealed class EvaluationMetrics_Tests
{
    private readonly ITestOutputHelper _output;

    public EvaluationMetrics_Tests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void EvaluationMetricsCaptureStageAndDuration()
    {
        using MetricCollector collector = new();
        using ProjectCollection collection = new();

        _ = ProjectInstance.FromProjectRootElement(
            CreateRootElement("<Project />"),
            new ProjectOptions { ProjectCollection = collection });

        collector.Measurements.ShouldContain(measurement =>
            measurement.InstrumentName == EvaluationMetrics.ProjectEvaluationCountName &&
            measurement.Value == 1 &&
            measurement.HasTag(EvaluationMetrics.StageTagName, "full") &&
            measurement.HasTag(EvaluationMetrics.OriginTagName, EvaluationMetrics.StandaloneOrigin) &&
            measurement.HasTag(EvaluationMetrics.SucceededTagName, true));

        collector.Measurements.ShouldContain(measurement =>
            measurement.InstrumentName == EvaluationMetrics.ProjectEvaluationDurationName &&
            measurement.Value >= 0 &&
            measurement.HasTag(EvaluationMetrics.StageTagName, "full") &&
            measurement.HasTag(EvaluationMetrics.OriginTagName, EvaluationMetrics.StandaloneOrigin) &&
            measurement.HasTag(EvaluationMetrics.SucceededTagName, true));
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
            measurement.HasTag(EvaluationMetrics.SucceededTagName, true));
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
            measurement.HasTag(EvaluationMetrics.OriginTagName, EvaluationMetrics.StandaloneOrigin) &&
            measurement.HasTag(EvaluationMetrics.SucceededTagName, false));
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
