// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;

namespace MSBuild.Benchmarks;

/// <summary>
///  Measures large operation histories, separating recording from final materialization.
/// </summary>
/// <remarks>
///  No workload reads conditions or materializes captured sources during recording. Update workloads
///  begin with a bulk Include of <see cref="OperationCount"/> items followed by that many Updates.
/// </remarks>
[BenchmarkCategory("Items", "ItemEvaluation", "Scaling")]
[MemoryDiagnoser]
public class LazyItemHistoryBenchmark
{
    public enum WorkloadKind
    {
        IndividualIncludes,
        InterleavedItemTypes,
        DisjointLiteralUpdates,
        OverlappingLiteralUpdates,
    }

    private LazyItemBenchmarkFixture _fixture = null!;

    [Params(100, 10000)]
    public int OperationCount { get; set; }

    [ParamsAllValues]
    public WorkloadKind Workload { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _fixture = new LazyItemBenchmarkFixture(LazyItemBenchmarkScenario.CreateHistory(Workload, OperationCount));
        _fixture.Validate();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
        => _fixture?.Dispose();

    [Benchmark]
    public object Record()
        => _fixture.Record();

    [Benchmark]
    public int Evaluate()
        => _fixture.Evaluate();
}
