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
    ///  Stores one item type's append-only operations and evaluates requested prefixes using per-operation caches.
    /// </summary>
    private sealed class ItemOperationSequence
    {
        private readonly SegmentedList<MemoizedOperation> _operations = [];

        /// <summary>
        ///  Appends an operation without changing the positions identified by earlier references.
        /// </summary>
        /// <param name="operation">The operation to append.</param>
        public void Add(LazyItemOperation operation)
        {
            _operations.Add(new MemoizedOperation(operation));
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
        /// <param name="globsToIgnore">The later removed globs that may prune this computation.</param>
        /// <returns>
        ///  The matching items in evaluation order.
        /// </returns>
        public ImmutableList<I> GetMatchedItems(int operationCount, ImmutableHashSet<string> globsToIgnore)
        {
            ImmutableList<I>.Builder items = ImmutableList.CreateBuilder<I>();
            foreach (ItemData data in GetItemData(operationCount, globsToIgnore))
            {
                if (data.ConditionResult)
                {
                    items.Add(data.Item);
                }
            }

            return items.ToImmutable();
        }

        /// <summary>
        ///  Gets the items produced by a prefix, reusing results cached for the same glob-exclusion context.
        /// </summary>
        /// <param name="operationCount">The number of operations in the requested prefix.</param>
        /// <param name="globsToIgnore">The later removed globs that may prune this computation.</param>
        /// <returns>
        ///  A builder over the requested item result.
        /// </returns>
        public OrderedItemDataCollection.Builder GetItemData(int operationCount, ImmutableHashSet<string> globsToIgnore)
        {
            if (operationCount == 0)
            {
                return OrderedItemDataCollection.CreateBuilder();
            }

            // Captured references announce their demand before evaluation; eager condition reads announce it here.
            // The prefix is stable as operations are appended, but its result also depends on globsToIgnore.
            MemoizedOperation operation = _operations[operationCount - 1];
            if (operation.TryGetFromCache(globsToIgnore, out OrderedItemDataCollection items))
            {
                return items.ToBuilder();
            }

            operation.MarkAsReferenced();
            return ComputeItems(operationCount, globsToIgnore);
        }

        /// <summary>
        ///  Applies uncached operations in order, looking ahead for Remove globs and batching eligible literal Updates.
        /// </summary>
        /// <param name="operationCount">The number of operations in the requested prefix.</param>
        /// <param name="globsToIgnore">The later removed globs that may prune this computation.</param>
        /// <returns>
        ///  The item result after applying the requested prefix.
        /// </returns>
        private OrderedItemDataCollection.Builder ComputeItems(int operationCount, ImmutableHashSet<string> globsToIgnore)
        {
            OrderedItemDataCollection.Builder? items = null;
            int firstUncachedIndex = operationCount;

            // Save cumulative exclusion contexts at Remove boundaries so forward replay can restore them.
            Stack<ImmutableHashSet<string>>? globsToIgnoreStack = null;

            // Plan backward within the requested prefix only. Later Removes determine both which cached
            // prefixes are compatible and which files earlier Includes can skip during enumeration.
            for (int index = operationCount - 1; index >= 0; index--)
            {
                MemoizedOperation operation = _operations[index];
                var globsToIgnoreFromFutureOperations = globsToIgnoreStack?.Peek() ?? globsToIgnore;

                if (operation.TryGetFromCache(globsToIgnoreFromFutureOperations, out OrderedItemDataCollection itemsFromCache))
                {
                    // This cache contains the complete result through this operation, not just its changes.
                    // Since we search backward, this is the closest compatible cached prefix to the requested end.
                    // firstUncachedIndex is index + 1: we replay the suffix after this cached operation.
                    items = itemsFromCache.ToBuilder();
                    break;
                }

                // This Remove can prune earlier Includes. Extend the context only after checking its cache:
                // the Remove's own cached result is keyed by later exclusions, not by its own patterns.
                if (operation.Operation is RemoveOperation removeOperation)
                {
                    globsToIgnoreStack ??= new Stack<ImmutableHashSet<string>>();

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
            ImmutableHashSet<string> currentGlobsToIgnore = globsToIgnoreStack is null ? globsToIgnore : globsToIgnoreStack.Peek();

            // Disjoint literal Updates can be applied together with one scan of the current items.
            Dictionary<string, UpdateOperation> itemsWithNoWildcards = new Dictionary<string, UpdateOperation>(StringComparer.OrdinalIgnoreCase);
            bool addedToBatch = false;

            // Replay the uncached range directly, never visiting operations beyond the captured position.
            for (int index = firstUncachedIndex; index < operationCount; index++)
            {
                MemoizedOperation operation = _operations[index];

                if (operation.Operation is UpdateOperation op)
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
                            itemsWithNoWildcards.Remove(operation.Operation.Spec.Fragments[j].TextFragment);
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

                // This Remove is no longer in the future. Restore the context containing only later Removes
                // before applying it, so its result uses the same cache key checked during backward planning.
                if (operation.Operation is RemoveOperation)
                {
                    Assumed.NotNull(globsToIgnoreStack);
                    globsToIgnoreStack.Pop();
                    currentGlobsToIgnore = globsToIgnoreStack.Count == 0 ? globsToIgnore : globsToIgnoreStack.Peek();
                }

                // Non-batched operations use the memoized path, which saves the complete prefix result
                // when this position has been marked as referenced.
                operation.Apply(items, currentGlobsToIgnore);
            }

            // The requested prefix may end with a deferred Update batch.
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
