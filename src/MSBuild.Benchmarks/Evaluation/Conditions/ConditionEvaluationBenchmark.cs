// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared.FileSystem;
using static MSBuild.Benchmarks.ConditionStrings;

namespace MSBuild.Benchmarks;

/// <summary>
///  Benchmarks end-to-end condition evaluation with the production expression-tree cache warm.
/// </summary>
/// <remarks>
///  Global setup primes every condition so the measured cache state does not depend on the
///  BenchmarkDotNet job's warmup configuration.
/// </remarks>
[BenchmarkCategory("Conditions", "ConditionEvaluation")]
[MemoryDiagnoser]
public class ConditionEvaluationBenchmark
{
    private Expander<ProjectPropertyInstance, ProjectItemInstance> _expander = null!;
    private ExpanderBenchmarkFixture _fixture = null!;
    private ElementLocation _elementLocation = null!;
    private string _evaluationDirectory = null!;
    private TemporaryDirectory _temporaryDirectory = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _temporaryDirectory = new TemporaryDirectory(nameof(ConditionEvaluationBenchmark));
        _evaluationDirectory = _temporaryDirectory.DirectoryPath;

        string existingDirectoryRoot = Path.GetDirectoryName(_evaluationDirectory)!;
        string existingDirectoryLeaf = Path.GetFileName(_evaluationDirectory);

        using ExpanderBuilder builder = new(_temporaryDirectory.GetPath("benchmark.proj"));
        builder.AddProperty("Configuration", "Debug");
        builder.AddProperty("Platform", "AnyCPU");
        builder.AddProperty("TargetFramework", "net11.0");
        builder.AddProperty("TargetFrameworkIdentifier", ".NETCoreApp");
        builder.AddProperty("TargetFrameworkVersion", "11.0");
        builder.AddProperty("UseWindowsForms", "false");
        builder.AddProperty("BuildNumber", "42");
        builder.AddProperty("ErrorCount", "0");
        builder.AddProperty("OutputPath", Path.Combine(_evaluationDirectory, "bin") + Path.DirectorySeparatorChar);
        builder.AddProperty("IsPackable", "true");
        builder.AddProperty("GenerateDocumentationFile", "false");
        builder.AddProperty("RootNamespace", "MyApp");
        builder.AddProperty("AssemblyName", "MyApp");
        builder.AddProperty("A", "1");
        builder.AddProperty("B", "2");
        builder.AddProperty("C", "3");
        builder.AddProperty("D", "4");
        builder.AddProperty("MissingPath", Path.Combine(_evaluationDirectory, "missing"));
        builder.AddProperty("ExistingDirectoryRoot", existingDirectoryRoot);
        builder.AddProperty("ExistingDirectoryLeaf", existingDirectoryLeaf);
        builder.AddProperty("DirectorySeparator", Path.DirectorySeparatorChar.ToString());
        builder.AddProperty("MSBuildProjectDirectory", _evaluationDirectory, mayBeReserved: true);
        builder.AddItem("Compile", "Program.cs");
        builder.AddMetadata("Extension", ".cs");

        _fixture = builder.Build();
        _expander = _fixture.Expander;
        _elementLocation = ElementLocation.EmptyLocation;

        foreach (string condition in AllConditions)
        {
            _ = Evaluate(condition);
        }
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _fixture.Dispose();
        _temporaryDirectory.Dispose();
    }

    [Benchmark(Baseline = true)]
    public bool SimpleEquality_Evaluate()
        => Evaluate(SimpleEquality);

    [Benchmark]
    public bool EmptyCheck_Evaluate()
        => Evaluate(EmptyCheck);

    [Benchmark]
    public bool NonEmptyCheck_Evaluate()
        => Evaluate(NonEmptyCheck);

    [Benchmark]
    public bool NumericComparison_Evaluate()
        => Evaluate(NumericComparison);

    [Benchmark]
    public bool NumericLessThan_Evaluate()
        => Evaluate(NumericLessThan);

    [Benchmark]
    public bool BooleanAnd_Evaluate()
        => Evaluate(BooleanAnd);

    [Benchmark]
    public bool BooleanOr_Evaluate()
        => Evaluate(BooleanOr);

    [Benchmark]
    public bool Negation_Evaluate()
        => Evaluate(Negation);

    [Benchmark]
    public bool NegatedEquality_Evaluate()
        => Evaluate(NegatedEquality);

    [Benchmark]
    public bool Complex_Evaluate()
        => Evaluate(Complex);

    [Benchmark]
    public bool DeepNesting_Evaluate()
        => Evaluate(DeepNesting);

    [Benchmark]
    public bool MultipleAnds_Evaluate()
        => Evaluate(MultipleAnds);

    [Benchmark]
    public bool MixedAndOr_Evaluate()
        => Evaluate(MixedAndOr);

    [Benchmark]
    public bool ExistsCheck_Evaluate()
        => Evaluate(ExistsCheck);

    [Benchmark]
    public bool HasTrailingSlash_Evaluate()
        => Evaluate(HasTrailingSlashCheck);

    [Benchmark]
    public bool ExistsWithConcatenation_Evaluate()
        => Evaluate(ExistsWithConcatenation);

    [Benchmark]
    public bool ConcatenatedComparison_Evaluate()
        => Evaluate(ConcatenatedComparison);

    [Benchmark]
    public bool MultipleProperties_Evaluate()
        => Evaluate(MultipleProperties);

    [Benchmark]
    public bool BooleanLiteralTrue_Evaluate()
        => Evaluate(BooleanLiteralTrue);

    [Benchmark]
    public bool BooleanLiteralFalse_Evaluate()
        => Evaluate(BooleanLiteralFalse);

    [Benchmark]
    public bool BareBoolean_Evaluate()
        => Evaluate(BareBoolean);

    [Benchmark]
    public bool ItemListCondition_Evaluate()
        => Evaluate(ItemListCondition);

    [Benchmark]
    public bool MetadataCondition_Evaluate()
        => Evaluate(MetadataCondition);

    [Benchmark]
    public bool RealisticSdkCondition_Evaluate()
        => Evaluate(RealisticSdkCondition);

    [Benchmark]
    public bool RealisticMultiTargeting_Evaluate()
        => Evaluate(RealisticMultiTargeting);

    private bool Evaluate(string condition)
        => ConditionEvaluator.EvaluateCondition(
            condition,
            ParserOptions.AllowAll,
            _expander,
            ExpanderOptions.ExpandAll,
            _evaluationDirectory,
            _elementLocation,
            FileSystems.Default,
            loggingContext: null);
}
