// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Framework;

namespace MSBuild.Benchmarks;

/// <summary>
///  Varies qualified-Update source and target counts independently to expose multiplicative matching costs.
/// </summary>
[BenchmarkCategory("Items", "ItemEvaluation")]
[MemoryDiagnoser]
public class LazyItemQualifiedUpdateBenchmark
{
    private LazyItemBenchmarkFixture _fixture = null!;

    [Params(100, 1000)]
    public int SourceCount { get; set; }

    [Params(100, 1000)]
    public int TargetCount { get; set; }

    [GlobalSetup(Target = nameof(Evaluate))]
    public void GlobalSetup()
        => Setup(wildcardSources: false);

    [GlobalSetup(Target = nameof(EvaluateWildcardSources))]
    public void GlobalSetupWildcardSources()
        => Setup(wildcardSources: true);

    private void Setup(bool wildcardSources)
    {
        if (Traits.Instance.EscapeHatches.DoNotExpandQualifiedMetadataInUpdateOperation)
        {
            throw new InvalidOperationException(
                "The qualified Update workload requires MSBuildDoNotExpandQualifiedMetadataInUpdateOperation to be unset.");
        }

        _fixture = new LazyItemBenchmarkFixture(LazyItemBenchmarkScenario.CreateQualifiedUpdate(SourceCount, TargetCount, wildcardSources));
        _fixture.Validate();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
        => _fixture?.Dispose();

    [Benchmark]
    public int Evaluate()
        => _fixture.Evaluate();

    [Benchmark]
    public int EvaluateWildcardSources()
        => _fixture.Evaluate();
}
