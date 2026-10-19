// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;

namespace MSBuild.Benchmarks;

/// <summary>
///  Measures repeated item collection and filtered-item reads from a primed evaluator.
/// </summary>
/// <remarks>
///  Setup creates the evaluator and cached snapshots. Filtered-item reads use an item-group
///  Count condition, which requests the evaluator's current items on both the historical baseline and HEAD.
///  They do not call Project.GetItems on an already evaluated project's unrelated final item collection.
/// </remarks>
[BenchmarkCategory("Items", "ItemEvaluation")]
[MemoryDiagnoser]
public class LazyItemReadBenchmark
{
    private LazyItemBenchmarkFixture _fixture = null!;

    [Params(500, 5000)]
    public int ItemCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _fixture = new LazyItemBenchmarkFixture(
            LazyItemBenchmarkScenario.CreateOperations(LazyItemOperationBenchmark.WorkloadKind.IncludeOnly, ItemCount));
        _fixture.Validate();
        _fixture.PrepareCachedReads();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
        => _fixture?.Dispose();

    [Benchmark]
    public int CollectItems()
        => _fixture.CollectCachedItems();

    [Benchmark]
    public void ReadFilteredItems()
        => _fixture.ReadFilteredItems();
}
