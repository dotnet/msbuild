// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;

namespace MSBuild.Benchmarks;

/// <summary>
///  Measures current-state reads while collecting operations, rather than retaining bookmarks.
/// </summary>
/// <remarks>
///  Conditions request growing Include states or each updated state. Repeated-current reads make
///  100 requests within a fresh evaluation, complementing the setup-primed <see cref="LazyItemReadBenchmark"/>.
/// </remarks>
[BenchmarkCategory("Items", "ItemEvaluation")]
[MemoryDiagnoser]
public class LazyItemCurrentReadBenchmark
{
    public enum WorkloadKind
    {
        SparseGrowingReads,
        EveryGrowingRead,
        ReadAfterEveryUpdate,
        RepeatedCurrentReads,
    }

    private LazyItemBenchmarkFixture _fixture = null!;

    [Params(100, 1000)]
    public int ItemCount { get; set; }

    [ParamsAllValues]
    public WorkloadKind Workload { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _fixture = new LazyItemBenchmarkFixture(LazyItemBenchmarkScenario.CreateCurrentReads(Workload, ItemCount));
        _fixture.Validate();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
        => _fixture?.Dispose();

    [Benchmark]
    public int Evaluate()
        => _fixture.Evaluate();
}
