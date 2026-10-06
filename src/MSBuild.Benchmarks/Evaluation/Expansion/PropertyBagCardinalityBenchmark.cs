// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace MSBuild.Benchmarks;

/// <summary>
///  Measures property expansion while varying the number of unrelated properties in the property bag.
/// </summary>
/// <remarks>
///  The expression shapes and referenced properties remain fixed so parameter differences isolate
///  property-bag cardinality. The setup matches the small and large bags from the original expander
///  benchmark.
/// </remarks>
[BenchmarkCategory("Expansion", "PropertyExpansionScaling", "PropertyBagCardinality", "Scaling")]
[MemoryDiagnoser]
public class PropertyBagCardinalityBenchmark
{
    private const string Literal = "prefix-suffix";
    private const string SingleProperty = "$(Configuration)";
    private const string MultipleProperties = @"$(Configuration)\$(Platform)\$(OutputPath)";
    private const string AdjacentProperties = "$(RootNamespace).$(AssemblyName)";
    private const string ConcatenatedProperties =
        "prefix_$(Configuration)_$(Platform)_$(TargetFramework)_suffix";

    private ExpanderBenchmarkFixture _fixture = null!;

    /// <summary>
    ///  Gets or sets the number of unreferenced properties added before the six referenced properties.
    /// </summary>
    [Params(10, 100)]
    public int UnusedPropertyCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        using ExpanderBuilder builder = new();

        for (int i = 0; i < UnusedPropertyCount; i++)
        {
            builder.AddProperty($"Prop{i}", $"Value{i}");
        }

        builder.AddProperty("Configuration", "Release");
        builder.AddProperty("Platform", "AnyCPU");
        builder.AddProperty("OutputPath", @"bin\Release\net11.0");
        builder.AddProperty("RootNamespace", "MyProject.Core");
        builder.AddProperty("AssemblyName", "MyProject.Core");
        builder.AddProperty("TargetFramework", "net11.0");

        _fixture = builder.Build();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
        => _fixture.Dispose();

    [Benchmark(Baseline = true)]
    public string NoExpansion()
        => Expand(Literal);

    [Benchmark]
    public string Single()
        => Expand(SingleProperty);

    [Benchmark]
    public string Multiple()
        => Expand(MultipleProperties);

    [Benchmark]
    public string Adjacent()
        => Expand(AdjacentProperties);

    [Benchmark]
    public string Concatenation()
        => Expand(ConcatenatedProperties);

    private string Expand(string expression)
        => _fixture.Expander.ExpandIntoStringLeaveEscaped(
            expression,
            ExpanderOptions.ExpandProperties,
            ElementLocation.EmptyLocation);
}
