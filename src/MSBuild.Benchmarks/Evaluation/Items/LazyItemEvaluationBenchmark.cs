// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using BenchmarkDotNet.Attributes;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

namespace MSBuild.Benchmarks;

/// <summary>
///  Measures end-to-end project evaluation for isolated and combined lazy item operations.
/// </summary>
/// <remarks>
///  Each invocation deliberately creates and disposes a project collection, then loads and evaluates
///  a project from disk. This measures the full cold path, including project collection construction,
///  XML parsing, and common evaluation overhead. Files are created during global setup and are expected
///  to be served from the operating system's filesystem cache during measurement.
/// </remarks>
[BenchmarkCategory("Items", "ItemEvaluation")]
[MemoryDiagnoser]
public class LazyItemEvaluationBenchmark
{
    private string _wildcardProjectPath = null!;
    private string _semicolonListProjectPath = null!;
    private string _updateProjectPath = null!;
    private string _removeProjectPath = null!;
    private string _combinedProjectPath = null!;
    private TemporaryDirectory _temporaryDirectory = null!;

    [Params(500)]
    public int FileCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _temporaryDirectory = new TemporaryDirectory(nameof(LazyItemEvaluationBenchmark));

        for (int i = 0; i < FileCount; i++)
        {
            _temporaryDirectory.WriteFile(
                Path.Combine("src", $"dir{i % 10}", $"File{i}.cs"),
                string.Empty);
        }

        string semicolonList = CreateSemicolonList(itemCount: 40);

        _wildcardProjectPath = WriteProject(
            "wildcard.proj",
            """
              <ItemGroup>
                <Compile Include="src\**\*.cs" />
              </ItemGroup>
            """);

        _semicolonListProjectPath = WriteProject(
            "semicolon-list.proj",
            $"""
              <ItemGroup>
                <None Include="{semicolonList}" />
              </ItemGroup>
            """);

        _updateProjectPath = WriteProject(
            "update.proj",
            """
              <ItemGroup>
                <Compile Include="src\**\*.cs" />
                <Compile Update="src\dir1\**\*.cs">
                  <Culture>fr-FR</Culture>
                  <Generator>ResX</Generator>
                </Compile>
              </ItemGroup>
            """);

        _removeProjectPath = WriteProject(
            "remove.proj",
            """
              <ItemGroup>
                <Compile Include="src\**\*.cs" />
                <Compile Remove="src\dir9\**\*.cs" />
              </ItemGroup>
            """);

        _combinedProjectPath = WriteProject(
            "combined.proj",
            $"""
              <ItemGroup>
                <Compile Include="src\**\*.cs">
                  <Culture>en-US</Culture>
                </Compile>
                <None Include="{semicolonList}" />
                <Compile Update="src\dir1\**\*.cs">
                  <Culture>fr-FR</Culture>
                  <Generator>ResX</Generator>
                </Compile>
                <Compile Remove="src\dir9\**\*.cs" />
              </ItemGroup>
            """);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
        => _temporaryDirectory.Dispose();

    [Benchmark(Baseline = true)]
    public int WildcardInclude()
        => EvaluateItems(_wildcardProjectPath);

    [Benchmark]
    public int SemicolonListInclude()
        => EvaluateItems(_semicolonListProjectPath);

    [Benchmark]
    public int MetadataUpdate()
        => EvaluateItems(_updateProjectPath);

    [Benchmark]
    public int Remove()
        => EvaluateItems(_removeProjectPath);

    [Benchmark]
    public int Combined()
        => EvaluateItems(_combinedProjectPath);

    private int EvaluateItems(string projectPath)
    {
        using ProjectCollection collection = new();
        ProjectInstance project = ProjectInstance.FromFile(projectPath, new ProjectOptions
        {
            ProjectCollection = collection,
        });

        return project.GetItems("Compile").Count + project.GetItems("None").Count;
    }

    private string WriteProject(string fileName, string itemGroups)
        => _temporaryDirectory.WriteFile(
            fileName,
            $"""
            <Project>
            {itemGroups}
            </Project>
            """);

    private static string CreateSemicolonList(int itemCount)
    {
        var builder = new StringBuilder();

        for (int i = 0; i < itemCount; i++)
        {
            if (i > 0)
            {
                builder.Append(';');
            }

            builder.Append("item");
            builder.Append(i);
            builder.Append(".txt");
        }

        return builder.ToString();
    }
}
