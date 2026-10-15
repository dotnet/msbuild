// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.CodeAnalysis.Collections;

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    /// <summary>
    ///  Collects one item type's operation sequence and evaluates item states using the shared snapshot cache.
    /// </summary>
    /// <param name="lazyEvaluator">The evaluator that owns the cached snapshots.</param>
    private sealed class ItemOperationSequence(LazyItemEvaluator<P, I, M, D> lazyEvaluator)
    {
        private readonly LazyItemEvaluator<P, I, M, D> _lazyEvaluator = lazyEvaluator;
        private readonly SegmentedList<ItemOperation> _operations = [];

        /// <summary>
        ///  Appends an operation without changing the positions identified by earlier references.
        /// </summary>
        /// <param name="operation">The operation to append.</param>
        public void Add(ItemOperation operation)
        {
            _operations.Add(operation);
        }

        /// <summary>
        ///  Captures the current exclusive stopping position without computing or copying items.
        /// </summary>
        /// <returns>
        ///  A bookmark for the operations collected so far.
        /// </returns>
        public ItemListBookmark BookmarkCurrent() => new(this, _operations.Count);

        /// <summary>
        ///  Gets the items through the requested position, filtered by originating Include conditions.
        /// </summary>
        /// <param name="operationCount">The number of operations through the requested position.</param>
        /// <returns>
        ///  The filtered items in evaluation order.
        /// </returns>
        public ImmutableList<I> GetFilteredItems(int operationCount)
        {
            ImmutableList<I>.Builder items = ImmutableList.CreateBuilder<I>();
            foreach (ItemData data in GetItemData(operationCount))
            {
                if (data.ConditionResult)
                {
                    items.Add(data.Item);
                }
            }

            return items.ToImmutable();
        }

        /// <summary>
        ///  Gets the snapshot after the requested number of operations, reusing a cached result when possible.
        /// </summary>
        /// <param name="operationCount">The number of operations through the requested position.</param>
        /// <returns>
        ///  A builder over the requested item result.
        /// </returns>
        public OrderedItemDataCollection.Builder GetItemData(int operationCount)
        {
            if (operationCount == 0)
            {
                return OrderedItemDataCollection.CreateBuilder();
            }

            // Captured references announce their demand before evaluation; eager condition reads announce it here.
            // Direct requests start without later exclusions; backward planning can add them for earlier positions.
            ItemOperation operation = _operations[operationCount - 1];
            if (_lazyEvaluator.TryGetCachedItems(operation, s_emptyRemoveGlobPatterns, out OrderedItemDataCollection? items))
            {
                return items.ToBuilder();
            }

            operation.MarkAsNeeded();
            return ComputeItems(operationCount);
        }

        /// <summary>
        ///  Applies uncached operations in order, looking ahead for Remove globs and batching eligible literal Updates.
        /// </summary>
        /// <param name="operationCount">The number of operations through the requested position.</param>
        /// <returns>
        ///  The item result after applying the requested item state.
        /// </returns>
        private OrderedItemDataCollection.Builder ComputeItems(int operationCount)
        {
            OrderedItemDataCollection.Builder? items = null;
            int firstUncachedIndex = operationCount;

            // Save cumulative later Remove glob pattern sets at Remove boundaries so forward evaluation can restore them.
            Stack<ImmutableHashSet<string>>? laterRemoveGlobPatternStack = null;

            // Plan backward within the requested item state only. Later Removes determine both which cached
            // item states are compatible and which files earlier Includes can skip during enumeration.
            for (int index = operationCount - 1; index >= 0; index--)
            {
                ItemOperation operation = _operations[index];
                var patternsFromLaterOperations = laterRemoveGlobPatternStack?.Peek() ?? s_emptyRemoveGlobPatterns;

                if (_lazyEvaluator.TryGetCachedItems(
                    operation,
                    patternsFromLaterOperations,
                    out OrderedItemDataCollection? itemsFromCache))
                {
                    // The cached snapshot contains computed items through this operation under these later Remove patterns.
                    // The backward search finds the nearest compatible cached snapshot before the requested end.
                    // firstUncachedIndex is index + 1: only operations after this cached snapshot remain to apply.
                    items = itemsFromCache.ToBuilder();
                    break;
                }

                // This Remove can prune earlier Includes. Extend the context only after checking its cache:
                // the Remove's own cached result is keyed by later exclusions, not by its own patterns.
                if (operation is RemoveOperation removeOperation)
                {
                    laterRemoveGlobPatternStack ??= new Stack<ImmutableHashSet<string>>();

                    var earlierRemoveGlobPatterns = removeOperation.GetRemoveGlobPatterns();
                    foreach (var globToRemove in patternsFromLaterOperations)
                    {
                        earlierRemoveGlobPatterns.Add(globToRemove);
                    }

                    laterRemoveGlobPatternStack.Push(earlierRemoveGlobPatterns.ToImmutable());
                }

                firstUncachedIndex = index;
            }

            // Without a cache hit, evaluation starts at operation zero with no items. Otherwise it uses
            // the cached snapshot; shared immutable storage protects that earlier result.
            items ??= OrderedItemDataCollection.CreateBuilder();

            // The first operation to evaluation sees all Remove globs accumulated while planning backward.
            ImmutableHashSet<string> currentRemoveGlobPatterns = laterRemoveGlobPatternStack?.Peek() ?? s_emptyRemoveGlobPatterns;

            // Disjoint literal Updates can be applied together with one scan of the current items.
            Dictionary<string, UpdateOperation> itemsWithNoWildcards = new Dictionary<string, UpdateOperation>(StringComparer.OrdinalIgnoreCase);
            bool addedToBatch = false;

            // Apply the uncached operations without visiting positions beyond the requested count.
            for (int index = firstUncachedIndex; index < operationCount; index++)
            {
                ItemOperation operation = _operations[index];

                if (operation is UpdateOperation op)
                {
                    bool addToBatch = true;
                    int i;
                    // The TextFragments are things like abc.def or x*y.*z.
                    for (i = 0; i < op.Spec.Fragments.Count; i++)
                    {
                        ItemSpecFragment frag = op.Spec.Fragments[i];
                        if (MSBuildConstants.CharactersForExpansion.Any(frag.TextFragment.Contains))
                        {
                            // Fragment contains wild cards, items, or properties. Cannot batch over it using a dictionary.
                            addToBatch = false;
                            break;
                        }

                        string fullPath = FileUtilities.NormalizePathForComparisonNoThrow(frag.TextFragment, frag.ProjectDirectory);
                        if (itemsWithNoWildcards.ContainsKey(fullPath))
                        {
                            // Another update will already happen on this path. Make that happen before evaluating this one.
                            addToBatch = false;
                            break;
                        }
                        else
                        {
                            itemsWithNoWildcards.Add(fullPath, op);
                        }
                    }
                    if (!addToBatch)
                    {
                        // We found a wildcard. Remove any fragments associated with the current operation and process them later.
                        for (int j = 0; j < i; j++)
                        {
                            itemsWithNoWildcards.Remove(operation.Spec.Fragments[j].TextFragment);
                        }
                    }
                    else
                    {
                        // Defer this Update until the batch is flushed.
                        addedToBatch = true;
                        continue;
                    }
                }

                if (addedToBatch)
                {
                    // Finish earlier Updates before an Include, Remove, or non-batchable Update changes the state.
                    addedToBatch = false;
                    ProcessNonWildCardItemUpdates(itemsWithNoWildcards, items);
                }

                // Before applying this Remove, keep only the glob patterns from subsequent Removes
                // before applying it, so its result uses the same cache key checked during backward planning.
                if (operation is RemoveOperation)
                {
                    Assumed.NotNull(laterRemoveGlobPatternStack);
                    laterRemoveGlobPatternStack.Pop();
                    currentRemoveGlobPatterns = laterRemoveGlobPatternStack.Count == 0 ? s_emptyRemoveGlobPatterns : laterRemoveGlobPatternStack.Peek();
                }

                // Apply saves the complete snapshot when this position has been marked as referenced.
                operation.Apply(items, currentRemoveGlobPatterns);
            }

            // The requested position may end with a pending Update batch.
            ProcessNonWildCardItemUpdates(itemsWithNoWildcards, items);

            return items;
        }

        private static void ProcessNonWildCardItemUpdates(Dictionary<string, UpdateOperation> itemsWithNoWildcards, OrderedItemDataCollection.Builder items)
        {
            if (itemsWithNoWildcards.Count > 0)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    string fullPath = FileUtilities.NormalizePathForComparisonNoThrow(items[i].Item.EvaluatedInclude, items[i].Item.ProjectDirectory);
                    if (itemsWithNoWildcards.TryGetValue(fullPath, out UpdateOperation? op))
                    {
                        items[i] = op.UpdateItem(items[i]);
                    }
                }
                itemsWithNoWildcards.Clear();
            }
        }

        /// <summary>
        ///  Marks an item state's final operation for caching before another operation requests its result.
        /// </summary>
        /// <param name="operationCount">The number of operations in the referenced item state.</param>
        public void MarkAsNeeded(int operationCount)
        {
            if (operationCount > 0)
            {
                _operations[operationCount - 1].MarkAsNeeded();
            }
        }
    }
}
