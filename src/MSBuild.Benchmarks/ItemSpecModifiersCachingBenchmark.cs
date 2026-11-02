// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MSBuild.Benchmarks;

/// <summary>
///  Measures cold and repeated access to derivable item-spec modifiers.
/// </summary>
/// <remarks>
///  Iteration setup creates fresh items, and each benchmark batches enough items for a single
///  measured invocation before results are normalized per item.
/// </remarks>
[MemoryDiagnoser]
[RunOncePerIteration]
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

    private TemporaryDirectory _tempDir = null!;
    private string[] _filePaths = null!;
    private ProjectCollection _projectCollection = null!;
    private ProjectRootElement _projectRoot = null!;
    private TaskItem[] _taskItems = null!;
    private ProjectItemInstance[] _projectItems = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _tempDir = new TemporaryDirectory(nameof(ItemSpecModifiersCachingBenchmark));
        string srcDir = _tempDir.CreateDirectory(Path.Combine("src", "Framework"));

        _filePaths = new string[ItemCount];
        for (int i = 0; i < ItemCount; i++)
        {
            string filePath = Path.Combine(srcDir, $"File{i}.cs");
            File.WriteAllText(filePath, string.Empty);
            _filePaths[i] = filePath;
        }

        _projectCollection = new ProjectCollection();
        _projectRoot = ProjectRootElement.Create(_projectCollection);
        _projectRoot.FullPath = _tempDir.GetPath("Test.csproj");

        ProjectItemGroupElement itemGroup = _projectRoot.AddItemGroup();
        for (int i = 0; i < ItemCount; i++)
        {
            itemGroup.AddItem("Compile", _filePaths[i]);
        }
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _taskItems = new TaskItem[ItemCount];
        for (int i = 0; i < ItemCount; i++)
        {
            _taskItems[i] = new TaskItem(_filePaths[i]);
        }

        ProjectInstance projectInstance = new(
            _projectRoot,
            globalProperties: null,
            toolsVersion: null,
            _projectCollection);

        _projectItems = [.. projectInstance.GetItems("Compile")];
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _projectCollection.Dispose();
        _tempDir.Dispose();
    }

    // -----------------------------------------------------------------------
    // TaskItem: Read all derivable modifiers once on each fresh item.
    // Every modifier is computed from a cold per-item cache.
    // -----------------------------------------------------------------------

    [Benchmark(OperationsPerInvoke = ItemCount)]
    public string TaskItem_AllDerivableModifiers_Once()
    {
        string last = null!;

        foreach (TaskItem item in _taskItems)
        {
            last = item.GetMetadata(ItemSpecModifiers.FullPath);
            last = item.GetMetadata(ItemSpecModifiers.RootDir);
            last = item.GetMetadata(ItemSpecModifiers.Filename);
            last = item.GetMetadata(ItemSpecModifiers.Extension);
            last = item.GetMetadata(ItemSpecModifiers.RelativeDir);
            last = item.GetMetadata(ItemSpecModifiers.Directory);
            last = item.GetMetadata(ItemSpecModifiers.Identity);
        }

        return last;
    }

    // -----------------------------------------------------------------------
    // TaskItem: Read Filename + Extension repeatedly on each item.
    // This is the hot-path pattern — tasks reading the same metadata many
    // times on the same item. The cache should make reads 2..N near-free.
    // -----------------------------------------------------------------------

    [Benchmark(OperationsPerInvoke = ItemCount)]
    public string TaskItem_FilenameAndExtension_Repeated()
    {
        string last = null!;

        foreach (TaskItem item in _taskItems)
        {
            for (int i = 0; i < RepeatedReads; i++)
            {
                last = item.GetMetadata(ItemSpecModifiers.Filename);
                last = item.GetMetadata(ItemSpecModifiers.Extension);
            }
        }

        return last;
    }

    // -----------------------------------------------------------------------
    // TaskItem: Read Filename across many items.
    // Simulates a task iterating all items and reading %(Filename) on each.
    // First read per item populates the cache; this measures the amortized
    // cost including the initial computation.
    // -----------------------------------------------------------------------

    [Benchmark(OperationsPerInvoke = ItemCount)]
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
    // TaskItem: Read FullPath + Directory + RootDir repeatedly on each item.
    // Directory and RootDir both depend on FullPath internally, so the cache
    // should eliminate redundant Path.GetFullPath calls after the first read.
    // -----------------------------------------------------------------------

    [Benchmark(OperationsPerInvoke = ItemCount)]
    public string TaskItem_FullPathDerivedModifiers_Repeated()
    {
        string last = null!;

        foreach (TaskItem item in _taskItems)
        {
            for (int i = 0; i < RepeatedReads; i++)
            {
                last = item.GetMetadata(ItemSpecModifiers.FullPath);
                last = item.GetMetadata(ItemSpecModifiers.RootDir);
                last = item.GetMetadata(ItemSpecModifiers.Directory);
            }
        }

        return last;
    }

    // -----------------------------------------------------------------------
    // ProjectItemInstance: Read all derivable modifiers once on each fresh item.
    // Exercises the ProjectItemInstance.TaskItem → BuiltInMetadata →
    // ItemSpecModifiers.GetItemSpecModifier(ref CachedItemSpecModifiers) path.
    // -----------------------------------------------------------------------

    [Benchmark(OperationsPerInvoke = ItemCount)]
    public string ProjectItemInstance_AllDerivableModifiers_Once()
    {
        string last = null!;

        foreach (ProjectItemInstance item in _projectItems)
        {
            last = item.GetMetadataValue(ItemSpecModifiers.FullPath);
            last = item.GetMetadataValue(ItemSpecModifiers.RootDir);
            last = item.GetMetadataValue(ItemSpecModifiers.Filename);
            last = item.GetMetadataValue(ItemSpecModifiers.Extension);
            last = item.GetMetadataValue(ItemSpecModifiers.RelativeDir);
            last = item.GetMetadataValue(ItemSpecModifiers.Directory);
            last = item.GetMetadataValue(ItemSpecModifiers.Identity);
        }

        return last;
    }

    // -----------------------------------------------------------------------
    // ProjectItemInstance: Read Filename + Extension on all items.
    // The dominant real-world pattern — iterating all Compile items and
    // reading %(Filename)%(Extension) for output path computation.
    // -----------------------------------------------------------------------

    [Benchmark(OperationsPerInvoke = ItemCount)]
    public string ProjectItemInstance_FilenameExtension_AllItems()
    {
        string last = null!;

        foreach (ProjectItemInstance item in _projectItems)
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

    [Benchmark(OperationsPerInvoke = ItemCount)]
    public string ProjectItemInstance_FilenameExtension_AllItems_Repeated()
    {
        string last = null!;

        for (int pass = 0; pass < RepeatedReads; pass++)
        {
            foreach (ProjectItemInstance item in _projectItems)
            {
                last = item.GetMetadataValue(ItemSpecModifiers.Filename);
                last = item.GetMetadataValue(ItemSpecModifiers.Extension);
            }
        }

        return last;
    }
}
