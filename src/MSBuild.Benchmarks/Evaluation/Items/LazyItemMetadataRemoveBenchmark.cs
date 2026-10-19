// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;

namespace MSBuild.Benchmarks;

/// <summary>
///  Measures construction and lookup of three-value metadata tuples with different leading-value sharing.
/// </summary>
/// <remarks>
///  The target count is fixed at 512, with half matching and half nonmatching tuples. Source count varies
///  independently. Metadata-name order is unchanged between the shared-first and distinct-first cases.
/// </remarks>
[BenchmarkCategory("Items", "ItemEvaluation")]
[MemoryDiagnoser]
public class LazyItemMetadataRemoveBenchmark
{
    private LazyItemBenchmarkFixture _fixture = null!;

    [Params(16, 128)]
    public int SourceCount { get; set; }

    [Params(false, true)]
    public bool SharedFirstValue { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _fixture = new(LazyItemBenchmarkScenario.CreateMetadataRemove(SourceCount, SharedFirstValue));
        _fixture.Validate();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
        => _fixture?.Dispose();

    [Benchmark]
    public int Evaluate()
        => _fixture.Evaluate();
}
