// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using System.Xml;
using BenchmarkDotNet.Attributes;
using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

namespace MSBuild.Benchmarks;

/// <summary>
/// Measures item evaluation with metadata that is shared across all items produced by an item operation.
/// </summary>
/// <remarks>
/// The invariant cases exercise the operation-local evaluated-metadata store and both final item factories.
/// The fallback cases contain a metadata reference, which intentionally routes through per-item expansion
/// instead; they are negative controls for changes isolated to the invariant path.
/// </remarks>
[BenchmarkCategory("Items", "ItemEvaluation", "InvariantMetadata")]
[MemoryDiagnoser]
public class InvariantItemMetadataEvaluationBenchmark
{
    private const int OperationCount = 64;
    private const int ItemsPerOperation = 4;
    private const int ExpectedItemCount = OperationCount * ItemsPerOperation;

    private string _fallbackProjectXml = null!;
    private string _invariantProjectXml = null!;

    [Params(4, 10)]
    public int MetadataCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _invariantProjectXml = CreateProjectXml(useMetadataReference: false);
        _fallbackProjectXml = CreateProjectXml(useMetadataReference: true);

        ValidateProject(_invariantProjectXml, expectedSecondMetadata: "value1");
        ValidateProjectInstance(_invariantProjectXml, expectedSecondMetadata: "value1");
        ValidateProject(_fallbackProjectXml, expectedSecondMetadata: "value0-value1");
        ValidateProjectInstance(_fallbackProjectXml, expectedSecondMetadata: "value0-value1");
    }

    [Benchmark]
    public int Project_InvariantNoPrefixRead()
        => EvaluateProject(_invariantProjectXml);

    [Benchmark]
    public int ProjectInstance_InvariantNoPrefixRead()
        => EvaluateProjectInstance(_invariantProjectXml);

    [Benchmark]
    public int Project_PerItemMetadataReferenceFallback()
        => EvaluateProject(_fallbackProjectXml);

    [Benchmark]
    public int ProjectInstance_PerItemMetadataReferenceFallback()
        => EvaluateProjectInstance(_fallbackProjectXml);

    private string CreateProjectXml(bool useMetadataReference)
    {
        StringBuilder builder = new();
        builder.AppendLine("<Project>");
        builder.AppendLine("  <ItemGroup>");

        for (int operation = 0; operation < OperationCount; operation++)
        {
            builder.Append("    <I Include=\"");

            for (int item = 0; item < ItemsPerOperation; item++)
            {
                if (item > 0)
                {
                    builder.Append(';');
                }

                builder.Append("operation");
                builder.Append(operation);
                builder.Append("-item");
                builder.Append(item);
            }

            builder.AppendLine("\">");

            for (int metadata = 0; metadata < MetadataCount; metadata++)
            {
                builder.Append("      <M");
                builder.Append(metadata);
                builder.Append('>');

                if (useMetadataReference && metadata == 1)
                {
                    builder.Append("%(M0)-value1");
                }
                else
                {
                    builder.Append("value");
                    builder.Append(metadata);
                }

                builder.Append("</M");
                builder.Append(metadata);
                builder.AppendLine(">");
            }

            builder.AppendLine("    </I>");
        }

        builder.AppendLine("  </ItemGroup>");
        builder.AppendLine("</Project>");
        return builder.ToString();
    }

    private static int EvaluateProject(string projectXml)
    {
        using ProjectCollection collection = new();
        using XmlReader reader = XmlReader.Create(new StringReader(projectXml));
        Project project = new(reader, globalProperties: null, toolsVersion: null, collection);
        return project.GetItems("I").Count;
    }

    private static int EvaluateProjectInstance(string projectXml)
    {
        using ProjectCollection collection = new();
        using XmlReader reader = XmlReader.Create(new StringReader(projectXml));
        ProjectRootElement root = ProjectRootElement.Create(reader, collection);
        ProjectInstance project = ProjectInstance.FromProjectRootElement(root, new ProjectOptions
        {
            ProjectCollection = collection,
        });

        return project.GetItems("I").Count;
    }

    private void ValidateProject(string projectXml, string expectedSecondMetadata)
    {
        using ProjectCollection collection = new();
        using XmlReader reader = XmlReader.Create(new StringReader(projectXml));
        Project project = new(reader, globalProperties: null, toolsVersion: null, collection);
        ICollection<ProjectItem> items = project.GetItems("I");

        ValidateItemCount(items.Count);

        foreach (ProjectItem item in items)
        {
            for (int metadata = 0; metadata < MetadataCount; metadata++)
            {
                ValidateMetadata(metadata, item.GetMetadataValue($"M{metadata}"), expectedSecondMetadata);
            }
        }
    }

    private void ValidateProjectInstance(string projectXml, string expectedSecondMetadata)
    {
        using ProjectCollection collection = new();
        using XmlReader reader = XmlReader.Create(new StringReader(projectXml));
        ProjectRootElement root = ProjectRootElement.Create(reader, collection);
        ProjectInstance project = ProjectInstance.FromProjectRootElement(root, new ProjectOptions
        {
            ProjectCollection = collection,
        });
        ICollection<ProjectItemInstance> items = project.GetItems("I");

        ValidateItemCount(items.Count);

        foreach (ProjectItemInstance item in items)
        {
            for (int metadata = 0; metadata < MetadataCount; metadata++)
            {
                ValidateMetadata(metadata, item.GetMetadataValue($"M{metadata}"), expectedSecondMetadata);
            }
        }
    }

    private static void ValidateItemCount(int itemCount)
    {
        if (itemCount != ExpectedItemCount)
        {
            throw new InvalidOperationException("The benchmark project did not evaluate to the expected items and metadata.");
        }
    }

    private static void ValidateMetadata(int metadata, string actualValue, string expectedSecondMetadata)
    {
        string expectedValue = metadata == 1 ? expectedSecondMetadata : $"value{metadata}";

        if (actualValue != expectedValue)
        {
            throw new InvalidOperationException("The benchmark project did not evaluate to the expected items and metadata.");
        }
    }
}
