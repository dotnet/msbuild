// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using BenchmarkDotNet.Attributes;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace MSBuild.Benchmarks;

/// <summary>
///  Measures property-reference scaling and representative multi-reference composition shapes.
/// </summary>
[BenchmarkCategory("Expansion", "PropertyExpansionScaling", "Scaling")]
[MemoryDiagnoser]
public class PropertyExpansionScalingBenchmark
{
    private const int MaximumReferenceCount = 16;

    private readonly string[] _distinctExpressions = new string[MaximumReferenceCount + 1];
    private readonly string[] _repeatedExpressions = new string[MaximumReferenceCount + 1];
    private ExpanderBenchmarkFixture _fixture = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        using ExpanderBuilder builder = new();
        builder.AddProperty("Repeated", "Value");

        for (int i = 0; i < MaximumReferenceCount; i++)
        {
            builder.AddProperty($"Property{i}", $"Value{i}");
        }

        _fixture = builder.Build();

        foreach (int referenceCount in ReferenceCounts())
        {
            _distinctExpressions[referenceCount] = CreateExpression(referenceCount, repeated: false);
            _repeatedExpressions[referenceCount] = CreateExpression(referenceCount, repeated: true);
        }
    }

    [GlobalCleanup]
    public void GlobalCleanup()
        => _fixture.Dispose();

    public static IEnumerable<int> ReferenceCounts()
        => [1, 2, 4, 8, 16];

    [Benchmark]
    [ArgumentsSource(nameof(ReferenceCounts))]
    public string MultipleDistinct(int referenceCount)
        => Expand(_distinctExpressions[referenceCount]);

    [Benchmark]
    [ArgumentsSource(nameof(ReferenceCounts))]
    public string MultipleRepeated(int referenceCount)
        => Expand(_repeatedExpressions[referenceCount]);

    [Benchmark]
    public string Adjacent()
        => Expand("$(Property0).$(Property1)");

    [Benchmark]
    public string EmbeddedMultiple()
        => Expand("prefix_$(Property0)_$(Property1)_$(Property2)_suffix");

    private string Expand(string expression)
        => _fixture.Expander.ExpandIntoStringLeaveEscaped(
            expression,
            ExpanderOptions.ExpandProperties,
            ElementLocation.EmptyLocation);

    private static string CreateExpression(int referenceCount, bool repeated)
    {
        var builder = new StringBuilder();

        for (int i = 0; i < referenceCount; i++)
        {
            if (i > 0)
            {
                builder.Append('|');
            }

            builder.Append("$(");
            builder.Append(repeated ? "Repeated" : $"Property{i}");
            builder.Append(')');
        }

        return builder.ToString();
    }
}
