// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;

namespace MSBuild.Benchmarks;

/// <summary>
///  Measures glob planning and removal against a bounded tree with a setup-warmed file cache.
/// </summary>
/// <remarks>
///  Every invocation creates a fresh evaluator. Files and parsed declarations are prepared outside
///  timing. The bookmark workload evaluates and collects all files from the original complete item state.
/// </remarks>
[BenchmarkCategory("Items", "ItemEvaluation")]
[MemoryDiagnoser]
public class LazyItemGlobBenchmark
{
    public enum WorkloadKind
    {
        IncludeOnly,
        RemoveAll,
        SubtreeRemove,
        ManyRemoves,
        Reinclude,
        EarlyRead,
        ManyRemovesAfterRead,
        ManyRemovesAfterBookmark,
    }

    private LazyItemBenchmarkFixture _fixture = null!;

    [Params(100, 500)]
    public int FileCount { get; set; }

    [ParamsAllValues]
    public WorkloadKind Workload { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _fixture = new LazyItemBenchmarkFixture(LazyItemBenchmarkScenario.CreateGlobs(Workload, FileCount));
        _fixture.Validate();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
        => _fixture?.Dispose();

    [Benchmark]
    public int Evaluate()
        => _fixture.Evaluate();
}
