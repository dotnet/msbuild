// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    private class LazyItemList
    {
        private readonly LazyItemList _previous;
        private readonly MemoizedOperation _memoizedOperation;

        public LazyItemList(LazyItemList previous, ItemOperation operation)
        {
            _previous = previous;
            _memoizedOperation = new MemoizedOperation(operation);
        }

        public ImmutableList<I> GetFilteredItems(ImmutableHashSet<string> laterRemoveGlobPatterns)
        {
            ImmutableList<I>.Builder items = ImmutableList.CreateBuilder<I>();
            foreach (ItemData data in GetItemData(laterRemoveGlobPatterns))
            {
                if (data.ConditionResult)
                {
                    items.Add(data.Item);
                }
            }

            return items.ToImmutable();
        }

        public OrderedItemDataCollection.Builder GetItemData(ImmutableHashSet<string> laterRemoveGlobPatterns)
        {
            // Cache item states that expressions or current reads need. Item expressions mark their
            // positions before evaluation; current reads mark them on demand. A cached result can
            // be reused only with compatible later Remove glob patterns. Earlier linked operations
            // remain unchanged when new operations are added, and immutable snapshots share data.

            OrderedItemDataCollection items;
            if (_memoizedOperation.TryGetFromCache(laterRemoveGlobPatterns, out items))
            {
                return items.ToBuilder();
            }
            else
            {
                // tell the cache that this operation's result is needed by an external caller
                // this is required for callers that cannot tell the item list ahead of time that
                // they would be using an operation
                MarkAsNeeded();

                return ComputeItems(this, laterRemoveGlobPatterns);
            }
        }

        /// <summary>
        /// Applies uncached item operations (include, remove, update) in order. Since Remove effectively overwrites Include or Update,
        /// Remove operations are preprocessed (adding to laterRemoveGlobPatterns) to create a longer list of globs we don't need to process
        /// properly because we know they will be removed. Update operations are batched as much as possible, meaning rather
        /// than being applied immediately, they are combined into a dictionary of UpdateOperations that need to be applied. This
        /// is to optimize the case in which as series of UpdateOperations, each of which affects a single ItemSpec, are applied to all
        /// items in the list, leading to a quadratic-time operation.
        /// </summary>
        private static OrderedItemDataCollection.Builder ComputeItems(LazyItemList lazyItemList, ImmutableHashSet<string> laterRemoveGlobPatterns)
        {
            // Stack of operations up to the first one that's cached (exclusive)
            Stack<LazyItemList> itemListStack = new Stack<LazyItemList>();

            OrderedItemDataCollection.Builder items = null;

            // Keep a separate stack of lists of globs to ignore that only gets modified for Remove operations
            Stack<ImmutableHashSet<string>> laterRemoveGlobPatternStack = null;

            for (var currentList = lazyItemList; currentList != null; currentList = currentList._previous)
            {
                var patternsFromLaterOperations = laterRemoveGlobPatternStack?.Peek() ?? laterRemoveGlobPatterns;

                OrderedItemDataCollection itemsFromCache;
                if (currentList._memoizedOperation.TryGetFromCache(patternsFromLaterOperations, out itemsFromCache))
                {
                    // the base items on top of which to apply the uncached operations are the items of the first operation that is cached
                    items = itemsFromCache.ToBuilder();
                    break;
                }

                // If this is a remove operation, then add any globs that will be removed
                //  to a list of globs to ignore in previous operations
                if (currentList._memoizedOperation.Operation is RemoveOperation removeOperation)
                {
                    laterRemoveGlobPatternStack ??= new Stack<ImmutableHashSet<string>>();

                    var earlierRemoveGlobPatterns = removeOperation.GetRemoveGlobPatterns();
                    foreach (var globToRemove in patternsFromLaterOperations)
                    {
                        earlierRemoveGlobPatterns.Add(globToRemove);
                    }

                    laterRemoveGlobPatternStack.Push(earlierRemoveGlobPatterns.ToImmutable());
                }

                itemListStack.Push(currentList);
            }

            if (items == null)
            {
                items = OrderedItemDataCollection.CreateBuilder();
            }

            ImmutableHashSet<string> currentRemoveGlobPatterns = laterRemoveGlobPatternStack == null ? laterRemoveGlobPatterns : laterRemoveGlobPatternStack.Peek();

            Dictionary<string, UpdateOperation> itemsWithNoWildcards = new Dictionary<string, UpdateOperation>(StringComparer.OrdinalIgnoreCase);
            bool addedToBatch = false;

            // Walk back down the stack of item lists applying operations
            while (itemListStack.Count > 0)
            {
                var currentList = itemListStack.Pop();

                if (currentList._memoizedOperation.Operation is UpdateOperation op)
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
                            itemsWithNoWildcards.Remove(currentList._memoizedOperation.Operation.Spec.Fragments[j].TextFragment);
                        }
                    }
                    else
                    {
                        addedToBatch = true;
                        continue;
                    }
                }

                if (addedToBatch)
                {
                    addedToBatch = false;
                    ProcessNonWildCardItemUpdates(itemsWithNoWildcards, items);
                }

                // If this is a remove operation, then it could modify the globs to ignore, so pop the potentially
                //  modified entry off the stack of globs to ignore
                if (currentList._memoizedOperation.Operation is RemoveOperation)
                {
                    laterRemoveGlobPatternStack.Pop();
                    currentRemoveGlobPatterns = laterRemoveGlobPatternStack.Count == 0 ? laterRemoveGlobPatterns : laterRemoveGlobPatternStack.Peek();
                }

                currentList._memoizedOperation.Apply(items, currentRemoveGlobPatterns);
            }

            // We finished looping through the operations. Now process the final batch if necessary.
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
                    if (itemsWithNoWildcards.TryGetValue(fullPath, out UpdateOperation op))
                    {
                        items[i] = op.UpdateItem(items[i]);
                    }
                }
                itemsWithNoWildcards.Clear();
            }
        }

        public void MarkAsNeeded()
        {
            _memoizedOperation.MarkAsNeeded();
        }
    }
}
