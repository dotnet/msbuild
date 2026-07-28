// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Execution;

namespace MSBuild.Benchmarks.Analysis;

/// <summary>
/// Measures a <em>full</em> evaluation of a restored <c>dotnet new console</c> project, which is what every real
/// build, <c>ProjectGraph</c> construction, and Visual Studio design-time load performs.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="PartialEvaluationBenchmark"/> covers the synthetic <c>-getProperty</c>/<c>-getItem</c> scenario.
/// This benchmark complements it by using a real SDK project so that import loading, SDK resolution, and the
/// default item globs are all represented.
/// </para>
/// <para>
/// The <c>Cold</c> benchmarks create a fresh <see cref="ProjectCollection"/> and an isolated
/// <see cref="EvaluationContext"/> per invocation, so project XML, SDK resolution results, and file existence
/// results are never reused. The <c>Warm</c> benchmarks share both across invocations.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class FullEvaluationBenchmark
{
    private ConsoleAppFixture _fixture = null!;
    private ProjectCollection _warmCollection = null!;
    private EvaluationContext _warmContext = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        MSBuildEnvironment.Ensure();
        _fixture = ConsoleAppFixture.Create();

        _warmCollection = new ProjectCollection();
        _warmContext = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared);

        // Populate the shared caches so the warm benchmarks measure steady state rather than first load.
        _ = EvaluateWarm(ProjectEvaluationStage.Full);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _warmCollection?.Dispose();
        _fixture?.Dispose();
    }

    private ProjectInstance EvaluateCold(ProjectEvaluationStage stage)
    {
        using ProjectCollection collection = new();
        return ProjectInstance.FromFile(_fixture.ProjectFile, new ProjectOptions
        {
            ProjectCollection = collection,
            EvaluationStage = stage,
            EvaluationContext = EvaluationContext.Create(EvaluationContext.SharingPolicy.Isolated),
        });
    }

    private ProjectInstance EvaluateWarm(ProjectEvaluationStage stage)
        => ProjectInstance.FromFile(_fixture.ProjectFile, new ProjectOptions
        {
            ProjectCollection = _warmCollection,
            EvaluationStage = stage,
            EvaluationContext = _warmContext,
        });

    /// <summary>Everything a build needs: all passes, no cache reuse.</summary>
    [Benchmark(Baseline = true)]
    public ProjectInstance Cold_Full() => EvaluateCold(ProjectEvaluationStage.Full);

    /// <summary>Stops after the items pass, skipping using-task and target registration.</summary>
    [Benchmark]
    public ProjectInstance Cold_Items() => EvaluateCold(ProjectEvaluationStage.Items);

    /// <summary>Stops after the properties pass, skipping item globbing and everything after it.</summary>
    [Benchmark]
    public ProjectInstance Cold_Properties() => EvaluateCold(ProjectEvaluationStage.Properties);

    /// <summary>Full evaluation with the project XML, SDK resolution, and file existence caches already populated.</summary>
    [Benchmark]
    public ProjectInstance Warm_Full() => EvaluateWarm(ProjectEvaluationStage.Full);

    /// <summary>Properties-only evaluation against warm caches.</summary>
    [Benchmark]
    public ProjectInstance Warm_Properties() => EvaluateWarm(ProjectEvaluationStage.Properties);
}
