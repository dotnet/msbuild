// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;

namespace MSBuild.Benchmarks;

/// <summary>
///  Measures append and metadata-edit workloads with different numbers of retained historical prefixes.
/// </summary>
/// <remarks>
///  Each captured reference selects one tagged item, keeping output copying small while retaining an entire
///  source prefix. All captures are materialized after the final source state. This exercises deferred history
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
    public int ReferenceCount { get; set; }

    [ParamsAllValues]
    public WorkloadKind Workload { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _fixture = new LazyItemBenchmarkFixture(LazyItemBenchmarkScenario.CreateSnapshots(Workload, ItemCount, ReferenceCount));
        _fixture.Validate();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
        => _fixture?.Dispose();

    [Benchmark]
    public int Evaluate()
        => _fixture.Evaluate();
}
