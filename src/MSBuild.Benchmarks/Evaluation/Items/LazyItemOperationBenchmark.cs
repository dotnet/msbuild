// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Framework;

namespace MSBuild.Benchmarks;

/// <summary>
///  Measures declaration processing and materialization for individual item-operation workloads.
/// </summary>
/// <remarks>
///  Each invocation creates a fresh lazy evaluator over prebuilt declarations and initialized project data.
///  Project construction, XML parsing, and filesystem enumeration are excluded. Wildcards match existing
///  in-memory items in Update and Remove operations; Includes contain only literal paths, either grouped
///  into bulk declarations or declared individually.
/// </remarks>
[BenchmarkCategory("Items", "ItemEvaluation")]
[MemoryDiagnoser]
public class LazyItemOperationBenchmark
{
    public enum WorkloadKind
    {
        IncludeOnly,
        DisjointLiteralUpdates,
        OverlappingLiteralUpdates,
        SparseWildcardUpdates,
        DenseWildcardUpdates,
        IndexedLiteralRemoves,
        IndexedWildcardRemoves,
        IndexedDuplicatePathRemoves,
        IndividualIncludes,
        MetadataWildcardUpdate,
        MetadataRemove,
        BulkLiteralRemoves,
        BulkDuplicatePathRemoves,
    }

    private LazyItemBenchmarkFixture _fixture = null!;

    [Params(500, 5000)]
    public int ItemCount { get; set; }

    [ParamsAllValues]
    public WorkloadKind Workload { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        LazyItemBenchmarkScenario scenario = LazyItemBenchmarkScenario.CreateOperations(Workload, ItemCount);
        if (Workload is WorkloadKind.IndexedLiteralRemoves
                     or WorkloadKind.IndexedWildcardRemoves
                     or WorkloadKind.IndexedDuplicatePathRemoves
                     or WorkloadKind.BulkLiteralRemoves
                     or WorkloadKind.BulkDuplicatePathRemoves
            && Traits.Instance.DictionaryBasedItemRemoveThreshold > scenario.SourceCount)
        {
            throw new InvalidOperationException(
                "The indexed Remove workload requires MSBUILDDICTIONARYBASEDITEMREMOVETHRESHOLD to be unset or no greater than the final Source count.");
        }

        _fixture = new LazyItemBenchmarkFixture(scenario);
        _fixture.Validate();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
        => _fixture?.Dispose();

    [Benchmark]
    public int Evaluate()
        => _fixture.Evaluate();
}
