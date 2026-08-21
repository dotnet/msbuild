// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;

namespace MSBuild.Benchmarks;

/// <summary>
///  Benchmarks item-vector expansion and transform execution.
/// </summary>
/// <remarks>
///  Built-in metadata is primed during setup so transforms measure steady-state expansion rather
///  than first-use item-spec modifier computation.
/// </remarks>
[BenchmarkCategory("Expansion", "ItemExpansion")]
[MemoryDiagnoser]
public class ItemExpansionBenchmark
{
    private const string Literal = "This is a plain string with no item expansion tokens.";
    private const string SimpleItemList = "@(Compile)";
    private const string QuotedTransform = "@(Compile->'%(Filename).obj')";
    private const string ItemListWithSeparator = "@(Compile, ',')";
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
            item.SetMetadata("Link", $@"linked\File{i}.cs");
            item.SetMetadata("Generator", "ResXFileCodeGenerator");
            _ = item.GetMetadataValue(ItemSpecModifiers.Filename);
        }

        _fixture = builder.Build();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
        => _fixture.Dispose();

    [Benchmark(Baseline = true)]
    public string NoExpansion()
        => Expand(Literal);

    [Benchmark]
    public string Simple()
        => Expand(SimpleItemList);

    [Benchmark]
    public string WithQuotedTransform()
        => Expand(QuotedTransform);

    [Benchmark]
    public string WithSeparator()
        => Expand(ItemListWithSeparator);

    [Benchmark]
    public string WithQuotedTransformAndUnescape()
        => _fixture.Expander.ExpandIntoStringAndUnescape(
            QuotedTransform,
            ExpanderOptions.ExpandItems,
            ElementLocation.EmptyLocation);

    private string Expand(string expression)
        => _fixture.Expander.ExpandIntoStringLeaveEscaped(
            expression,
            ExpanderOptions.ExpandItems,
            ElementLocation.EmptyLocation);
}
