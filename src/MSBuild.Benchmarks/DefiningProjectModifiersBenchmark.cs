// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MSBuild.Benchmarks;

[MemoryDiagnoser]
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
    private ProjectInstance _singleProjectInstance = null!;
    private ProjectInstance _multiProjectInstance = null!;
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

        // --- Single-project scenario ---
        // All items defined in one project file. DefiningProjectFullPath is the same for all items,
        // so a cache keyed by defining project path would hit on every item after the first.
        using (var pc = new ProjectCollection())
        {
            var root = Microsoft.Build.Construction.ProjectRootElement.Create(pc);
            root.FullPath = Path.Combine(_tempDir, "SingleProject.csproj");

            var itemGroup = root.AddItemGroup();
            for (int i = 0; i < ItemsPerProject; i++)
            {
                itemGroup.AddItem("Compile", Path.Combine(srcDir, $"File{i}.cs"));
            }

            var project = new Project(root, null, null, pc);
            _singleProjectInstance = project.CreateProjectInstance();
        }

        // --- Multi-project scenario ---
        // Items imported from a second project file. The main project and the imported project
        // each define items, so there are two distinct DefiningProjectFullPath values.
        using (var pc = new ProjectCollection())
        {
            // Imported project defines half the items.
            var importRoot = Microsoft.Build.Construction.ProjectRootElement.Create(pc);
            importRoot.FullPath = Path.Combine(_tempDir, "Imported.props");
            var importItemGroup = importRoot.AddItemGroup();
            for (int i = 0; i < ItemsPerProject / 2; i++)
            {
                importItemGroup.AddItem("Compile", Path.Combine(srcDir, $"File{i}.cs"));
            }

            importRoot.Save();

            // Main project imports the props file and defines the other half.
            var mainRoot = Microsoft.Build.Construction.ProjectRootElement.Create(pc);
            mainRoot.FullPath = Path.Combine(_tempDir, "MainProject.csproj");
            mainRoot.AddImport("Imported.props");
            var mainItemGroup = mainRoot.AddItemGroup();
            for (int i = ItemsPerProject / 2; i < ItemsPerProject; i++)
            {
                mainItemGroup.AddItem("Compile", Path.Combine(srcDir, $"File{i}.cs"));
            }

            var project = new Project(mainRoot, null, null, pc);
            _multiProjectInstance = project.CreateProjectInstance();
        }

        // --- TaskItem instances with defining project set ---
        // Copy from ProjectItemInstance so that _definingProject is populated.
        var sourceItems = _singleProjectInstance.GetItems("Compile").ToArray();
        _taskItemsWithDefiningProject = new TaskItem[sourceItems.Length];
        for (int i = 0; i < sourceItems.Length; i++)
        {
            _taskItemsWithDefiningProject[i] = new TaskItem(sourceItems[i]);
        }
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
    // ProjectItemInstance: Read all DefiningProject* modifiers on one item.
    // Cold-cache baseline for all four DefiningProject modifiers.
    // -----------------------------------------------------------------------

    [Benchmark]
    public string ProjectItemInstance_AllDefiningProjectModifiers_Once()
    {
        ProjectItemInstance item = _singleProjectInstance.GetItems("Compile").First();
        string last = null!;

        last = item.GetMetadataValue(ItemSpecModifiers.DefiningProjectFullPath);
        last = item.GetMetadataValue(ItemSpecModifiers.DefiningProjectDirectory);
        last = item.GetMetadataValue(ItemSpecModifiers.DefiningProjectName);
        last = item.GetMetadataValue(ItemSpecModifiers.DefiningProjectExtension);

        return last;
    }

    // -----------------------------------------------------------------------
    // ProjectItemInstance: Read DefiningProjectDirectory repeatedly.
    // This is the most expensive DefiningProject modifier — it resolves
    // FullPath, RootDir, and Directory internally. Repeated reads on the
    // same item should benefit heavily from caching.
    // -----------------------------------------------------------------------

    [Benchmark]
    public string ProjectItemInstance_DefiningProjectDirectory_Repeated()
    {
        ProjectItemInstance item = _singleProjectInstance.GetItems("Compile").First();
        string last = null!;

        for (int i = 0; i < RepeatedReads; i++)
        {
            last = item.GetMetadataValue(ItemSpecModifiers.DefiningProjectDirectory);
        }

        return last;
    }

    // -----------------------------------------------------------------------
    // ProjectItemInstance: Read DefiningProjectName + DefiningProjectExtension
    // on all items from a single project.
    // All items share the same defining project, so a per-defining-project
    // cache should compute once and return cached results for the rest.
    // -----------------------------------------------------------------------

    [Benchmark]
    public string ProjectItemInstance_DefiningProjectNameExtension_AllItems_SingleProject()
    {
        string last = null!;

        foreach (ProjectItemInstance item in _singleProjectInstance.GetItems("Compile"))
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

    [Benchmark]
    public string ProjectItemInstance_DefiningProjectFullPath_AllItems_MultiProject()
    {
        string last = null!;

        foreach (ProjectItemInstance item in _multiProjectInstance.GetItems("Compile"))
        {
            last = item.GetMetadataValue(ItemSpecModifiers.DefiningProjectFullPath);
        }

        return last;
    }

    // -----------------------------------------------------------------------
    // ProjectItemInstance: Read DefiningProjectDirectory on all items from a
    // multi-project scenario, repeated.
    // The most expensive modifier across multiple passes — represents the
    // worst case for uncached DefiningProject resolution.
    // -----------------------------------------------------------------------

    [Benchmark]
    public string ProjectItemInstance_DefiningProjectDirectory_AllItems_MultiProject_Repeated()
    {
        string last = null!;

        for (int pass = 0; pass < RepeatedReads; pass++)
        {
            foreach (ProjectItemInstance item in _multiProjectInstance.GetItems("Compile"))
            {
                last = item.GetMetadataValue(ItemSpecModifiers.DefiningProjectDirectory);
            }
        }

        return last;
    }

    // -----------------------------------------------------------------------
    // TaskItem: Read all DefiningProject* modifiers on one item.
    // Exercises the Utilities.TaskItem → ItemSpecModifiers path with a
    // defining project obtained by copying from a ProjectItemInstance.
    // -----------------------------------------------------------------------

    [Benchmark]
    public string TaskItem_AllDefiningProjectModifiers_Once()
    {
        TaskItem item = _taskItemsWithDefiningProject[0];
        string last = null!;

        last = item.GetMetadata(ItemSpecModifiers.DefiningProjectFullPath);
        last = item.GetMetadata(ItemSpecModifiers.DefiningProjectDirectory);
        last = item.GetMetadata(ItemSpecModifiers.DefiningProjectName);
        last = item.GetMetadata(ItemSpecModifiers.DefiningProjectExtension);

        return last;
    }

    // -----------------------------------------------------------------------
    // TaskItem: Read DefiningProjectName + DefiningProjectExtension across
    // all items. All share the same defining project path.
    // -----------------------------------------------------------------------

    [Benchmark]
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
    // TaskItem: Read DefiningProjectDirectory repeatedly on one item.
    // -----------------------------------------------------------------------

    [Benchmark]
    public string TaskItem_DefiningProjectDirectory_Repeated()
    {
        TaskItem item = _taskItemsWithDefiningProject[0];
        string last = null!;

        for (int i = 0; i < RepeatedReads; i++)
        {
            last = item.GetMetadata(ItemSpecModifiers.DefiningProjectDirectory);
        }

        return last;
    }
}
