// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;

namespace MSBuild.Benchmarks;

/// <summary>
///  Benchmarks item-function parsing, argument handling, and chained transform execution.
/// </summary>
/// <remarks>
///  Built-in metadata is primed during setup so transforms measure steady-state expansion rather
///  than first-use item-spec modifier computation.
/// </remarks>
[BenchmarkCategory("Expansion", "ItemExpansion", "ItemFunctions")]
[MemoryDiagnoser]
public class ItemFunctionExpansionBenchmark
{
    private const string FunctionTransform = "@(Compile->Distinct())";
    private const string FunctionTransformWithArguments = "@(Compile->WithMetadataValue('Culture', 'en-US'))";
    private const string StringFunctionTransform = "@(Compile->'%(Filename)'->Substring(0, 3))";
    private const string ChainedTransforms = "@(Compile->Distinct()->Reverse())";

    private ExpanderBenchmarkFixture _fixture = null!;

    [Params(10, 100)]
    public int ItemCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        using ExpanderBuilder builder = new();

        for (int i = 0; i < ItemCount; i++)
        {
            ProjectItemInstance item = builder.AddItem("Compile", $@"src\dir{i % 10}\File{i}.cs");
            item.SetMetadata("Culture", i % 2 == 0 ? "en-US" : "fr-FR");
            _ = item.GetMetadataValue(ItemSpecModifiers.Filename);
        }

        _fixture = builder.Build();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
        => _fixture.Dispose();

    [Benchmark(Baseline = true)]
    public string Function()
        => Expand(FunctionTransform);

    [Benchmark]
    public string FunctionWithArguments()
        => Expand(FunctionTransformWithArguments);

    [Benchmark]
    public string StringFunction()
        => Expand(StringFunctionTransform);

    [Benchmark]
    public string ChainedFunctions()
        => Expand(ChainedTransforms);

    private string Expand(string expression)
        => _fixture.Expander.ExpandIntoStringLeaveEscaped(
            expression,
            ExpanderOptions.ExpandItems,
            ElementLocation.EmptyLocation);
}
