// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;

namespace MSBuild.Benchmarks;

/// <summary>
///  Measures collecting Includes and Updates with zero, one, or several metadata declarations.
/// </summary>
/// <remarks>
///  One metadata entry is constant. Four entries include item-vector and per-item metadata references;
///  their values contribute to the validated Trace value. Collection does not demand items via conditions.
/// </remarks>
[BenchmarkCategory("Items", "ItemEvaluation", "Scaling")]
[MemoryDiagnoser]
public class LazyItemMetadataCollectionBenchmark
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
        _fixture = new(LazyItemBenchmarkScenario.CreateMetadataCollection(Updates, OperationCount, MetadataCount));
        _fixture.Validate();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
        => _fixture?.Dispose();

    [Benchmark]
    public object CollectOperations()
        => _fixture.CollectOperations();
}
