// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
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
        private RemoveGlobPatternSequence? _removeGlobPatterns;

        /// <summary>
        ///  The ending operation of the most recent successful current-state computation.
        /// </summary>
        private ItemOperation? _lastCurrentReadOperation;

        /// <summary>
        ///  Appends an operation without changing the positions identified by earlier references.
        /// </summary>
        /// <param name="operation">The operation to append.</param>
        public void Add(ItemOperation operation)
        {
            if (operation is RemoveOperation remove)
            {
                remove.CollectRemoveGlobPatterns(ref _removeGlobPatterns, _operations.Count + 1);
            }

            _operations.Add(operation);
        }

        /// <summary>
        ///  Gets the number of pattern sets cached for filesystem expansion.
        /// </summary>
        public int CachedRemoveGlobRangeCount => _removeGlobPatterns?.CachedRangeCount ?? 0;

        /// <summary>
        ///  Gets the total number of pattern sets constructed for filesystem expansion.
        /// </summary>
        public int CreatedRemoveGlobSetCount => _removeGlobPatterns?.CreatedSetCount ?? 0;

        /// <summary>
        ///  Creates a bookmark at the current operation count without computing or copying items.
        /// </summary>
        /// <returns>
        ///  A bookmark for the operations collected so far.
        /// </returns>
        public ItemListBookmark BookmarkCurrent()
        {
            if (_operations.Count > 0)
            {
                _operations[^1].MarkAsBookmarked();
            }

            return new(this, _operations.Count);
        }

        /// <summary>
        ///  Gets current items filtered by originating Include conditions, reusing the cached collection.
        /// </summary>
        /// <returns>
        ///  The current read-only collection of filtered items in evaluation order.
        /// </returns>
        public ICollection<I> GetCurrentFilteredItems()
            => GetCurrentItemData().GetFilteredItems(_lazyEvaluator._includes);

        /// <summary>
        ///  Computes the current snapshot before removing a replaceable previous snapshot from the cache.
        /// </summary>
        /// <returns>
        ///  An immutable snapshot of the current item result.
        /// </returns>
        /// <remarks>
        ///  Bookmarked positions and results using later Remove patterns remain cached. Bookmark reads
        ///  do not replace this sequence's current-state tracking.
        /// </remarks>
        public OrderedItemDataCollection GetCurrentItemData()
        {
            OrderedItemDataCollection items = GetItemData(_operations.Count);
            if (_operations.Count > 0)
            {
                ItemOperation lastOperation = _operations[^1];
                if (!ReferenceEquals(_lastCurrentReadOperation, lastOperation))
                {
                    ItemOperation? previous = _lastCurrentReadOperation;
                    _lastCurrentReadOperation = lastOperation;

                    // The previous snapshot remains available until its replacement has been computed successfully.
                    if (previous is not null && !previous.IsBookmarked)
                    {
                        _lazyEvaluator.RemoveCachedItems(previous, default);
                    }
                }
            }

            return items;
        }

        /// <summary>
        ///  Gets the items through the requested position, filtered by originating Include conditions.
        /// </summary>
        /// <param name="operationCount">The number of operations through the requested position.</param>
        /// <returns>
        ///  The cached read-only collection of filtered items in evaluation order.
        /// </returns>
        public ICollection<I> GetFilteredItems(int operationCount)
            => GetItemData(operationCount).GetFilteredItems(_lazyEvaluator._includes);

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

            // Bookmark and current reads request complete item states. Only BookmarkCurrent marks
            // a position for lasting reuse; a current read must not retain every previous snapshot.
            ItemOperation lastOperation = _operations[operationCount - 1];
            if (_lazyEvaluator.TryGetCachedItems(lastOperation, default, out OrderedItemDataCollection? items))
            {
                return items;
            }

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

            RemoveGlobPatternSequence? removeGlobPatterns = _removeGlobPatterns;
            int patternEnd = removeGlobPatterns?.GetPatternCount(operationCount) ?? 0;
            int patternStart = patternEnd;

            // Only patterns within the requested sequence participate. Moving the start boundary backward
            // adds each Remove's patterns without constructing cumulative exclusion sets.
            for (int index = operationCount - 1; index >= 0; index--)
            {
                ItemOperation operation = _operations[index];
                RemoveGlobPatterns laterRemoveGlobPatterns = removeGlobPatterns is null
                    ? default
                    : removeGlobPatterns.GetRange(patternStart, patternEnd);

                // Intermediate snapshots are cached at bookmarks. A current-only result is cached
                // without later Remove patterns; probing other positions would build unused pattern sets.
                bool canHaveCachedItems = operation.IsBookmarked
                    || (ReferenceEquals(operation, _lastCurrentReadOperation) && laterRemoveGlobPatterns.IsEmpty);
                if (canHaveCachedItems && _lazyEvaluator.TryGetCachedItems(
                    operation,
                    laterRemoveGlobPatterns,
                    out OrderedItemDataCollection? itemsFromCache))
                {
                    // The cached snapshot contains computed items through this operation under these later Remove patterns.
                    // The backward search finds the nearest compatible cached snapshot before the requested end.
                    // firstUncachedIndex is index + 1: only operations after this cached snapshot remain to apply.
                    items = itemsFromCache.ToBuilder();
                    break;
                }

                // This operation's own patterns apply only to earlier operations. Metadata Removes
                // and false-condition Removes never add patterns to the collected sequence.
                if (operation is RemoveOperation && removeGlobPatterns is not null)
                {
                    patternStart = removeGlobPatterns.IncludePatternsFromOperation(patternStart, index + 1);
                }

                firstUncachedIndex = index;
            }

            // Without a cache hit, evaluation starts at operation zero with no items. Otherwise it uses
            // the cached snapshot; shared immutable storage protects that earlier result.
            items ??= OrderedItemDataCollection.CreateBuilder();

            RemoveGlobPatterns currentRemoveGlobPatterns = removeGlobPatterns is null
                ? default
                : removeGlobPatterns.GetRange(patternStart, patternEnd);

            // Disjoint literal Updates can be applied together with one scan of the current items.
            UpdateBatch updateBatch = default;

            // Apply the uncached operations without visiting positions beyond the requested count.
            for (int index = firstUncachedIndex; index < operationCount; index++)
            {
                ItemOperation operation = _operations[index];

                if (operation is UpdateOperation op && updateBatch.TryAdd(op))
                {
                    if (op.IsBookmarked)
                    {
                        // A bookmark ends at this Update. Complete and cache its snapshot before any later
                        // Updates join the batch, without applying this Update a second time through Apply.
                        updateBatch.ApplyTo(items);
                        op.CacheResultIfBookmarked(items, currentRemoveGlobPatterns);
                    }

                    continue;
                }

                // Finish earlier Updates before an Include, Remove, or non-batchable Update changes the state.
                updateBatch.ApplyTo(items);

                // Before applying this Remove, keep only the glob patterns from subsequent Removes
                // before applying it, so its result uses the same cache key checked during backward planning.
                if (operation is RemoveOperation && removeGlobPatterns is not null)
                {
                    patternStart = removeGlobPatterns.SkipPatternsFromOperation(patternStart, patternEnd, index + 1);
                    currentRemoveGlobPatterns = removeGlobPatterns.GetRange(patternStart, patternEnd);
                }

                // Apply caches intermediate snapshots only at bookmarked positions.
                operation.Apply(items, currentRemoveGlobPatterns);
            }

            // The requested position may end with a pending Update batch.
            updateBatch.ApplyTo(items);

            ItemOperation lastOperation = _operations[operationCount - 1];
            if (lastOperation.IsBookmarked)
            {
                return _lazyEvaluator.GetCachedItems(lastOperation, default);
            }

            // Current-only reads need a reusable result, but no persistent operation-level demand.
            OrderedItemDataCollection result = items.ToImmutable();
            _lazyEvaluator.CacheItems(lastOperation, default, result);
            return result;
        }
    }
}
