// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using BenchmarkDotNet.Attributes;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

namespace MSBuild.Benchmarks;

/// <summary>
/// Measures the cost of evaluating a project only as far as needed for the
/// <c>-getProperty</c>/<c>-getItem</c> command-line scenario (no targets), comparing a full
/// evaluation against the opt-in partial (stop-after-pass) evaluation exposed via
/// <see cref="ProjectOptions.EvaluationStage"/>.
/// </summary>
/// <remarks>
/// Each invocation mirrors the CLI path in <c>XMake.cs</c>: a fresh <see cref="ProjectCollection"/>
/// is created, the project is loaded via <see cref="ProjectInstance.FromFile(string, ProjectOptions)"/>, and
/// a single property value is read. The project XML is written to disk once in
/// <see cref="GlobalSetup"/> and re-parsed on every invocation (as the CLI does), so the reported
/// delta reflects the evaluation passes that partial evaluation skips, not parsing.
/// </remarks>
[MemoryDiagnoser]
public class PartialEvaluationBenchmark
{
    /// <summary>
    /// Number of <c>&lt;Target&gt;</c> elements to register during the final evaluation pass. This is
    /// the pass that a properties-only evaluation skips entirely. A restored <c>dotnet new console</c>
    /// (net10.0) evaluates ~500 targets, so 506 reflects a realistic SDK-style project.
    /// </summary>
    [Params(506)]
    public int TargetCount { get; set; }

    private const int PropertyCount = 200;
    private const int ItemCount = 500;
    private const int ItemDefinitionCount = 30;
    private const int UsingTaskCount = 30;

    private string _projectPath = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        StringBuilder sb = new();
        sb.AppendLine("<Project>");

        // Properties chained so each references the previous one, forcing real expansion in pass 1.
        sb.AppendLine("  <PropertyGroup>");
        sb.AppendLine("    <Prop0>Value0</Prop0>");
        for (int i = 1; i < PropertyCount; i++)
        {
            sb.AppendLine($"    <Prop{i}>$(Prop{i - 1})-{i}</Prop{i}>");
        }

        sb.AppendLine($"    <RequestedProperty>$(Prop{PropertyCount - 1})</RequestedProperty>");
        sb.AppendLine("  </PropertyGroup>");

        // Item definitions (pass 2).
        sb.AppendLine("  <ItemDefinitionGroup>");
        for (int i = 0; i < ItemDefinitionCount; i++)
        {
            sb.AppendLine($"    <Item{i}><Kind>source</Kind><Order>{i}</Order></Item{i}>");
        }

        sb.AppendLine("  </ItemDefinitionGroup>");

        // Items (pass 3).
        sb.AppendLine("  <ItemGroup>");
        for (int i = 0; i < ItemCount; i++)
        {
            sb.AppendLine($"    <Compile Include=\"src/dir{i % 10}/File{i}.cs\"><Culture>en-US</Culture></Compile>");
        }

        sb.AppendLine("  </ItemGroup>");

        // Using-tasks (pass 4). Assemblies are never loaded during evaluation, so the names need not resolve.
        for (int i = 0; i < UsingTaskCount; i++)
        {
            sb.AppendLine($"  <UsingTask TaskName=\"Task{i}\" AssemblyName=\"Some.Assembly{i}, Version=1.0.0.0\" />");
        }

        // Targets (pass 5) - the bulk of a full evaluation for property-only queries.
        for (int i = 0; i < TargetCount; i++)
        {
            sb.AppendLine($"  <Target Name=\"Target{i}\" Condition=\" '$(Prop0)' == 'Value0' \"><Message Text=\"In Target{i}\" /></Target>");
        }

        sb.AppendLine("</Project>");

        _projectPath = Path.Combine(Path.GetTempPath(), $"partial-eval-bench-{Guid.NewGuid():N}.proj");
        File.WriteAllText(_projectPath, sb.ToString());
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        if (_projectPath is not null && File.Exists(_projectPath))
        {
            File.Delete(_projectPath);
        }
    }

    private string EvaluateAndReadProperty(ProjectEvaluationStage stage)
    {
        // A fresh collection per invocation mirrors the CLI and avoids cross-iteration caching so the
        // project XML is re-parsed and re-evaluated every time, exactly as `msbuild -getProperty` does.
        using ProjectCollection collection = new();
        ProjectInstance project = ProjectInstance.FromFile(_projectPath, new ProjectOptions
        {
            ProjectCollection = collection,
            EvaluationStage = stage,
        });

        return project.GetPropertyValue("RequestedProperty");
    }

    private string EvaluateAndReadItems(ProjectEvaluationStage stage)
    {
        using ProjectCollection collection = new();
        ProjectInstance project = ProjectInstance.FromFile(_projectPath, new ProjectOptions
        {
            ProjectCollection = collection,
            EvaluationStage = stage,
        });

        // Touch both a property and the items, mirroring `-getProperty ... -getItem Compile`.
        string value = project.GetPropertyValue("RequestedProperty");
        _ = project.GetItems("Compile").Count;
        return value;
    }

    /// <summary>
    /// Full evaluation - what <c>-getProperty</c>/<c>-getItem</c> did before the partial-evaluation change.
    /// </summary>
    [Benchmark(Baseline = true)]
    public string Full_ReadProperty() => EvaluateAndReadProperty(ProjectEvaluationStage.Full);

    /// <summary>
    /// <c>-getProperty</c> only: stop after the Properties pass.
    /// </summary>
    [Benchmark]
    public string Partial_Properties_ReadProperty() => EvaluateAndReadProperty(ProjectEvaluationStage.Properties);

    /// <summary>
    /// <c>-getProperty -getItem</c>: stop after the Items pass.
    /// </summary>
    [Benchmark]
    public string Partial_Items_ReadPropertyAndItems() => EvaluateAndReadItems(ProjectEvaluationStage.Items);
}
