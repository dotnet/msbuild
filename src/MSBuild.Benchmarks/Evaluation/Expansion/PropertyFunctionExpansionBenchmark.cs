// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace MSBuild.Benchmarks;

/// <summary>
///  Benchmarks property-function parsing, overload binding, invocation, and result conversion.
/// </summary>
[BenchmarkCategory("Expansion", "PropertyExpansion", "PropertyFunctions")]
[MemoryDiagnoser]
public class PropertyFunctionExpansionBenchmark
{
    private const string PropertyBaseline = "$(Text)";
    private const string StaticMethod = "$([System.Math]::Max(123, 456))";
    private const string StaticMethodWithPropertyArguments = "$([System.String]::Concat('$(Prefix)', '$(Suffix)'))";
    private const string StaticPathFunctionExpression = @"$([System.IO.Path]::Combine('$(OutputPath)', 'app.dll'))";
    private const string InstanceMethod = "$(Text.ToUpperInvariant())";
    private const string InstanceSubstringExpression = "$(Text.Substring(7))";
    private const string InstanceReplaceExpression = "$(Text.Replace('-', '_'))";
    private const string InstanceWithPropertyArgumentExpression = "$(Text.Replace('$(Prefix)', 'value-'))";
    private const string ChainedInstanceMethods = "$(Text.Substring(7).ToUpperInvariant())";
    private const string NestedFunctions = "$([System.String]::Concat($([System.String]::Concat('prefix', '-')), 'suffix'))";
    private const string IntrinsicFunction = "$([MSBuild]::ValueOrDefault('$(Undefined)', 'fallback'))";
    private const string IntrinsicAddExpression = "$([MSBuild]::Add(40, 2))";
    private const string MultipleFunctions = "$([System.Math]::Max(123, 456))|$([System.Math]::Min(123, 456))";
    private ExpanderBenchmarkFixture _fixture = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        using ExpanderBuilder builder = new();
        builder.AddProperty("Text", "prefix-value");
        builder.AddProperty("Prefix", "prefix-");
        builder.AddProperty("Suffix", "suffix");
        builder.AddProperty("OutputPath", @"bin\Release\net11.0");
        _fixture = builder.Build();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
        => _fixture.Dispose();

    [Benchmark(Baseline = true)]
    public string Property()
        => Expand(PropertyBaseline);

    [Benchmark]
    public string Static()
        => Expand(StaticMethod);

    [Benchmark]
    public string StaticWithPropertyArguments()
        => Expand(StaticMethodWithPropertyArguments);

    [Benchmark]
    public string StaticPath()
        => Expand(StaticPathFunctionExpression);

    [Benchmark]
    public string Instance()
        => Expand(InstanceMethod);

    [Benchmark]
    public string InstanceWithArgument()
        => Expand(InstanceSubstringExpression);

    [Benchmark]
    public string InstanceWithOverload()
        => Expand(InstanceReplaceExpression);

    [Benchmark]
    public string InstanceWithPropertyArgument()
        => Expand(InstanceWithPropertyArgumentExpression);

    [Benchmark]
    public string ChainedInstance()
        => Expand(ChainedInstanceMethods);

    [Benchmark]
    public string Nested()
        => Expand(NestedFunctions);

    [Benchmark]
    public string Intrinsic()
        => Expand(IntrinsicFunction);

    [Benchmark]
    public string IntrinsicArithmetic()
        => Expand(IntrinsicAddExpression);

    [Benchmark]
    public string Multiple()
        => Expand(MultipleFunctions);

    private string Expand(string expression)
        => _fixture.Expander.ExpandIntoStringLeaveEscaped(
            expression,
            ExpanderOptions.ExpandProperties,
            ElementLocation.EmptyLocation);
}
