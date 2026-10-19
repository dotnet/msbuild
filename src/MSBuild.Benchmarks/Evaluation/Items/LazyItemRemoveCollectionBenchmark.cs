// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;

namespace MSBuild.Benchmarks;

/// <summary>
///  Compares collecting literal and glob Removes without applying either kind of operation.
/// </summary>
[BenchmarkCategory("Items", "ItemEvaluation", "Scaling")]
[MemoryDiagnoser]
public class LazyItemRemoveCollectionBenchmark
{
    private LazyItemBenchmarkFixture _fixture = null!;

    [Params(100, 10000)]
    public int OperationCount { get; set; }

    [Params(false, true)]
    public bool GlobRemoves { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _fixture = new(LazyItemBenchmarkScenario.CreateRemoveCollection(GlobRemoves, OperationCount));
        _fixture.Validate();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
        => _fixture?.Dispose();

    [Benchmark]
    public object CollectOperations()
        => _fixture.CollectOperations();
}
