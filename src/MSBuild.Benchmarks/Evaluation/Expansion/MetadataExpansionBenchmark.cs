// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace MSBuild.Benchmarks;

/// <summary>
///  Benchmarks unqualified and qualified metadata expansion without unrelated item-count parameters.
/// </summary>
[BenchmarkCategory("Expansion", "MetadataExpansion")]
[MemoryDiagnoser]
public class MetadataExpansionBenchmark
{
    private const string Literal = "This is a plain string with no metadata expansion tokens.";
    private const string UnqualifiedMetadata = "%(Culture)";
    private const string QualifiedMetadata = "%(Compile.Link)";
    private const string MultipleMetadata = "%(Culture)_%(Generator)";

    private ExpanderBenchmarkFixture _fixture = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        using ExpanderBuilder builder = new();
        builder.AddMetadata("Culture", "en-US");
        builder.AddMetadata("Generator", "ResXFileCodeGenerator");
        builder.AddMetadata("Compile.Link", @"linked\SomeFile.cs");
        _fixture = builder.Build();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
        => _fixture.Dispose();

    [Benchmark(Baseline = true)]
    public string NoExpansion()
        => Expand(Literal);

    [Benchmark]
    public string Unqualified()
        => Expand(UnqualifiedMetadata);

    [Benchmark]
    public string Qualified()
        => Expand(QualifiedMetadata);

    [Benchmark]
    public string Multiple()
        => Expand(MultipleMetadata);

    [Benchmark]
    public string MultipleAndUnescape()
        => _fixture.Expander.ExpandIntoStringAndUnescape(
            MultipleMetadata,
            ExpanderOptions.ExpandMetadata,
            ElementLocation.EmptyLocation);

    private string Expand(string expression)
        => _fixture.Expander.ExpandIntoStringLeaveEscaped(
            expression,
            ExpanderOptions.ExpandMetadata,
            ElementLocation.EmptyLocation);
}
