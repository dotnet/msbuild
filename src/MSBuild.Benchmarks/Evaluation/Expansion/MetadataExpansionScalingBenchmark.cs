// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using BenchmarkDotNet.Attributes;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace MSBuild.Benchmarks;

/// <summary>
///  Measures how metadata expansion scales with the number of distinct and repeated references.
/// </summary>
[BenchmarkCategory("Expansion", "MetadataExpansionScaling", "Scaling")]
[MemoryDiagnoser]
public class MetadataExpansionScalingBenchmark
{
    private const int MaximumReferenceCount = 16;

    private readonly string[] _distinctExpressions = new string[MaximumReferenceCount + 1];
    private readonly string[] _repeatedExpressions = new string[MaximumReferenceCount + 1];
    private ExpanderBenchmarkFixture _fixture = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        using ExpanderBuilder builder = new();

        for (int i = 0; i < MaximumReferenceCount; i++)
        {
            builder.AddMetadata($"Metadata{i}", $"Value{i}");
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

    private string Expand(string expression)
        => _fixture.Expander.ExpandIntoStringLeaveEscaped(
            expression,
            ExpanderOptions.ExpandMetadata,
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

            builder.Append("%(Metadata");
            builder.Append(repeated ? 0 : i);
            builder.Append(')');
        }

        return builder.ToString();
    }
}
