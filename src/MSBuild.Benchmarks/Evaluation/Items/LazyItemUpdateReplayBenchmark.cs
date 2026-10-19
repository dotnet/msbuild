// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;

namespace MSBuild.Benchmarks;

/// <summary>
///  Measures multi-fragment Update admission and repeated application of the same recorded operations.
/// </summary>
/// <remarks>
///  Zero replays measures one final computation. Each additional nonmatching glob Remove and condition
///  read demands a different exclusion context, preventing a cached final result from hiding replay work.
/// </remarks>
[BenchmarkCategory("Items", "ItemEvaluation")]
[MemoryDiagnoser]
public class LazyItemUpdateReplayBenchmark
{
    public enum WorkloadKind
    {
        Disjoint,
        LateOverlap,
        DuplicateNormalizedKeys,
    }

    private LazyItemBenchmarkFixture _fixture = null!;

    [Params(500, 5000)]
    public int ItemCount { get; set; }

    [Params(0, 8)]
    public int ReplayCount { get; set; }

    [ParamsAllValues]
    public WorkloadKind Workload { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _fixture = new(LazyItemBenchmarkScenario.CreateUpdateReplay(Workload, ItemCount, ReplayCount));
        _fixture.Validate();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
        => _fixture?.Dispose();

    [Benchmark]
    public int Evaluate()
        => _fixture.Evaluate();
}
