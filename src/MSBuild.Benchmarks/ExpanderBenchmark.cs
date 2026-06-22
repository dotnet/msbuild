// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

namespace MSBuild.Benchmarks;

/// <summary>
/// Benchmarks for the <see cref="Expander{P, I}"/> covering property expansion,
/// item list expansion, metadata expansion, and mixed expressions.
/// </summary>
[MemoryDiagnoser]
public class ExpanderBenchmark
{
    /// <summary>
    /// Number of properties to populate in the property bag.
    /// </summary>
    [Params(10, 100)]
    public int PropertyCount { get; set; }

    /// <summary>
    /// Number of items per item type in the item dictionary.
    /// </summary>
    [Params(10, 100)]
    public int ItemCount { get; set; }

    private Expander<ProjectPropertyInstance, ProjectItemInstance> _expander = null!;
    private IElementLocation _location = null!;

    // Kept alive for the lifetime of the benchmark because the created ProjectInstance/
    // ProjectItemInstance objects hold references back to this collection. Disposed in GlobalCleanup.
    private ProjectCollection _projectCollection = null!;

    // Pre-built expression strings, assigned in GlobalSetup.
    private string _singleProperty = null!;
    private string _multipleProperties = null!;
    private string _nestedProperties = null!;
    private string _propertyConcat = null!;

    private string _singleItemList = null!;
    private string _itemListWithTransform = null!;
    private string _itemListWithSeparator = null!;

    private string _singleMetadata = null!;
    private string _qualifiedMetadata = null!;
    private string _multipleMetadata = null!;

    private string _mixedPropertyAndItem = null!;
    private string _mixedPropertyAndMetadata = null!;
    private string _mixedAll = null!;

    private string _noExpansion = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _location = ElementLocation.EmptyLocation;

        // Use a dedicated ProjectCollection so the benchmark does not leak state into the global one.
        // Stored in a field (not a local using) so it stays alive for the benchmark iterations; the
        // ProjectInstance/ProjectItemInstance objects below reference it. Disposed in GlobalCleanup.
        _projectCollection = new ProjectCollection();
        ProjectRootElement xml = ProjectRootElement.Create(_projectCollection);
        xml.FullPath = @"c:\src\project.csproj";
        ProjectInstance project = new(xml, globalProperties: null, toolsVersion: null, _projectCollection);

        // --- Properties ---
        var properties = new PropertyDictionary<ProjectPropertyInstance>();
        for (int i = 0; i < PropertyCount; i++)
        {
            properties.Set(ProjectPropertyInstance.Create($"Prop{i}", $"Value{i}"));
        }

        // Add well-known properties used in expressions.
        properties.Set(ProjectPropertyInstance.Create("Configuration", "Release"));
        properties.Set(ProjectPropertyInstance.Create("Platform", "AnyCPU"));
        properties.Set(ProjectPropertyInstance.Create("OutputPath", @"bin\Release\net10.0"));
        properties.Set(ProjectPropertyInstance.Create("RootNamespace", "MyProject.Core"));
        properties.Set(ProjectPropertyInstance.Create("AssemblyName", "MyProject.Core"));
        properties.Set(ProjectPropertyInstance.Create("TargetFramework", "net10.0"));

        // --- Items ---
        var itemBag = new ItemDictionary<ProjectItemInstance>();
        for (int i = 0; i < ItemCount; i++)
        {
            var item = new ProjectItemInstance(project, "Compile", $@"src\dir{i % 10}\File{i}.cs", project.FullPath);
            item.SetMetadata("Culture", i % 2 == 0 ? "en-US" : "fr-FR");
            item.SetMetadata("Link", $@"linked\File{i}.cs");
            item.SetMetadata("Generator", "ResXFileCodeGenerator");
            itemBag.Add(item);
        }

        for (int i = 0; i < ItemCount / 2; i++)
        {
            var item = new ProjectItemInstance(project, "Reference", $"System.Lib{i}", project.FullPath);
            item.SetMetadata("HintPath", $@"packages\lib{i}\lib\net10.0\System.Lib{i}.dll");
            item.SetMetadata("Private", "true");
            itemBag.Add(item);
        }

        // --- Metadata table (for unqualified/qualified metadata lookups) ---
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Culture"] = "en-US",
            ["Generator"] = "ResXFileCodeGenerator",
            ["Compile.Link"] = @"linked\SomeFile.cs",
            ["Compile.Culture"] = "de-DE",
            ["Identity"] = @"src\SomeFile.cs",
        };

        var metadataTable = new StringMetadataTable(metadata);

        _expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(
            properties, itemBag, metadataTable, FileSystems.Default);

        // --- Build expressions ---

        // Property expressions
        _singleProperty = "$(Configuration)";
        _multipleProperties = @"$(Configuration)\$(Platform)\$(OutputPath)";
        _nestedProperties = "$(RootNamespace).$(AssemblyName)";
        _propertyConcat = $"prefix_$(Configuration)_$(Platform)_$(TargetFramework)_suffix";

        // Item expressions
        _singleItemList = "@(Compile)";
        _itemListWithTransform = "@(Compile->'%(Filename).obj')";
        _itemListWithSeparator = "@(Compile, ',')";

        // Metadata expressions
        _singleMetadata = "%(Culture)";
        _qualifiedMetadata = "%(Compile.Link)";
        _multipleMetadata = "%(Culture)_%(Generator)";

        // Mixed expressions
        _mixedPropertyAndItem = @"$(OutputPath)\@(Compile->'%(Filename)')";
        _mixedPropertyAndMetadata = @"$(OutputPath)\%(Culture)\%(Identity)";
        _mixedAll = @"$(OutputPath)\%(Culture)\@(Compile->'%(Filename)')";

        // Plain string (no expansion needed)
        _noExpansion = @"This is a plain string with no expansion tokens at all.";
    }

    [GlobalCleanup]
    public void GlobalCleanup()
        => _projectCollection?.Dispose();

    // =========================================================================
    // Property expansion
    // =========================================================================

    [Benchmark]
    public string Property_Single()
        => _expander.ExpandIntoStringLeaveEscaped(_singleProperty, ExpanderOptions.ExpandProperties, _location);

    [Benchmark]
    public string Property_Multiple()
        => _expander.ExpandIntoStringLeaveEscaped(_multipleProperties, ExpanderOptions.ExpandProperties, _location);

    [Benchmark]
    public string Property_Nested()
        => _expander.ExpandIntoStringLeaveEscaped(_nestedProperties, ExpanderOptions.ExpandProperties, _location);

    [Benchmark]
    public string Property_Concatenation()
        => _expander.ExpandIntoStringLeaveEscaped(_propertyConcat, ExpanderOptions.ExpandProperties, _location);

    // =========================================================================
    // Item list expansion
    // =========================================================================

    [Benchmark]
    public string ItemList_Simple()
        => _expander.ExpandIntoStringLeaveEscaped(_singleItemList, ExpanderOptions.ExpandItems, _location);

    [Benchmark]
    public string ItemList_WithTransform()
        => _expander.ExpandIntoStringLeaveEscaped(_itemListWithTransform, ExpanderOptions.ExpandItems, _location);

    [Benchmark]
    public string ItemList_WithSeparator()
        => _expander.ExpandIntoStringLeaveEscaped(_itemListWithSeparator, ExpanderOptions.ExpandItems, _location);

    // =========================================================================
    // Metadata expansion
    // =========================================================================

    [Benchmark]
    public string Metadata_Unqualified()
        => _expander.ExpandIntoStringLeaveEscaped(_singleMetadata, ExpanderOptions.ExpandMetadata, _location);

    [Benchmark]
    public string Metadata_Qualified()
        => _expander.ExpandIntoStringLeaveEscaped(_qualifiedMetadata, ExpanderOptions.ExpandMetadata, _location);

    [Benchmark]
    public string Metadata_Multiple()
        => _expander.ExpandIntoStringLeaveEscaped(_multipleMetadata, ExpanderOptions.ExpandMetadata, _location);

    // =========================================================================
    // Mixed expansion
    // =========================================================================

    [Benchmark]
    public string Mixed_PropertyAndItem()
        => _expander.ExpandIntoStringLeaveEscaped(_mixedPropertyAndItem, ExpanderOptions.ExpandPropertiesAndItems, _location);

    [Benchmark]
    public string Mixed_PropertyAndMetadata()
        => _expander.ExpandIntoStringLeaveEscaped(_mixedPropertyAndMetadata, ExpanderOptions.ExpandPropertiesAndMetadata, _location);

    [Benchmark]
    public string Mixed_All()
        => _expander.ExpandIntoStringLeaveEscaped(_mixedAll, ExpanderOptions.ExpandAll, _location);

    // =========================================================================
    // Baseline: no expansion
    // =========================================================================

    [Benchmark(Baseline = true)]
    public string NoExpansion()
        => _expander.ExpandIntoStringLeaveEscaped(_noExpansion, ExpanderOptions.ExpandAll, _location);

    // =========================================================================
    // ExpandIntoStringAndUnescape variants (measures unescape overhead)
    // =========================================================================

    [Benchmark]
    public string PropertyAndUnescape_Multiple()
        => _expander.ExpandIntoStringAndUnescape(_multipleProperties, ExpanderOptions.ExpandProperties, _location);

    [Benchmark]
    public string ItemListAndUnescape_WithTransform()
        => _expander.ExpandIntoStringAndUnescape(_itemListWithTransform, ExpanderOptions.ExpandItems, _location);

    [Benchmark]
    public string MetadataAndUnescape_Multiple()
        => _expander.ExpandIntoStringAndUnescape(_multipleMetadata, ExpanderOptions.ExpandMetadata, _location);

    [Benchmark]
    public string MixedAndUnescape_All()
        => _expander.ExpandIntoStringAndUnescape(_mixedAll, ExpanderOptions.ExpandAll, _location);
}
