// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

namespace MSBuild.Benchmarks;

/// <summary>
/// Compares strategies for adding a task's newly created output items to a <see cref="Lookup"/>:
///   * <see cref="OneAtATime"/> - the historical behavior: one <c>AddNewItem</c> call per item.
///   * <see cref="SingleOrBatch"/> - <c>AddNewItem</c> for 0/1 outputs, otherwise collect into a temporary
///     <see cref="List{T}"/> and call <c>AddNewItemsOfItemType</c> once.
///   * <see cref="Appender"/> - <c>BeginAddNewItems</c> ref struct that appends directly into the add table.
///
/// Every operation creates a fresh <see cref="Lookup"/> and enters a scope, as a task batch does.
/// <see cref="LookupOnly"/> measures just that fixed overhead so it can be subtracted.
/// Items are created once in setup so the numbers isolate the cost of adding them.
/// </summary>
[MemoryDiagnoser]
public class LookupAddNewItemsBenchmark
{
    [Params(0, 1, 8, 32, 64, 100, 1000, 10000)]
    public int ItemCount { get; set; }

    private const string ItemType = "TaskOutput";

    private ProjectInstance _project = null!;
    private ItemDictionary<ProjectItemInstance> _baseItems = null!;
    private ProjectItemInstance[] _outputs = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        using var pc = new ProjectCollection();
        var projectXml = Microsoft.Build.Construction.ProjectRootElement.Create(pc);
        projectXml.AddTarget("_");
        var project = new Project(projectXml, null, null, pc);
        _project = project.CreateProjectInstance();

        _baseItems = new ItemDictionary<ProjectItemInstance>();
        _outputs = new ProjectItemInstance[ItemCount];
        for (int i = 0; i < ItemCount; i++)
        {
            _outputs[i] = new ProjectItemInstance(_project, ItemType, $@"obj\out\file_{i:D6}.dll", _project.FullPath);
        }
    }

    private Lookup CreateLookup()
    {
        var lookup = new Lookup(_baseItems, new PropertyDictionary<ProjectPropertyInstance>());
        lookup.EnterScope("Task");
        return lookup;
    }

    [Benchmark]
    public object LookupOnly() => CreateLookup();

    [Benchmark(Baseline = true)]
    public object OneAtATime()
    {
        Lookup lookup = CreateLookup();
        foreach (ProjectItemInstance output in _outputs)
        {
            lookup.AddNewItem(output);
        }

        return lookup;
    }

    [Benchmark]
    public object SingleOrBatch()
    {
        Lookup lookup = CreateLookup();
        ProjectItemInstance[] outputs = _outputs;
        if (outputs.Length <= 1)
        {
            if (outputs.Length == 1)
            {
                lookup.AddNewItem(outputs[0]);
            }

            return lookup;
        }

        List<ProjectItemInstance>? newItems = null;
        foreach (ProjectItemInstance output in outputs)
        {
            newItems ??= new List<ProjectItemInstance>(outputs.Length);
            newItems.Add(output);
        }

        if (newItems is not null)
        {
            lookup.AddNewItemsOfItemType(ItemType, newItems);
        }

        return lookup;
    }

    [Benchmark]
    public object Appender()
    {
        Lookup lookup = CreateLookup();
        Lookup.NewItemAppender appender = lookup.BeginAddNewItems(ItemType, _outputs.Length);
        foreach (ProjectItemInstance output in _outputs)
        {
            appender.Add(output);
        }

        return lookup;
    }
}
