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
///  Measures cold population and repeated access for defining-project item-spec modifiers.
/// </summary>
/// <remarks>
///  Iteration setup creates fresh items and clears the process-wide defining-project cache. Each
///  benchmark batches enough items for a single measured invocation before normalization per item.
/// </remarks>
[MemoryDiagnoser]
[RunOncePerIteration]
public class DefiningProjectModifiersBenchmark
{
    /// <summary>
    /// Number of items per project file.
    /// </summary>
    private const int ItemsPerProject = 100;

    /// <summary>
    /// Number of times each modifier is read per item, simulating repeated metadata access
    /// during evaluation, task execution, etc.
    /// </summary>
    private const int RepeatedReads = 10;

    private string _tempDir = null!;
    private ProjectCollection _projectCollection = null!;
    private ProjectRootElement _singleProjectRoot = null!;
    private ProjectRootElement _multiProjectRoot = null!;
    private ProjectItemInstance[] _singleProjectItems = null!;
    private ProjectItemInstance[] _multiProjectItems = null!;
    private TaskItem[] _taskItemsWithDefiningProject = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MSBuildBenchmarks", Guid.NewGuid().ToString("N"));
        string srcDir = Path.Combine(_tempDir, "src");
        Directory.CreateDirectory(srcDir);

        // Create dummy files.
        for (int i = 0; i < ItemsPerProject; i++)
        {
            File.WriteAllText(Path.Combine(srcDir, $"File{i}.cs"), string.Empty);
        }

        _projectCollection = new ProjectCollection();

        // --- Single-project scenario ---
        // All items defined in one project file. DefiningProjectFullPath is the same for all items,
        // so a cache keyed by defining project path would hit on every item after the first.
        _singleProjectRoot = ProjectRootElement.Create(_projectCollection);
        _singleProjectRoot.FullPath = Path.Combine(_tempDir, "SingleProject.csproj");

        ProjectItemGroupElement singleProjectItemGroup = _singleProjectRoot.AddItemGroup();
        for (int i = 0; i < ItemsPerProject; i++)
        {
            singleProjectItemGroup.AddItem("Compile", Path.Combine(srcDir, $"File{i}.cs"));
        }

        // --- Multi-project scenario ---
        // Items imported from a second project file. The main project and the imported project
        // each define items, so there are two distinct DefiningProjectFullPath values.
        // Imported project defines half the items.
        ProjectRootElement importRoot = ProjectRootElement.Create(_projectCollection);
        importRoot.FullPath = Path.Combine(_tempDir, "Imported.props");
        ProjectItemGroupElement importItemGroup = importRoot.AddItemGroup();
        for (int i = 0; i < ItemsPerProject / 2; i++)
        {
            importItemGroup.AddItem("Compile", Path.Combine(srcDir, $"File{i}.cs"));
        }

        importRoot.Save();

        // Main project imports the props file and defines the other half.
        _multiProjectRoot = ProjectRootElement.Create(_projectCollection);
        _multiProjectRoot.FullPath = Path.Combine(_tempDir, "MainProject.csproj");
        _multiProjectRoot.AddImport("Imported.props");
        ProjectItemGroupElement mainItemGroup = _multiProjectRoot.AddItemGroup();
        for (int i = ItemsPerProject / 2; i < ItemsPerProject; i++)
        {
            mainItemGroup.AddItem("Compile", Path.Combine(srcDir, $"File{i}.cs"));
        }
    }

    [IterationSetup]
    public void IterationSetup()
    {
        // Copy from a throwaway ProjectInstance so TaskItem._definingProject is populated without
        // warming the ProjectItemInstance objects used by the measured workload.
        ProjectInstance taskItemSource = new(
            _singleProjectRoot,
            globalProperties: null,
            toolsVersion: null,
            _projectCollection);

        ProjectItemInstance[] sourceItems = [.. taskItemSource.GetItems("Compile")];
        _taskItemsWithDefiningProject = new TaskItem[sourceItems.Length];
        for (int i = 0; i < sourceItems.Length; i++)
        {
            _taskItemsWithDefiningProject[i] = new TaskItem(sourceItems[i]);
        }

        ProjectInstance singleProjectInstance = new(
            _singleProjectRoot,
            globalProperties: null,
            toolsVersion: null,
            _projectCollection);

        ProjectInstance multiProjectInstance = new(
            _multiProjectRoot,
            globalProperties: null,
            toolsVersion: null,
            _projectCollection);

        _singleProjectItems = [.. singleProjectInstance.GetItems("Compile")];
        _multiProjectItems = [.. multiProjectInstance.GetItems("Compile")];

        // TaskItem construction reads DefiningProjectFullPath from the source items. Reset the
        // process-wide cache after setup so every measured iteration starts from the same state.
        ItemSpecModifiers.ClearDefiningProjectCache();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        ItemSpecModifiers.ClearDefiningProjectCache();
        _projectCollection.Dispose();

        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    // -----------------------------------------------------------------------
    // ProjectItemInstance: Read all DefiningProject* modifiers once on each fresh item.
    // The process-wide cache starts empty and is shared across items from the same project.
    // -----------------------------------------------------------------------

    [Benchmark(OperationsPerInvoke = ItemsPerProject)]
    public string ProjectItemInstance_AllDefiningProjectModifiers_Once()
    {
        string last = null!;

        foreach (ProjectItemInstance item in _singleProjectItems)
        {
            last = item.GetMetadataValue(ItemSpecModifiers.DefiningProjectFullPath);
            last = item.GetMetadataValue(ItemSpecModifiers.DefiningProjectDirectory);
            last = item.GetMetadataValue(ItemSpecModifiers.DefiningProjectName);
            last = item.GetMetadataValue(ItemSpecModifiers.DefiningProjectExtension);
        }

        return last;
    }

    // -----------------------------------------------------------------------
    // ProjectItemInstance: Read DefiningProjectDirectory repeatedly on each item.
    // This is the most expensive DefiningProject modifier — it resolves
    // FullPath, RootDir, and Directory internally. Repeated reads on the
    // same item should benefit heavily from caching.
    // -----------------------------------------------------------------------

    [Benchmark(OperationsPerInvoke = ItemsPerProject)]
    public string ProjectItemInstance_DefiningProjectDirectory_Repeated()
    {
        string last = null!;

        foreach (ProjectItemInstance item in _singleProjectItems)
        {
            for (int i = 0; i < RepeatedReads; i++)
            {
                last = item.GetMetadataValue(ItemSpecModifiers.DefiningProjectDirectory);
            }
        }

        return last;
    }

    // -----------------------------------------------------------------------
    // ProjectItemInstance: Read DefiningProjectName + DefiningProjectExtension
    // on all items from a single project.
    // All items share the same defining project, so a per-defining-project
    // cache should compute once and return cached results for the rest.
    // -----------------------------------------------------------------------

    [Benchmark(OperationsPerInvoke = ItemsPerProject)]
    public string ProjectItemInstance_DefiningProjectNameExtension_AllItems_SingleProject()
    {
        string last = null!;

        foreach (ProjectItemInstance item in _singleProjectItems)
        {
            last = item.GetMetadataValue(ItemSpecModifiers.DefiningProjectName);
            last = item.GetMetadataValue(ItemSpecModifiers.DefiningProjectExtension);
        }

        return last;
    }

    // -----------------------------------------------------------------------
    // ProjectItemInstance: Read DefiningProjectFullPath on all items from a
    // multi-project scenario (main + import).
    // Items come from two different defining projects, so a cache keyed by
    // defining project path has two entries.
    // -----------------------------------------------------------------------

    [Benchmark(OperationsPerInvoke = ItemsPerProject)]
    public string ProjectItemInstance_DefiningProjectFullPath_AllItems_MultiProject()
    {
        string last = null!;

        foreach (ProjectItemInstance item in _multiProjectItems)
        {
            last = item.GetMetadataValue(ItemSpecModifiers.DefiningProjectFullPath);
        }

        return last;
    }

    // -----------------------------------------------------------------------
    // ProjectItemInstance: Read DefiningProjectDirectory on all items from a
    // multi-project scenario, repeated.
    // The first pass populates the two defining-project cache entries; later
    // passes measure repeated access through both process-wide and per-item caches.
    // -----------------------------------------------------------------------

    [Benchmark(OperationsPerInvoke = ItemsPerProject)]
    public string ProjectItemInstance_DefiningProjectDirectory_AllItems_MultiProject_Repeated()
    {
        string last = null!;

        for (int pass = 0; pass < RepeatedReads; pass++)
        {
            foreach (ProjectItemInstance item in _multiProjectItems)
            {
                last = item.GetMetadataValue(ItemSpecModifiers.DefiningProjectDirectory);
            }
        }

        return last;
    }

    // -----------------------------------------------------------------------
    // TaskItem: Read all DefiningProject* modifiers once on each item.
    // Exercises the Utilities.TaskItem → ItemSpecModifiers path with a
    // defining project obtained by copying from a ProjectItemInstance.
    // -----------------------------------------------------------------------

    [Benchmark(OperationsPerInvoke = ItemsPerProject)]
    public string TaskItem_AllDefiningProjectModifiers_Once()
    {
        string last = null!;

        foreach (TaskItem item in _taskItemsWithDefiningProject)
        {
            last = item.GetMetadata(ItemSpecModifiers.DefiningProjectFullPath);
            last = item.GetMetadata(ItemSpecModifiers.DefiningProjectDirectory);
            last = item.GetMetadata(ItemSpecModifiers.DefiningProjectName);
            last = item.GetMetadata(ItemSpecModifiers.DefiningProjectExtension);
        }

        return last;
    }

    // -----------------------------------------------------------------------
    // TaskItem: Read DefiningProjectName + DefiningProjectExtension across
    // all items. All share the same defining project path.
    // -----------------------------------------------------------------------

    [Benchmark(OperationsPerInvoke = ItemsPerProject)]
    public string TaskItem_DefiningProjectNameExtension_AllItems()
    {
        string last = null!;

        for (int i = 0; i < _taskItemsWithDefiningProject.Length; i++)
        {
            last = _taskItemsWithDefiningProject[i].GetMetadata(ItemSpecModifiers.DefiningProjectName);
            last = _taskItemsWithDefiningProject[i].GetMetadata(ItemSpecModifiers.DefiningProjectExtension);
        }

        return last;
    }

    // -----------------------------------------------------------------------
    // TaskItem: Read DefiningProjectDirectory repeatedly on each item.
    // -----------------------------------------------------------------------

    [Benchmark(OperationsPerInvoke = ItemsPerProject)]
    public string TaskItem_DefiningProjectDirectory_Repeated()
    {
        string last = null!;

        foreach (TaskItem item in _taskItemsWithDefiningProject)
        {
            for (int i = 0; i < RepeatedReads; i++)
            {
                last = item.GetMetadata(ItemSpecModifiers.DefiningProjectDirectory);
            }
        }

        return last;
    }
}
