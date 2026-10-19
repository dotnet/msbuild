// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;

namespace MSBuild.Benchmarks;

/// <summary>
///  Measures stored and expanded exclusions independently of the fixed 100-item literal Include.
/// </summary>
[BenchmarkCategory("Items", "ItemEvaluation")]
[MemoryDiagnoser]
public class LazyItemExcludeBenchmark
{
    private LazyItemBenchmarkFixture _fixture = null!;

    [Params(0, 4, 128, 16384)]
    public int ExcludeCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _fixture = new LazyItemBenchmarkFixture(LazyItemBenchmarkScenario.CreateExcludes(itemCount: 100, ExcludeCount));
        _fixture.Validate();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
        => _fixture?.Dispose();

    [Benchmark]
    public int Evaluate()
        => _fixture.Evaluate();
}
