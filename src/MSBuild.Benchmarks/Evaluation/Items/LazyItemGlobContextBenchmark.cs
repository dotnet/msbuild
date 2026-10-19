// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;

namespace MSBuild.Benchmarks;

/// <summary>
///  Measures repeated equivalent or growing exclusion contexts over a fixed filesystem tree.
/// </summary>
/// <remarks>
///  Each Remove is followed by a current-state read or a deferred one-item capture. Equivalent contexts
///  reorder and duplicate the same nonmatching patterns; growing contexts introduce a new pattern each time.
/// </remarks>
[BenchmarkCategory("Items", "ItemEvaluation")]
[MemoryDiagnoser]
public class LazyItemGlobContextBenchmark
{
    public const int FileCount = 100;

    public enum WorkloadKind
    {
        EquivalentAfterRead,
        GrowingAfterRead,
        EquivalentAfterCapture,
        GrowingAfterCapture,
    }

    private LazyItemBenchmarkFixture _fixture = null!;

    [Params(4, 128)]
    public int RemoveCount { get; set; }

    [ParamsAllValues]
    public WorkloadKind Workload { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _fixture = new(LazyItemBenchmarkScenario.CreateGlobContexts(Workload, RemoveCount));
        _fixture.Validate();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
        => _fixture?.Dispose();

    [Benchmark]
    public int Evaluate()
        => _fixture.Evaluate();
}
