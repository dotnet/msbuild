// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Framework;

namespace MSBuild.Benchmarks;

/// <summary>
/// Measures observation-layer overhead in isolated and shared evaluation contexts.
/// Filesystem validation is measured separately by <see cref="EvaluationInputValidationBenchmark"/>.
/// </summary>
[MemoryDiagnoser]
public class EvaluationInputRecordingBenchmark
{
    private const string ModeVariable = "MSBUILDEVALUATIONCACHEMODE";
    private const string RecordingVariable = "MSBUILDRECORDEVALUATIONINPUTS";
    private const string SnapshotVariable = "MSBUILDENABLEPROJECTINSTANCESNAPSHOTCACHE";

    private readonly string? _originalMode = Environment.GetEnvironmentVariable(ModeVariable);
    private readonly string? _originalRecording = Environment.GetEnvironmentVariable(RecordingVariable);
    private readonly string? _originalSnapshot = Environment.GetEnvironmentVariable(SnapshotVariable);
    private EvaluationInputBenchmarkFixture _fixture = null!;
    private EvaluationContext _sharedContext = null!;

    [ParamsSource(nameof(ProjectPaths))]
    public string ProjectPath { get; set; } = EvaluationInputBenchmarkFixture.SyntheticProject;

    public static IEnumerable<string> ProjectPaths => EvaluationInputBenchmarkFixture.ProjectPaths;

    [GlobalSetup(Targets = [nameof(Evaluate), nameof(EvaluateSharedContext)])]
    public void SetupWithoutRecording() => Setup(record: false, mode: null);

    [GlobalSetup(Targets = [nameof(EvaluateExplicitDisabled), nameof(EvaluateExplicitDisabledSharedContext)])]
    public void SetupExplicitDisabled() => Setup(record: false, mode: "Disabled");

    [GlobalSetup(Targets = [nameof(EvaluateRecording), nameof(EvaluateRecordingSharedContext)])]
    public void SetupWithRecording() => Setup(record: true, mode: null);

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        Environment.SetEnvironmentVariable(ModeVariable, _originalMode);
        Environment.SetEnvironmentVariable(RecordingVariable, _originalRecording);
        Environment.SetEnvironmentVariable(SnapshotVariable, _originalSnapshot);
        Traits.UpdateFromEnvironment();
        _fixture?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public int Evaluate() => _fixture.EvaluateProject().GetItems("Compile").Count;

    [Benchmark]
    public int EvaluateRecording() => _fixture.EvaluateProject().GetItems("Compile").Count;

    /// <summary>The ordinary isolated evaluation path with the controlling mode explicitly disabled.</summary>
    [Benchmark]
    public int EvaluateExplicitDisabled() => _fixture.EvaluateProject().GetItems("Compile").Count;

    /// <summary>Evaluation through one shared <see cref="EvaluationContext"/>, whose glob and SDK caches stay warm across evaluations as in a graph build.</summary>
    [Benchmark]
    public int EvaluateSharedContext() => _fixture.EvaluateProject(_sharedContext).GetItems("Compile").Count;

    /// <summary>The shared-context evaluation with recording: cached glob expansions are reused and the directories they depend on are replayed to the recorder.</summary>
    [Benchmark]
    public int EvaluateRecordingSharedContext() => _fixture.EvaluateProject(_sharedContext).GetItems("Compile").Count;

    /// <summary>The shared-context evaluation path with the controlling mode explicitly disabled.</summary>
    [Benchmark]
    public int EvaluateExplicitDisabledSharedContext() => _fixture.EvaluateProject(_sharedContext).GetItems("Compile").Count;

    private void Setup(bool record, string? mode)
    {
        Environment.SetEnvironmentVariable(ModeVariable, null);
        Environment.SetEnvironmentVariable(RecordingVariable, null);
        Environment.SetEnvironmentVariable(SnapshotVariable, null);
        Traits.UpdateFromEnvironment();
        _fixture = new EvaluationInputBenchmarkFixture(ProjectPath);
        _sharedContext = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared);
        _fixture.RecordInputs();
        Environment.SetEnvironmentVariable(ModeVariable, mode);
        EvaluationInputBenchmarkFixture.SetRecording(record);
    }
}
