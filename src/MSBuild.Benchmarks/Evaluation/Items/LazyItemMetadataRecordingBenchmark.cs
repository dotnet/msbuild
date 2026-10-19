// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;

namespace MSBuild.Benchmarks;

/// <summary>
///  Measures Include and Update recording with zero, one, or several metadata declarations.
/// </summary>
/// <remarks>
///  One metadata entry is constant. Four entries include item-vector and per-item metadata references;
///  their values contribute to the validated Trace value. No condition reads materialize items during recording.
/// </remarks>
[BenchmarkCategory("Items", "ItemEvaluation", "Scaling")]
[MemoryDiagnoser]
public class LazyItemMetadataRecordingBenchmark
{
    private LazyItemBenchmarkFixture _fixture = null!;

    [Params(100, 10000)]
    public int OperationCount { get; set; }

    [Params(0, 1, 4)]
    public int MetadataCount { get; set; }

    [Params(false, true)]
    public bool Updates { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _fixture = new(LazyItemBenchmarkScenario.CreateMetadataRecording(Updates, OperationCount, MetadataCount));
        _fixture.Validate();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
        => _fixture?.Dispose();

    [Benchmark]
    public object Record()
        => _fixture.Record();
}
