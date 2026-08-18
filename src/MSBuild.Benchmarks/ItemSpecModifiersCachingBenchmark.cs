// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MSBuild.Benchmarks;

[MemoryDiagnoser]
public class ItemSpecModifiersCachingBenchmark
{
    /// <summary>
    /// Number of items to create for the multi-item benchmarks.
    /// </summary>
    private const int ItemCount = 200;

    /// <summary>
    /// Number of times each modifier is read per item, simulating repeated metadata access
    /// during evaluation, task execution, etc.
    /// </summary>
    private const int RepeatedReads = 10;

    private string _tempDir = null!;
    private TaskItem[] _taskItems = null!;
    private ProjectInstance _projectInstance = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MSBuildBenchmarks", Guid.NewGuid().ToString("N"));
        string srcDir = Path.Combine(_tempDir, "src", "Framework");
        Directory.CreateDirectory(srcDir);

        // Create TaskItem instances with realistic file paths.
        _taskItems = new TaskItem[ItemCount];
        for (int i = 0; i < ItemCount; i++)
        {
            string filePath = Path.Combine(srcDir, $"File{i}.cs");
            File.WriteAllText(filePath, string.Empty);
            _taskItems[i] = new TaskItem(filePath);
        }

        // Create a ProjectInstance with the same items for the ProjectItemInstance benchmarks.
        using var projectCollection = new ProjectCollection();
        var root = Microsoft.Build.Construction.ProjectRootElement.Create(projectCollection);
        root.FullPath = Path.Combine(_tempDir, "Test.csproj");

        var itemGroup = root.AddItemGroup();
        for (int i = 0; i < ItemCount; i++)
        {
            itemGroup.AddItem("Compile", Path.Combine(srcDir, $"File{i}.cs"));
        }

        var project = new Project(root, null, null, projectCollection);
        _projectInstance = project.CreateProjectInstance();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    // -----------------------------------------------------------------------
    // TaskItem: Read all derivable modifiers on one item once.
    // This is the cold-cache baseline — every modifier must be computed.
    // -----------------------------------------------------------------------

    [Benchmark]
    public string TaskItem_AllDerivableModifiers_Once()
    {
        TaskItem item = _taskItems[0];
        string last = null!;

        last = item.GetMetadata(ItemSpecModifiers.FullPath);
        last = item.GetMetadata(ItemSpecModifiers.RootDir);
        last = item.GetMetadata(ItemSpecModifiers.Filename);
        last = item.GetMetadata(ItemSpecModifiers.Extension);
        last = item.GetMetadata(ItemSpecModifiers.RelativeDir);
        last = item.GetMetadata(ItemSpecModifiers.Directory);
        last = item.GetMetadata(ItemSpecModifiers.Identity);

        return last;
    }

    // -----------------------------------------------------------------------
    // TaskItem: Read Filename + Extension repeatedly on one item.
    // This is the hot-path pattern — tasks reading the same metadata many
    // times on the same item. The cache should make reads 2..N near-free.
    // -----------------------------------------------------------------------

    [Benchmark]
    public string TaskItem_FilenameAndExtension_Repeated()
    {
        TaskItem item = _taskItems[0];
        string last = null!;

        for (int i = 0; i < RepeatedReads; i++)
        {
            last = item.GetMetadata(ItemSpecModifiers.Filename);
            last = item.GetMetadata(ItemSpecModifiers.Extension);
        }

        return last;
    }

    // -----------------------------------------------------------------------
    // TaskItem: Read Filename across many items.
    // Simulates a task iterating all items and reading %(Filename) on each.
    // First read per item populates the cache; this measures the amortized
    // cost including the initial computation.
    // -----------------------------------------------------------------------

    [Benchmark]
    public string TaskItem_Filename_ManyItems()
    {
        string last = null!;

        for (int i = 0; i < _taskItems.Length; i++)
        {
            last = _taskItems[i].GetMetadata(ItemSpecModifiers.Filename);
        }

        return last;
    }

    // -----------------------------------------------------------------------
    // TaskItem: Read FullPath + Directory + RootDir repeatedly on one item.
    // Directory and RootDir both depend on FullPath internally, so the cache
    // should eliminate redundant Path.GetFullPath calls after the first read.
    // -----------------------------------------------------------------------

    [Benchmark]
    public string TaskItem_FullPathDerivedModifiers_Repeated()
    {
        TaskItem item = _taskItems[0];
        string last = null!;

        for (int i = 0; i < RepeatedReads; i++)
        {
            last = item.GetMetadata(ItemSpecModifiers.FullPath);
            last = item.GetMetadata(ItemSpecModifiers.RootDir);
            last = item.GetMetadata(ItemSpecModifiers.Directory);
        }

        return last;
    }

    // -----------------------------------------------------------------------
    // ProjectItemInstance: Read all derivable modifiers once.
    // Exercises the ProjectItemInstance.TaskItem → BuiltInMetadata →
    // ItemSpecModifiers.GetItemSpecModifier(ref CachedItemSpecModifiers) path.
    // -----------------------------------------------------------------------

    [Benchmark]
    public string ProjectItemInstance_AllDerivableModifiers_Once()
    {
        ProjectItemInstance item = _projectInstance.GetItems("Compile").First();
        string last = null!;

        last = item.GetMetadataValue(ItemSpecModifiers.FullPath);
        last = item.GetMetadataValue(ItemSpecModifiers.RootDir);
        last = item.GetMetadataValue(ItemSpecModifiers.Filename);
        last = item.GetMetadataValue(ItemSpecModifiers.Extension);
        last = item.GetMetadataValue(ItemSpecModifiers.RelativeDir);
        last = item.GetMetadataValue(ItemSpecModifiers.Directory);
        last = item.GetMetadataValue(ItemSpecModifiers.Identity);

        return last;
    }

    // -----------------------------------------------------------------------
    // ProjectItemInstance: Read Filename + Extension on all items.
    // The dominant real-world pattern — iterating all Compile items and
    // reading %(Filename)%(Extension) for output path computation.
    // -----------------------------------------------------------------------

    [Benchmark]
    public string ProjectItemInstance_FilenameExtension_AllItems()
    {
        string last = null!;

        foreach (ProjectItemInstance item in _projectInstance.GetItems("Compile"))
        {
            last = item.GetMetadataValue(ItemSpecModifiers.Filename);
            last = item.GetMetadataValue(ItemSpecModifiers.Extension);
        }

        return last;
    }

    // -----------------------------------------------------------------------
    // ProjectItemInstance: Read Filename + Extension on all items, repeated.
    // Simulates multiple targets or tasks reading the same metadata from
    // the same evaluated items during a single build.
    // -----------------------------------------------------------------------

    [Benchmark]
    public string ProjectItemInstance_FilenameExtension_AllItems_Repeated()
    {
        string last = null!;

        for (int pass = 0; pass < RepeatedReads; pass++)
        {
            foreach (ProjectItemInstance item in _projectInstance.GetItems("Compile"))
            {
                last = item.GetMetadataValue(ItemSpecModifiers.Filename);
                last = item.GetMetadataValue(ItemSpecModifiers.Extension);
            }
        }

        return last;
    }
}
