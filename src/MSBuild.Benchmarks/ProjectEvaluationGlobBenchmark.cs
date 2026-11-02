// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Framework;

namespace MSBuild.Benchmarks;

/// <summary>
/// Measures complete project evaluation for SDK-style item globs with default excludes.
/// </summary>
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class ProjectEvaluationGlobBenchmark
{
    private const int ExpectedCompileItemsPerDirectory = 2;

    private string _projectPath = null!;
    private EvaluationContext _sharedContext = null!;

    [Params(32, 128)]
    public int DirectoryCount { get; set; }

    [Params(8, 64)]
    public int NonMatchingFilesPerDirectory { get; set; }

    [GlobalSetup(Target = nameof(LegacyIsolatedEvaluation))]
    public void SetupLegacyIsolated() => Setup(useOptimizedFileMatcher: false, createSharedContext: false);

    [GlobalSetup(Target = nameof(OptimizedIsolatedEvaluation))]
    public void SetupOptimizedIsolated() => Setup(useOptimizedFileMatcher: true, createSharedContext: false);

    [GlobalSetup(Target = nameof(LegacySharedEvaluation))]
    public void SetupLegacyShared() => Setup(useOptimizedFileMatcher: false, createSharedContext: true);

    [GlobalSetup(Target = nameof(OptimizedSharedEvaluation))]
    public void SetupOptimizedShared() => Setup(useOptimizedFileMatcher: true, createSharedContext: true);

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        string? root = _projectPath is null ? null : Path.GetDirectoryName(_projectPath);
        if (root is not null && Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Isolated")]
    public int LegacyIsolatedEvaluation() => Evaluate(evaluationContext: null);

    [Benchmark]
    [BenchmarkCategory("Isolated")]
    public int OptimizedIsolatedEvaluation() => Evaluate(evaluationContext: null);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Shared")]
    public int LegacySharedEvaluation() => Evaluate(_sharedContext);

    [Benchmark]
    [BenchmarkCategory("Shared")]
    public int OptimizedSharedEvaluation() => Evaluate(_sharedContext);

    private void Setup(bool useOptimizedFileMatcher, bool createSharedContext)
    {
        Environment.SetEnvironmentVariable(
            "MSBUILDDISABLEFEATURESFROMVERSION",
            useOptimizedFileMatcher ? null : ChangeWaves.Wave18_11.ToString());
        Environment.SetEnvironmentVariable("MSBUILDUSELEGACYCULTURESENSITIVEFILEGLOBS", null);
        ChangeWaves.ResetStateForTests();

        string root = Path.Combine(
            Path.GetTempPath(),
            nameof(ProjectEvaluationGlobBenchmark),
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        CreateTree(root);
        _projectPath = Path.Combine(root, "evaluation.proj");
        File.WriteAllText(_projectPath, CreateProjectXml());

        if (createSharedContext)
        {
            _sharedContext = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared);
        }

        ValidateExpectedCompileItems(createSharedContext ? _sharedContext : null);
    }

    private void ValidateExpectedCompileItems(EvaluationContext? evaluationContext)
    {
        using ProjectCollection collection = new();
        Project project = Project.FromFile(_projectPath, new ProjectOptions
        {
            ProjectCollection = collection,
            EvaluationContext = evaluationContext,
        });

        string[] actual = project.GetItems("Compile")
            .Select(item => item.EvaluatedInclude)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        string[] expected = new string[DirectoryCount * ExpectedCompileItemsPerDirectory];
        int expectedIndex = 0;
        for (int directoryIndex = 0; directoryIndex < DirectoryCount; directoryIndex++)
        {
            string relativeDirectory = Path.Combine("src", $"group{directoryIndex:D3}", "nested");
            expected[expectedIndex++] = Path.Combine(relativeDirectory, "first.cs");
            expected[expectedIndex++] = Path.Combine(relativeDirectory, "second.cs");
        }

        Array.Sort(expected, StringComparer.Ordinal);
        if (!actual.AsSpan().SequenceEqual(expected))
        {
            throw new InvalidOperationException(
                $"Evaluation benchmark item identities differ: expected={expected.Length}, actual={actual.Length}.");
        }
    }

    private int Evaluate(EvaluationContext? evaluationContext)
    {
        using ProjectCollection collection = new();
        Project project = Project.FromFile(_projectPath, new ProjectOptions
        {
            ProjectCollection = collection,
            EvaluationContext = evaluationContext,
        });

        return project.GetItems("Compile").Count;
    }

    private void CreateTree(string root)
    {
        for (int directoryIndex = 0; directoryIndex < DirectoryCount; directoryIndex++)
        {
            string directory = Path.Combine(root, "src", $"group{directoryIndex:D3}", "nested");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "first.cs"), string.Empty);
            File.WriteAllText(Path.Combine(directory, "second.cs"), string.Empty);

            for (int fileIndex = 0; fileIndex < NonMatchingFilesPerDirectory; fileIndex++)
            {
                File.WriteAllText(Path.Combine(directory, $"content{fileIndex:D3}.txt"), string.Empty);
            }

            string excludedDirectory = Path.Combine(directory, directoryIndex % 2 == 0 ? "obj" : "bin");
            Directory.CreateDirectory(excludedDirectory);
            File.WriteAllText(Path.Combine(excludedDirectory, "generated.cs"), string.Empty);
        }
    }

    private static string CreateProjectXml()
    {
        StringBuilder project = new();
        project.AppendLine("<Project>");
        project.AppendLine("  <PropertyGroup>");
        project.AppendLine("    <DefaultItemExcludes>bin/**;obj/**;**/bin/**;**/obj/**;**/*.user;**/*.*proj;**/*.sln;**/*.slnx;**/*.vssscc;**/.DS_Store</DefaultItemExcludes>");
        project.AppendLine("  </PropertyGroup>");
        project.AppendLine("  <ItemGroup>");
        project.AppendLine("    <Compile Include=\"src/**/*.cs\" Exclude=\"$(DefaultItemExcludes)\" />");
        project.AppendLine("  </ItemGroup>");
        project.AppendLine("</Project>");
        return project.ToString();
    }
}