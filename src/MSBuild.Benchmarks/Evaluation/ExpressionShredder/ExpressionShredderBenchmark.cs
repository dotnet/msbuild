// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Evaluation;

namespace MSBuild.Benchmarks;

/// <summary>
///  Benchmarks the three ExpressionShredder entry points used during evaluation.
/// </summary>
/// <remarks>
///  Global setup retains the shredded results so weakly interned fragments remain rooted and the
///  measured workload consistently represents steady-state interning.
/// </remarks>
[BenchmarkCategory("ExpressionShredder", "ExpressionShredderThroughput")]
[MemoryDiagnoser]
public class ExpressionShredderBenchmark
{
    private static readonly ExpressionShredderScenario s_plain = new(
        "Plain",
        "This is a plain string with no expansion tokens at all.");

    private static readonly ExpressionShredderScenario s_singleItem = new(
        "SingleItem",
        "@(Compile)");

    private static readonly ExpressionShredderScenario s_quotedTransform = new(
        "QuotedTransform",
        "@(Compile->'%(Filename).obj')");

    private static readonly ExpressionShredderScenario s_functionTransform = new(
        "FunctionTransform",
        "@(Compile->Distinct())");

    private static readonly ExpressionShredderScenario s_functionTransformWithArguments = new(
        "FunctionTransformWithArguments",
        "@(Compile->Substring(0, 4))");

    private static readonly ExpressionShredderScenario s_functionTransformWithQuotedArguments = new(
        "FunctionTransformWithQuotedArguments",
        "@(Compile->'%(Filename)'->Substring('()', $(Val), ')('))");

    private static readonly ExpressionShredderScenario s_multipleTransforms = new(
        "MultipleTransforms",
        "@(Compile->'%(Filename)'->Distinct()->Reverse())");

    private static readonly ExpressionShredderScenario s_transformWithSeparator = new(
        "TransformWithSeparator",
        "@(Compile, ';')");

    private static readonly ExpressionShredderScenario s_chainedFunctionsWithWhitespace = new(
        "ChainedFunctionsWithWhitespace",
        "@(Compile->Distinct() -> Reverse() ->Count())");

    private static readonly ExpressionShredderScenario s_unqualifiedMetadata = new(
        "UnqualifiedMetadata",
        "%(Culture)");

    private static readonly ExpressionShredderScenario s_qualifiedMetadata = new(
        "QualifiedMetadata",
        "%(Compile.Culture)");

    private static readonly ExpressionShredderScenario s_multipleMetadata = new(
        "MultipleMetadata",
        "%(Culture)_%(Generator)");

    private static readonly ExpressionShredderScenario s_mixed = new(
        "Mixed",
        @"$(OutputPath)\%(Culture)\@(Compile->'%(Filename)')");

    private static readonly ExpressionShredderScenario s_realistic = new(
        "Realistic",
        "@(_OutputPathItem->'%(FullPath)', ';');$(MSBuildAllProjects);" +
        "@(Compile);@(ManifestResourceWithNoCulture);$(ApplicationIcon);$(AssemblyOriginatorKeyFile);" +
        "@(ManifestNonResxWithNoCultureOnDisk);@(ReferencePath);@(CompiledLicenseFile);" +
        "@(EmbeddedDocumentation);$(Win32Resource);$(Win32Manifest);@(CustomAdditionalCompileInputs)");

    private static readonly ExpressionShredderScenario s_semicolonList = new(
        "SemicolonList",
        "@(Compile->'%(FullPath)', ';');$(A);$(B);value1;value2;" +
        "@(Reference);%(Culture);value3;@(Content->'%(Filename)', ';');value4");

    private readonly List<object> _warmCacheRoots = [];

    [GlobalSetup]
    public void GlobalSetup()
    {
        foreach (object value in NamesAndMetadataCases())
        {
            var scenario = (ExpressionShredderScenario)value;
            _warmCacheRoots.Add(ExpressionShredder.GetReferencedItemNamesAndMetadata(scenario.Expressions));
        }

        foreach (object value in ItemExpressionsCases())
        {
            var scenario = (ExpressionShredderScenario)value;
            ExpressionShredder.ReferencedItemExpressionsEnumerator enumerator =
                ExpressionShredder.GetReferencedItemExpressions(scenario.Expression);

            while (enumerator.MoveNext())
            {
                _warmCacheRoots.Add(enumerator.Current);
            }
        }

        foreach (object value in SplitCases())
        {
            var scenario = (ExpressionShredderScenario)value;

            foreach (string expression in ExpressionShredder.SplitSemiColonSeparatedList(scenario.Expression))
            {
                _warmCacheRoots.Add(expression);
            }
        }
    }

    public static IEnumerable<object> NamesAndMetadataCases()
    {
        yield return s_plain;
        yield return s_singleItem;
        yield return s_functionTransform;
        yield return s_functionTransformWithArguments;
        yield return s_multipleTransforms;
        yield return s_transformWithSeparator;
        yield return s_unqualifiedMetadata;
        yield return s_qualifiedMetadata;
        yield return s_multipleMetadata;
        yield return s_mixed;
        yield return s_realistic;
    }

    public static IEnumerable<object> ItemExpressionsCases()
    {
        yield return s_singleItem;
        yield return s_quotedTransform;
        yield return s_functionTransform;
        yield return s_functionTransformWithArguments;
        yield return s_functionTransformWithQuotedArguments;
        yield return s_multipleTransforms;
        yield return s_chainedFunctionsWithWhitespace;
        yield return s_realistic;
    }

    public static IEnumerable<object> SplitCases()
    {
        yield return s_realistic;
        yield return s_semicolonList;
    }

    [Benchmark]
    [ArgumentsSource(nameof(NamesAndMetadataCases))]
    public int NamesAndMetadata(ExpressionShredderScenario scenario)
    {
        ItemsAndMetadataPair pair = ExpressionShredder.GetReferencedItemNamesAndMetadata(scenario.Expressions);
        return (pair.Items?.Count ?? 0) + (pair.Metadata?.Count ?? 0);
    }

    [Benchmark]
    [ArgumentsSource(nameof(ItemExpressionsCases))]
    public int ItemExpressions(ExpressionShredderScenario scenario)
    {
        int count = 0;
        ExpressionShredder.ReferencedItemExpressionsEnumerator enumerator =
            ExpressionShredder.GetReferencedItemExpressions(scenario.Expression);

        while (enumerator.MoveNext())
        {
            count++;
        }

        return count;
    }

    [Benchmark]
    [ArgumentsSource(nameof(SplitCases))]
    public int Split(ExpressionShredderScenario scenario)
    {
        int count = 0;

        foreach (string _ in ExpressionShredder.SplitSemiColonSeparatedList(scenario.Expression))
        {
            count++;
        }

        return count;
    }
}

public sealed class ExpressionShredderScenario
{
    internal ExpressionShredderScenario(string name, string expression)
    {
        Name = name;
        Expression = expression;
        Expressions = [expression];
    }

    internal string Name { get; }

    internal string Expression { get; }

    internal string[] Expressions { get; }

    public override string ToString()
        => Name;
}
