// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;

namespace MSBuild.Benchmarks;

/// <summary>
///  Measures multi-fragment Update admission and re-evaluation of the same collected operations.
/// </summary>
/// <remarks>
///  A zero re-evaluation count measures one computation. Each nonmatching glob Remove and condition
///  read demands a different later Remove glob patterns, preventing a cached final result from hiding reevaluation work.
/// </remarks>
[BenchmarkCategory("Items", "ItemEvaluation")]
[MemoryDiagnoser]
public class LazyItemUpdateReevaluationBenchmark
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
    public int ReevaluationCount { get; set; }

    [ParamsAllValues]
    public WorkloadKind Workload { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _fixture = new(LazyItemBenchmarkScenario.CreateUpdateReevaluation(Workload, ItemCount, ReevaluationCount));
        _fixture.Validate();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
        => _fixture?.Dispose();

    [Benchmark]
    public int Evaluate()
        => _fixture.Evaluate();
}
