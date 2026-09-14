// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;

namespace MSBuild.Benchmarks;

/// <summary>
///  Benchmarks expressions that cross property, item, and metadata expansion pipelines.
/// </summary>
/// <remarks>
///  Built-in metadata is primed during setup so transforms measure steady-state expansion rather
///  than first-use item-spec modifier computation.
/// </remarks>
[BenchmarkCategory("Expansion", "MixedExpansion")]
[MemoryDiagnoser]
public class MixedExpansionBenchmark
{
    private const int ItemCount = 100;
    private const string Literal = "This is a plain string with no expansion tokens.";
    private const string PropertyAndItemExpression = @"$(OutputPath)\@(Compile->'%(Filename)')";
    private const string PropertyAndMetadataExpression = @"$(OutputPath)\%(Culture)\%(Identity)";
    private const string AllExpression = @"$(OutputPath)\%(Culture)\@(Compile->'%(Filename)')";

    private ExpanderBenchmarkFixture _fixture = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        using ExpanderBuilder builder = new();
        builder.AddProperty("OutputPath", @"bin\Release\net11.0");
        builder.AddMetadata("Culture", "en-US");
        builder.AddMetadata("Identity", @"src\SomeFile.cs");

        for (int i = 0; i < ItemCount; i++)
        {
            ProjectItemInstance item = builder.AddItem("Compile", $@"src\dir{i % 10}\File{i}.cs");
            _ = item.GetMetadataValue(ItemSpecModifiers.Filename);
        }

        _fixture = builder.Build();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
        => _fixture.Dispose();

    [Benchmark(Baseline = true)]
    public string NoExpansion()
        => _fixture.Expander.ExpandIntoStringLeaveEscaped(
            Literal,
            ExpanderOptions.ExpandAll,
            ElementLocation.EmptyLocation);

    [Benchmark]
    public string PropertyAndItem()
        => _fixture.Expander.ExpandIntoStringLeaveEscaped(
            PropertyAndItemExpression,
            ExpanderOptions.ExpandPropertiesAndItems,
            ElementLocation.EmptyLocation);

    [Benchmark]
    public string PropertyAndMetadata()
        => _fixture.Expander.ExpandIntoStringLeaveEscaped(
            PropertyAndMetadataExpression,
            ExpanderOptions.ExpandPropertiesAndMetadata,
            ElementLocation.EmptyLocation);

    [Benchmark]
    public string All()
        => _fixture.Expander.ExpandIntoStringLeaveEscaped(
            AllExpression,
            ExpanderOptions.ExpandAll,
            ElementLocation.EmptyLocation);

    [Benchmark]
    public string AllAndUnescape()
        => _fixture.Expander.ExpandIntoStringAndUnescape(
            AllExpression,
            ExpanderOptions.ExpandAll,
            ElementLocation.EmptyLocation);
}
