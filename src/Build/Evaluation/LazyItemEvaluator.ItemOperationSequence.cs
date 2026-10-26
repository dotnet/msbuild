// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
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
        ///  The operation identifying the latest successfully computed current checkpoint.
        /// </summary>
        private LazyItemOperation? _currentCheckpointOperation;

        /// <summary>
        ///  Appends an operation without changing the positions identified by earlier references.
        /// </summary>
        /// <param name="operation">The operation to append.</param>
        public void Add(LazyItemOperation operation)
        {
            _operations.Add(operation);
        }

        /// <summary>
        ///  Captures and retains the current exclusive stopping position without computing or copying items.
        /// </summary>
        /// <returns>
        ///  A reference to the operations recorded so far.
        /// </returns>
        public ItemListReference CaptureCurrent()
        {
            if (_operations.Count > 0)
            {
                _operations[^1].MarkAsCaptured();
            }

            return new(this, _operations.Count);
        }

        /// <summary>
        ///  Gets the condition-true items through the latest operation, reusing the cached projection.
        /// </summary>
        /// <returns>
        ///  The current read-only collection of matching items in evaluation order.
        /// </returns>
        public ICollection<I> GetCurrentMatchedItems()
            => GetCurrentItemData().GetMatchedItems(_lazyEvaluator._includes);

        /// <summary>
        ///  Computes items through the latest operation and retires the previous current-only checkpoint after success.
        /// </summary>
        /// <returns>
        ///  An immutable snapshot of the current item result.
        /// </returns>
        /// <remarks>
        ///  Captured prefixes and nonempty exclusion contexts are not retired. Historical capture reads
        ///  do not change this sequence's current checkpoint.
        /// </remarks>
        public OrderedItemDataCollection GetCurrentItemData()
        {
            OrderedItemDataCollection items = GetItemData(_operations.Count);
            if (_operations.Count > 0)
            {
                LazyItemOperation lastOperation = _operations[^1];
                if (!ReferenceEquals(_currentCheckpointOperation, lastOperation))
                {
                    LazyItemOperation? previous = _currentCheckpointOperation;
                    _currentCheckpointOperation = lastOperation;

                    // The old result remains a resume point throughout computation, including failed attempts.
                    if (previous is not null && !previous.IsCaptured)
                    {
                        _lazyEvaluator.RemoveCachedItems(previous, s_emptyGlobsToIgnore);
                    }
                }
            }

            return items;
        }

        /// <summary>
        ///  Gets the condition-true items produced by a prefix of this sequence.
        /// </summary>
        /// <param name="operationCount">The number of operations in the requested prefix.</param>
        /// <returns>
        ///  The cached read-only collection of matching items in evaluation order.
        /// </returns>
        public ICollection<I> GetMatchedItems(int operationCount)
            => GetItemData(operationCount).GetMatchedItems(_lazyEvaluator._includes);

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

            // Both capture and current reads request an unpruned prefix. Only CaptureCurrent marks
            // persistent demand; a current read must not permanently retain every prefix it visits.
            LazyItemOperation lastOperation = _operations[operationCount - 1];
            if (_lazyEvaluator.TryGetCachedItems(lastOperation, s_emptyGlobsToIgnore, out OrderedItemDataCollection? items))
            {
                return items;
            }

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

            // Save cumulative exclusion contexts at ordinary Remove boundaries so forward replay can restore them.
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
                // Metadata Removes never prune Includes and do not push or pop exclusion contexts.
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
                    if (op.IsCaptured)
                    {
                        // A bookmark ends at this Update. Complete and cache its prefix before any later
                        // Updates join the batch, without applying this Update a second time through Apply.
                        updateBatch.ApplyTo(items);
                        op.CacheResultIfCaptured(items, currentGlobsToIgnore);
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

                // Apply saves intermediate results only where a capture requires them.
                operation.Apply(items, currentGlobsToIgnore);
            }

            // The requested prefix may end with a deferred Update batch.
            updateBatch.ApplyTo(items);

            LazyItemOperation lastOperation = _operations[operationCount - 1];
            if (lastOperation.IsCaptured)
            {
                return _lazyEvaluator.GetCachedItems(lastOperation, s_emptyGlobsToIgnore);
            }

            // Current-only reads need a reusable result, but no persistent operation-level demand.
            OrderedItemDataCollection result = items.ToImmutable();
            _lazyEvaluator.CacheItems(lastOperation, s_emptyGlobsToIgnore, result);
            return result;
        }
    }
}
