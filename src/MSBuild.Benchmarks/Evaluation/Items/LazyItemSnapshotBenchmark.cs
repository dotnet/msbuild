// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;

namespace MSBuild.Benchmarks;

/// <summary>
///  Measures appends and metadata edits with different numbers of bookmarked item states.
/// </summary>
/// <remarks>
///  Each bookmark selects one tagged item, keeping output copying small while retaining a computed
///  item state. Bookmarks are evaluated after the final state. This exercises earlier-position
///  reads rather than repeatedly reading the final state from the public Project object.
/// </remarks>
[BenchmarkCategory("Items", "ItemEvaluation")]
[MemoryDiagnoser]
public class LazyItemSnapshotBenchmark
{
    public enum WorkloadKind
    {
        Append,
        SparseUpdates,
        DenseUpdates,
    }

    private LazyItemBenchmarkFixture _fixture = null!;

    [Params(500, 5000)]
    public int ItemCount { get; set; }

    [Params(0, 4, 32)]
    public int BookmarkCount { get; set; }

    [ParamsAllValues]
    public WorkloadKind Workload { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _fixture = new LazyItemBenchmarkFixture(LazyItemBenchmarkScenario.CreateSnapshots(Workload, ItemCount, BookmarkCount));
        _fixture.Validate();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
        => _fixture?.Dispose();

    [Benchmark]
    public int Evaluate()
        => _fixture.Evaluate();
}
