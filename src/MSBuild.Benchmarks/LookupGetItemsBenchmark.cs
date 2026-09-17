// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

namespace MSBuild.Benchmarks;

/// <summary>
/// Repro for the QtMsBuild perf regression reported in DevDiv#2940203.
///
/// Models a Qt-style C++ build: a large @(ClCompile) collection in the base scope, then a build
/// target that performs many small batched removes (e.g. partitioning sources for moc/uic/rcc).
/// Each remove accumulates into the scope's remove table, and each subsequent <c>GetItems</c>
/// walks every (item, remove) pair via <c>ShouldRemoveItem</c>'s linear scan.
///
/// Two scenarios:
///   * <see cref="SingleScope"/> - removes accumulate in one scope's PrimaryRemoveTable.
///     Per-call cost: O(N * M_so_far). Total over the run: O(N * M^2).
///   * <see cref="MergedSubScopes"/> - more realistic: each batch enters a child scope,
///     does the remove, calls GetItems, then leaves (which concats into the outer scope's
///     SecondaryRemoveTable without dedup since PR #12320, see Lookup.MergeScopeIntoNotLastScope).
/// </summary>
[MemoryDiagnoser]
public class LookupGetItemsBenchmark
{
    [Params(2000)]
    public int BaseItemCount { get; set; }

    [Params(100)]
    public int RemoveBatchCount { get; set; }

    [Params(10)]
    public int RemoveBatchSize { get; set; }

    private const string ItemType = "ClCompile";

    private ProjectInstance _project = null!;
    private ItemDictionary<ProjectItemInstance> _baseItems = null!;
    private List<List<ProjectItemInstance>> _removeBatches = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        // A real ProjectInstance is needed by ProjectItemInstance ctors but we do not
        // build it; we only use it as a host for ProjectItemInstance. Use a dedicated
        // ProjectCollection so the benchmark does not leak state into the global one.
        using var pc = new ProjectCollection();
        var projectXml = Microsoft.Build.Construction.ProjectRootElement.Create(pc);
        projectXml.AddTarget("_");
        var project = new Project(projectXml, null, null, pc);
        _project = project.CreateProjectInstance();

        // Build a Qt-ish set of source paths. Use varied lengths so ShouldRemoveItem's
        // length-fast-filter cannot trivially short-circuit every comparison.
        _baseItems = new ItemDictionary<ProjectItemInstance>();
        var allItems = new List<ProjectItemInstance>(BaseItemCount);
        for (int i = 0; i < BaseItemCount; i++)
        {
            // E.g. src\\group_07\\file_01234.cpp
            string include = $@"src\group_{i % 50:D2}\file_{i:D6}.cpp";
            var item = new ProjectItemInstance(_project, ItemType, include, _project.FullPath);
            _baseItems.Add(item);
            allItems.Add(item);
        }

        // Pre-compute the per-batch remove lists. Spread across the full base set so each
        // batch removes a few items from anywhere in the collection (not contiguous).
        _removeBatches = new List<List<ProjectItemInstance>>(RemoveBatchCount);
        int totalToRemove = RemoveBatchCount * RemoveBatchSize;
        if (totalToRemove > BaseItemCount)
        {
            throw new InvalidOperationException("Asked to remove more items than exist.");
        }

        int stride = BaseItemCount / totalToRemove;
        int idx = 0;
        for (int b = 0; b < RemoveBatchCount; b++)
        {
            var batch = new List<ProjectItemInstance>(RemoveBatchSize);
            for (int k = 0; k < RemoveBatchSize; k++)
            {
                batch.Add(allItems[idx]);
                idx += stride;
            }
            _removeBatches.Add(batch);
        }
    }

    /// <summary>
    /// Single-scope accumulation: M batched removes against the same primary remove table.
    /// After each batch we call GetItems, mirroring how a batched task walks items between
    /// successive intrinsic Remove operations. This is the "loaded gun" form of the
    /// regression - one PrimaryRemoveTable list grows monotonically.
    /// </summary>
    [Benchmark]
    public int SingleScope()
    {
        var lookup = new Lookup(_baseItems, new PropertyDictionary<ProjectPropertyInstance>());
        lookup.EnterScope("bench");

        int observedCount = 0;
        foreach (List<ProjectItemInstance> batch in _removeBatches)
        {
            lookup.RemoveItems(ItemType, batch);
            ICollection<ProjectItemInstance> items = lookup.GetItems(ItemType);
            observedCount = items.Count;
        }

        return observedCount;
    }

    /// <summary>
    /// Multi-scope: each batch enters a child scope, removes, calls GetItems, leaves.
    /// LeaveScope concatenates the batch's removes into the outer scope's remove list
    /// (with no dedup since #12320), so subsequent batches see a growing
    /// <c>allRemoves</c> in <see cref="Lookup.GetItems"/>.
    /// </summary>
    [Benchmark]
    public int MergedSubScopes()
    {
        var lookup = new Lookup(_baseItems, new PropertyDictionary<ProjectPropertyInstance>());
        Lookup.Scope outer = lookup.EnterScope("outer");

        int observedCount = 0;
        foreach (List<ProjectItemInstance> batch in _removeBatches)
        {
            Lookup.Scope inner = lookup.EnterScope("batch");
            lookup.RemoveItems(ItemType, batch);
            ICollection<ProjectItemInstance> items = lookup.GetItems(ItemType);
            observedCount = items.Count;
            inner.LeaveScope();
        }

        outer.LeaveScope();
        return observedCount;
    }

    /// <summary>
    /// Read-only baseline: the most common GetItems usage in real builds. The fix must not
    /// regress this path. With no removes/adds/modifies in scope, GetItems takes its
    /// early-out branch and returns the underlying collection directly with zero allocation.
    /// Used to verify we preserve PR #12320's wins on the no-remove path.
    /// </summary>
    [Benchmark]
    public int ReadOnly_NoRemoves()
    {
        var lookup = new Lookup(_baseItems, new PropertyDictionary<ProjectPropertyInstance>());
        lookup.EnterScope("readonly");

        int observedCount = 0;
        // Same number of GetItems calls as the other scenarios for fair comparison.
        for (int i = 0; i < _removeBatches.Count; i++)
        {
            ICollection<ProjectItemInstance> items = lookup.GetItems(ItemType);
            observedCount = items.Count;
        }

        return observedCount;
    }
}
