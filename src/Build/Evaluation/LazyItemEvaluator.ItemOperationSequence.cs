// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Build.Collections;
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
        ///  A non-default array of filtered items in evaluation order.
        /// </returns>
        public ImmutableArray<I> GetFilteredItems(int operationCount)
        {
            OrderedItemDataCollection itemData = GetItemData(operationCount);
            if (itemData.Count == 0)
            {
                return [];
            }

            using RefArrayBuilder<I> items = new(itemData.Count);
            foreach (ItemData data in itemData)
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
        ///  An immutable snapshot through the requested position.
        /// </returns>
        public OrderedItemDataCollection GetItemData(int operationCount)
        {
            if (operationCount == 0)
            {
                return OrderedItemDataCollection.Empty;
            }

            // Captured references announce their demand before evaluation; eager condition reads announce it here.
            // Direct requests start without later exclusions; backward planning can add them for earlier positions.
            ItemOperation operation = _operations[operationCount - 1];
            if (_lazyEvaluator.TryGetCachedItems(operation, s_emptyRemoveGlobPatterns, out OrderedItemDataCollection? items))
            {
                return items;
            }

            operation.MarkAsNeeded();
            return ComputeItems(operationCount);
        }

        /// <summary>
        ///  Applies uncached operations in order, looking ahead for Remove globs and batching eligible literal Updates.
        /// </summary>
        /// <param name="operationCount">The number of operations through the requested position.</param>
        /// <returns>
        ///  The snapshot after applying operations through the requested position.
        /// </returns>
        private OrderedItemDataCollection ComputeItems(int operationCount)
        {
            OrderedItemDataCollection.Builder? items = null;
            int firstUncachedIndex = operationCount;

            // Save cumulative later Remove glob pattern sets at Remove boundaries so forward evaluation can restore them.
            Stack<ImmutableHashSet<string>> laterRemoveGlobPatternStack = new();

            // Plan backward within the requested item state only. Later Removes determine both which cached
            // item states are compatible and which files earlier Includes can skip during enumeration.
            for (int index = operationCount - 1; index >= 0; index--)
            {
                ItemOperation operation = _operations[index];
                var patternsFromLaterOperations = laterRemoveGlobPatternStack.Count > 0 ? laterRemoveGlobPatternStack.Peek() : s_emptyRemoveGlobPatterns;

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
            ImmutableHashSet<string> currentRemoveGlobPatterns = laterRemoveGlobPatternStack.Count > 0 ? laterRemoveGlobPatternStack.Peek() : s_emptyRemoveGlobPatterns;

            // Disjoint literal Updates can be applied together with one scan of the current items.
            UpdateBatch updateBatch = default;

            // Apply the uncached operations without visiting positions beyond the requested count.
            for (int index = firstUncachedIndex; index < operationCount; index++)
            {
                ItemOperation operation = _operations[index];

                if (operation is UpdateOperation op && updateBatch.TryAdd(op))
                {
                    if (op.IsReferenced)
                    {
                        // A bookmark ends at this Update. Complete and cache its snapshot before any later
                        // Updates join the batch, without applying this Update a second time through Apply.
                        updateBatch.ApplyTo(items);
                        op.CacheResultIfReferenced(items, currentRemoveGlobPatterns);
                    }

                    continue;
                }

                // Finish earlier Updates before an Include, Remove, or non-batchable Update changes the state.
                updateBatch.ApplyTo(items);

                // Before applying this Remove, keep only the glob patterns from subsequent Removes
                // before applying it, so its result uses the same cache key checked during backward planning.
                if (operation is RemoveOperation)
                {
                    laterRemoveGlobPatternStack.Pop();
                    currentRemoveGlobPatterns = laterRemoveGlobPatternStack.Count > 0 ? laterRemoveGlobPatternStack.Peek() : s_emptyRemoveGlobPatterns;
                }

                // Apply saves the complete snapshot when this position has been marked as referenced.
                operation.Apply(items, currentRemoveGlobPatterns);
            }

            // The requested position may end with a pending Update batch.
            updateBatch.ApplyTo(items);

            return items.ToImmutable();
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
