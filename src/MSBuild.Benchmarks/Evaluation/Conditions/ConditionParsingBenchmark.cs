// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using static MSBuild.Benchmarks.ConditionStrings;

namespace MSBuild.Benchmarks;

/// <summary>
///  Benchmarks condition scanning, parsing, and expression-tree construction without evaluation.
/// </summary>
/// <remarks>
///  Each invocation creates a parser directly, bypassing the production expression-tree cache, so
///  cache state from <see cref="ConditionEvaluationBenchmark"/> does not affect these measurements.
/// </remarks>
[BenchmarkCategory("Conditions", "ConditionParsing")]
[MemoryDiagnoser]
public class ConditionParsingBenchmark
{
    private ElementLocation _elementLocation = null!;

    [GlobalSetup]
    public void GlobalSetup()
        => _elementLocation = ElementLocation.EmptyLocation;

    [Benchmark(Baseline = true)]
    public object SimpleEquality_Parse()
        => Parse(SimpleEquality);

    [Benchmark]
    public object EmptyCheck_Parse()
        => Parse(EmptyCheck);

    [Benchmark]
    public object NonEmptyCheck_Parse()
        => Parse(NonEmptyCheck);

    [Benchmark]
    public object NumericComparison_Parse()
        => Parse(NumericComparison);

    [Benchmark]
    public object NumericLessThan_Parse()
        => Parse(NumericLessThan);

    [Benchmark]
    public object BooleanAnd_Parse()
        => Parse(BooleanAnd);

    [Benchmark]
    public object BooleanOr_Parse()
        => Parse(BooleanOr);

    [Benchmark]
    public object Negation_Parse()
        => Parse(Negation);

    [Benchmark]
    public object NegatedEquality_Parse()
        => Parse(NegatedEquality);

    [Benchmark]
    public object Complex_Parse()
        => Parse(Complex);

    [Benchmark]
    public object DeepNesting_Parse()
        => Parse(DeepNesting);

    [Benchmark]
    public object MultipleAnds_Parse()
        => Parse(MultipleAnds);

    [Benchmark]
    public object MixedAndOr_Parse()
        => Parse(MixedAndOr);

    [Benchmark]
    public object ExistsCheck_Parse()
        => Parse(ExistsCheck);

    [Benchmark]
    public object HasTrailingSlash_Parse()
        => Parse(HasTrailingSlashCheck);

    [Benchmark]
    public object ExistsWithConcatenation_Parse()
        => Parse(ExistsWithConcatenation);

    [Benchmark]
    public object ConcatenatedComparison_Parse()
        => Parse(ConcatenatedComparison);

    [Benchmark]
    public object MultipleProperties_Parse()
        => Parse(MultipleProperties);

    [Benchmark]
    public object BooleanLiteralTrue_Parse()
        => Parse(BooleanLiteralTrue);

    [Benchmark]
    public object BooleanLiteralFalse_Parse()
        => Parse(BooleanLiteralFalse);

    [Benchmark]
    public object BareBoolean_Parse()
        => Parse(BareBoolean);

    [Benchmark]
    public object ItemListCondition_Parse()
        => Parse(ItemListCondition);

    [Benchmark]
    public object MetadataCondition_Parse()
        => Parse(MetadataCondition);

    [Benchmark]
    public object RealisticSdkCondition_Parse()
        => Parse(RealisticSdkCondition);

    [Benchmark]
    public object RealisticMultiTargeting_Parse()
        => Parse(RealisticMultiTargeting);

    private object Parse(string condition)
        => new Parser().Parse(condition, ParserOptions.AllowAll, _elementLocation);
}
