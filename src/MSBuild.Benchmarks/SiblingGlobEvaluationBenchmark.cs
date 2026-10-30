// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace MSBuild.Benchmarks;

/// <summary>
/// Compares sibling-glob evaluation with Wave 18.13 disabled and enabled in the same binary.
/// Measures fusion together with its frontier scheduling; Wave 18.11 remains enabled in both cases.
/// </summary>
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SiblingGlobEvaluationBenchmark
{
    private string _root = null!;
    private string _projectPath = null!;
    private EvaluationContext? _sharedContext;
    private const int PreparedEvaluationCount = 16;
    private EvaluationContext[] _iterationContexts = [];
    private bool _broadFirst;
    private string? _originalDisabledWave;
    private string? _originalLegacyCulture;

    [Params(32, 128)]
    public int DirectoryCount { get; set; }

    [Params(8, 64)]
    public int NonMatchingFilesPerDirectory { get; set; }

    [GlobalSetup(Target = nameof(BeforeIsolated))]
    public void SetupBeforeIsolated() => Setup(enableFusion: false, shared: false);

    [GlobalSetup(Target = nameof(AfterIsolated))]
    public void SetupAfterIsolated() => Setup(enableFusion: true, shared: false);

    [GlobalSetup(Target = nameof(BeforeShared))]
    public void SetupBeforeShared() => Setup(enableFusion: false, shared: true);

    [GlobalSetup(Target = nameof(AfterShared))]
    public void SetupAfterShared() => Setup(enableFusion: true, shared: true);

    [GlobalSetup(Target = nameof(BeforeWarmDirectories))]
    public void SetupBeforeWarmDirectories() => Setup(enableFusion: false, shared: false);

    [GlobalSetup(Target = nameof(AfterWarmDirectories))]
    public void SetupAfterWarmDirectories() => Setup(enableFusion: true, shared: false);

    [GlobalSetup(Target = nameof(BeforePartiallyWarm))]
    public void SetupBeforePartiallyWarm() => Setup(enableFusion: false, shared: false, broadFirst: true);

    [GlobalSetup(Target = nameof(AfterPartiallyWarm))]
    public void SetupAfterPartiallyWarm() => Setup(enableFusion: true, shared: false, broadFirst: true);

    [GlobalSetup(Target = nameof(BeforeBroadIsolated))]
    public void SetupBeforeBroadIsolated() => Setup(enableFusion: false, shared: false, broadFirst: true);

    [GlobalSetup(Target = nameof(AfterBroadIsolated))]
    public void SetupAfterBroadIsolated() => Setup(enableFusion: true, shared: false, broadFirst: true);

    [IterationSetup(Targets = [nameof(BeforeWarmDirectories), nameof(AfterWarmDirectories), nameof(BeforePartiallyWarm), nameof(AfterPartiallyWarm)])]
    public void PrepareIterationContexts()
    {
        _iterationContexts = new EvaluationContext[PreparedEvaluationCount];
        for (int index = 0; index < _iterationContexts.Length; index++)
        {
            EvaluationContext context = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared);
            context.FileMatcher.GetFiles(_root, "**/*.benchmark-warmup", ["obj/**"]);
            if (_broadFirst)
            {
                context.FileMatcher.GetFiles(_root, "**/*", ["obj/**"]);
                if (index == 0 && context.FileMatcher.GetFiles(_root, "**/*", ["obj/**"]).Action != FileMatcher.SearchAction.None)
                {
                    throw new InvalidOperationException("The broad sibling must be a whole-glob cache hit before measurement.");
                }
            }

            _iterationContexts[index] = context;
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Isolated")]
    public int BeforeIsolated() => Evaluate(evaluationContext: null);

    [Benchmark]
    [BenchmarkCategory("Isolated")]
    public int AfterIsolated() => Evaluate(evaluationContext: null);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Shared")]
    public int BeforeShared() => Evaluate(_sharedContext);

    [Benchmark]
    [BenchmarkCategory("Shared")]
    public int AfterShared() => Evaluate(_sharedContext);

    [Benchmark(Baseline = true, OperationsPerInvoke = PreparedEvaluationCount)]
    [BenchmarkCategory("WarmDirectories")]
    public int BeforeWarmDirectories() => EvaluatePreparedContexts();

    [Benchmark(OperationsPerInvoke = PreparedEvaluationCount)]
    [BenchmarkCategory("WarmDirectories")]
    public int AfterWarmDirectories() => EvaluatePreparedContexts();

    [Benchmark(Baseline = true, OperationsPerInvoke = PreparedEvaluationCount)]
    [BenchmarkCategory("PartiallyWarm")]
    public int BeforePartiallyWarm() => EvaluatePreparedContexts();

    [Benchmark(OperationsPerInvoke = PreparedEvaluationCount)]
    [BenchmarkCategory("PartiallyWarm")]
    public int AfterPartiallyWarm() => EvaluatePreparedContexts();

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("BroadIsolated")]
    public int BeforeBroadIsolated() => Evaluate(evaluationContext: null);

    [Benchmark]
    [BenchmarkCategory("BroadIsolated")]
    public int AfterBroadIsolated() => Evaluate(evaluationContext: null);

    [GlobalCleanup]
    public void Cleanup()
    {
        Environment.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", _originalDisabledWave);
        Environment.SetEnvironmentVariable("MSBUILDUSELEGACYCULTURESENSITIVEFILEGLOBS", _originalLegacyCulture);
        ChangeWaves.ResetStateForTests();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private void Setup(bool enableFusion, bool shared, bool broadFirst = false)
    {
        _broadFirst = broadFirst;
        _originalDisabledWave = Environment.GetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION");
        _originalLegacyCulture = Environment.GetEnvironmentVariable("MSBUILDUSELEGACYCULTURESENSITIVEFILEGLOBS");
        Environment.SetEnvironmentVariable("MSBUILDUSELEGACYCULTURESENSITIVEFILEGLOBS", null);

        _root = Path.Combine(Path.GetTempPath(), nameof(SiblingGlobEvaluationBenchmark), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        CreateTree();
        _projectPath = Path.Combine(_root, "evaluation.proj");
        string includes = broadFirst ? "**/*;Assets/**/*.props" : "Assets/**;**/*.props;**/*.targets";
        File.WriteAllText(_projectPath, $$"""
            <Project>
              <ItemGroup>
                <None Include="{{includes}}" Exclude="obj/**" />
              </ItemGroup>
            </Project>
            """);

        (string Include, string RecursiveDir)[] expected =
        [
            (Normalize("Assets/logo.svg"), ""),
            (Normalize("Assets/styles/theme.props"), Normalize("styles/")),
            (Normalize("Assets/styles/theme.props"), Normalize("Assets/styles/")),
            (Normalize("build/Common.targets"), Normalize("build/"))
        ];

        SetFusionEnabled(false);
        var before = ReadItems(evaluationContext: null);
        if (broadFirst)
        {
            expected = before;
            if (expected.Length != DirectoryCount * NonMatchingFilesPerDirectory + 5)
            {
                throw new InvalidOperationException("The broad sibling must include the tree and duplicate the Assets props match.");
            }
        }

        SetFusionEnabled(true);
        var after = ReadItems(evaluationContext: null);
        if (!before.AsSpan().SequenceEqual(expected) || !after.AsSpan().SequenceEqual(expected))
        {
            throw new InvalidOperationException("Sibling-glob benchmark item order, duplicates, identities or RecursiveDir differ.");
        }

        SetFusionEnabled(enableFusion);
        if (shared)
        {
            _sharedContext = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared);
            if (!ReadItems(_sharedContext).AsSpan().SequenceEqual(expected))
            {
                throw new InvalidOperationException("Shared-context sibling-glob benchmark results differ.");
            }
        }
    }

    private static void SetFusionEnabled(bool enabled)
    {
        Environment.SetEnvironmentVariable(
            "MSBUILDDISABLEFEATURESFROMVERSION",
            enabled ? null : ChangeWaves.Wave18_13.ToString());
        ChangeWaves.ResetStateForTests();
        if (!ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave18_11)
            || ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave18_13) != enabled)
        {
            throw new InvalidOperationException("The benchmark must toggle only Wave 18.13 while retaining Wave 18.11.");
        }
    }

    private int Evaluate(EvaluationContext? evaluationContext)
    {
        using ProjectCollection collection = new();
        return CreateProject(collection, evaluationContext).GetItems("None").Count;
    }

    private int EvaluatePreparedContexts()
    {
        int count = 0;
        foreach (EvaluationContext context in _iterationContexts)
        {
            count += Evaluate(context);
        }

        return count;
    }

    private (string Include, string RecursiveDir)[] ReadItems(EvaluationContext? evaluationContext)
    {
        using ProjectCollection collection = new();
        return CreateProject(collection, evaluationContext).GetItems("None")
            .Select(item => (item.EvaluatedInclude, item.GetMetadataValue("RecursiveDir")))
            .ToArray();
    }

    private Project CreateProject(ProjectCollection collection, EvaluationContext? evaluationContext) =>
        Project.FromFile(_projectPath, new ProjectOptions
        {
            ProjectCollection = collection,
            EvaluationContext = evaluationContext
        });

    private void CreateTree()
    {
        string[] fixedFiles =
        [
            "Assets/logo.svg",
            "Assets/styles/theme.props",
            "build/Common.targets",
            "obj/generated.props",
            "obj/generated.targets"
        ];
        foreach (string relativePath in fixedFiles)
        {
            string path = Path.Combine(_root, Normalize(relativePath));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, string.Empty);
        }

        for (int directoryIndex = 0; directoryIndex < DirectoryCount; directoryIndex++)
        {
            string directory = Path.Combine(_root, "src", $"group{directoryIndex:D3}", "nested");
            Directory.CreateDirectory(directory);
            for (int fileIndex = 0; fileIndex < NonMatchingFilesPerDirectory; fileIndex++)
            {
                File.WriteAllText(Path.Combine(directory, $"content{fileIndex:D3}.txt"), string.Empty);
            }
        }
    }

    private static string Normalize(string path) => path.Replace('/', Path.DirectorySeparatorChar);
}
