// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Build.Collections;
using Microsoft.CodeAnalysis.Collections;

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    /// <summary>
    ///  Stores one item type's append-only operations and evaluates prefixes using the evaluator's shared cache.
    /// </summary>
    /// <param name="lazyEvaluator">The evaluator that owns the cached prefix results.</param>
    private sealed class ItemOperationSequence(LazyItemEvaluator<P, I, M, D> lazyEvaluator)
    {
        private readonly LazyItemEvaluator<P, I, M, D> _lazyEvaluator = lazyEvaluator;
        private readonly SegmentedList<LazyItemOperation> _operations = [];

        /// <summary>
        ///  Appends an operation without changing the positions identified by earlier references.
        /// </summary>
        /// <param name="operation">The operation to append.</param>
        public void Add(LazyItemOperation operation)
        {
            _operations.Add(operation);
        }

        /// <summary>
        ///  Captures the current exclusive stopping position without computing or copying items.
        /// </summary>
        /// <returns>
        ///  A reference to the operations recorded so far.
        /// </returns>
        public ItemListReference CaptureCurrent() => new(this, _operations.Count);

        /// <summary>
        ///  Gets the condition-true items produced by a prefix of this sequence.
        /// </summary>
        /// <param name="operationCount">The number of operations in the requested prefix.</param>
        /// <returns>
        ///  The matching items in evaluation order.
        /// </returns>
        public ImmutableList<I> GetMatchedItems(int operationCount)
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
        ///  Gets the items produced by a prefix, reusing the result cached at its stopping position.
        /// </summary>
        /// <param name="operationCount">The number of operations in the requested prefix.</param>
        /// <returns>
        ///  An immutable snapshot of the requested item result.
        /// </returns>
        public OrderedItemDataCollection GetItemData(int operationCount)
        {
            if (operationCount == 0)
            {
                return OrderedItemDataCollection.Empty;
            }

            // Captured references announce their demand before evaluation; eager condition reads announce it here.
            // Direct requests start without later exclusions; backward planning can add them for earlier positions.
            LazyItemOperation operation = _operations[operationCount - 1];
            if (_lazyEvaluator.TryGetCachedItems(operation, s_emptyGlobsToIgnore, out OrderedItemDataCollection? items))
            {
                return items;
            }

            operation.MarkAsReferenced();
            return ComputeItems(operationCount);
        }

        /// <summary>
        ///  Applies uncached operations in order, looking ahead for Remove globs and batching eligible literal Updates.
        /// </summary>
        /// <param name="operationCount">The number of operations in the requested prefix.</param>
        /// <returns>
        ///  An immutable snapshot of the item result after applying the requested prefix.
        /// </returns>
        private OrderedItemDataCollection ComputeItems(int operationCount)
        {
            OrderedItemDataCollection.Builder? items = null;
            int firstUncachedIndex = operationCount;

            // Save cumulative exclusion contexts at Remove boundaries so forward replay can restore them.
            using RefStack<ImmutableHashSet<string>> globsToIgnoreStack = default;

            // Plan backward within the requested prefix only. Later Removes determine both which cached
            // prefixes are compatible and which files earlier Includes can skip during enumeration.
            for (int index = operationCount - 1; index >= 0; index--)
            {
                LazyItemOperation operation = _operations[index];
                var globsToIgnoreFromFutureOperations = globsToIgnoreStack.PeekOrDefault(s_emptyGlobsToIgnore);

                if (_lazyEvaluator.TryGetCachedItems(
                    operation,
                    globsToIgnoreFromFutureOperations,
                    out OrderedItemDataCollection? itemsFromCache))
                {
                    // The shared cache contains the complete result through this operation, not just its changes.
                    // Since we search backward, this is the closest compatible cached prefix to the requested end.
                    // firstUncachedIndex is index + 1: we replay the suffix after this cached operation.
                    items = itemsFromCache.ToBuilder();
                    break;
                }

                // This Remove can prune earlier Includes. Extend the context only after checking its cache:
                // the Remove's own cached result is keyed by later exclusions, not by its own patterns.
                if (operation is RemoveOperation removeOperation)
                {
                    var globsToIgnoreForPreviousOperations = removeOperation.GetRemovedGlobs();
                    foreach (var globToRemove in globsToIgnoreFromFutureOperations)
                    {
                        globsToIgnoreForPreviousOperations.Add(globToRemove);
                    }

                    globsToIgnoreStack.Push(globsToIgnoreForPreviousOperations.ToImmutable());
                }

                firstUncachedIndex = index;
            }

            // Without a cache hit, replay starts at operation zero with no items. Otherwise it starts from
            // the cached prefix's result; the existing immutable item storage protects that saved result.
            items ??= OrderedItemDataCollection.CreateBuilder();

            // The first operation to replay sees all Remove globs accumulated while planning backward.
            ImmutableHashSet<string> currentGlobsToIgnore = globsToIgnoreStack.PeekOrDefault(s_emptyGlobsToIgnore);

            // Disjoint literal Updates can be applied together with one scan of the current items.
            UpdateBatch updateBatch = default;

            // Replay the uncached range directly, never visiting operations beyond the captured position.
            for (int index = firstUncachedIndex; index < operationCount; index++)
            {
                LazyItemOperation operation = _operations[index];

                if (operation is UpdateOperation op && updateBatch.TryAdd(op))
                {
                    if (op.IsReferenced)
                    {
                        // A bookmark ends at this Update. Complete and cache its prefix before any later
                        // Updates join the batch, without applying this Update a second time through Apply.
                        updateBatch.ApplyTo(items);
                        op.CacheResultIfReferenced(items, currentGlobsToIgnore);
                    }

                    continue;
                }

                // Finish earlier Updates before an Include, Remove, or non-batchable Update changes the state.
                updateBatch.ApplyTo(items);

                // This Remove is no longer in the future. Restore the context containing only later Removes
                // before applying it, so its result uses the same cache key checked during backward planning.
                if (operation is RemoveOperation)
                {
                    globsToIgnoreStack.Pop();
                    currentGlobsToIgnore = globsToIgnoreStack.PeekOrDefault(s_emptyGlobsToIgnore);
                }

                // Apply saves the complete prefix result when this position has been marked as referenced.
                operation.Apply(items, currentGlobsToIgnore);
            }

            // The requested prefix may end with a deferred Update batch.
            updateBatch.ApplyTo(items);

            return items.ToImmutable();
        }

        /// <summary>
        ///  Marks a prefix's final operation for caching before another operation requests its result.
        /// </summary>
        /// <param name="operationCount">The number of operations in the referenced prefix.</param>
        public void MarkAsReferenced(int operationCount)
        {
            if (operationCount > 0)
            {
                _operations[operationCount - 1].MarkAsReferenced();
            }
        }
    }
}
