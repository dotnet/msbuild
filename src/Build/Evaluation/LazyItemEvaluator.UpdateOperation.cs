// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Construction;
using System.Collections.Immutable;
using System.Linq;

namespace Microsoft.Build.Evaluation
{
    internal partial class LazyItemEvaluator<P, I, M, D>
    {
        class UpdateOperation : LazyItemOperation
        {
            private readonly ImmutableList<ProjectMetadataElement> _metadata;

            public UpdateOperation(OperationBuilderWithMetadata builder, LazyItemEvaluator<P, I, M, D> lazyEvaluator)
                : base(builder, lazyEvaluator)
            {
                _metadata = builder.Metadata.ToImmutable();
            }

            delegate bool ItemSpecMatchesItem(ItemSpec<P, I> itemSpec, I item);

            protected override void ApplyImpl(ImmutableList<ItemData>.Builder listBuilder, ImmutableHashSet<string> globsToIgnore)
            {
                if (!_conditionResult)
                {
                    return;
                }

                ItemSpecMatchesItem matchItemspec;

                if (ItemspecContainsASingleItemReference(_itemSpec, _itemElement.ItemType))
                {
                    // Perf optimization: If the Update operation references itself (e.g. <I Update="@(I)"/>)
                    // then all items are updated and matching is not necessary
                    matchItemspec = (itemSpec, item) => true;
                }
                else
                {
                    matchItemspec = (itemSpec, item) => itemSpec.MatchesItem(item);
                }

                var matchedItems = ImmutableList.CreateBuilder<ItemBatchingContext>();

                var itemFragments = _itemSpec.Fragments.OfType<ItemExpressionFragment<P, I>>().ToArray();

                for (int i = 0; i < listBuilder.Count; i++)
                {
                    var itemData = listBuilder[i];

                    if (matchItemspec(_itemSpec, itemData.Item))
                    {
                        // items should be deep immutable, so clone and replace items before mutating them
                        // otherwise, with GetItems caching enabled, the mutations would leak into the cache causing
                        // future operations to mutate the state of past operations
                        var clonedItemData = listBuilder[i].Clone(_itemFactory, _itemElement);
                        listBuilder[i] = clonedItemData;

                        var matchingItems = new Dictionary<string, IItem>(StringComparer.OrdinalIgnoreCase);

                        // todo: do this only when there's qualified metadata references (ExpressionShredder.GetReferencedItemNamesAndMetadata)
                        // todo: don't match twice for item references, add Itemspec API to return matched items. Or separate fragments without adding new API
                        foreach (var itemFragment in itemFragments)
                        {
                            foreach (var item in itemFragment.ReferencedItems)
                            {
                                if (item.ItemAsValueFragment.IsMatch(itemData.Item.EvaluatedInclude))
                                {
                                    matchingItems[item.Item.Key] = item.Item;
                                }
                            }
                        }

                        matchedItems.Add(new ItemBatchingContext(clonedItemData.Item, matchingItems));
                    }
                }

                DecorateItemsWithMetadata(matchedItems.ToImmutableList(), _metadata);
            }

            private static bool ItemspecContainsASingleItemReference(ItemSpec<P, I> itemSpec, string referencedItemType)
            {
                if (itemSpec.Fragments.Count != 1)
                {
                    return false;
                }

                var itemExpressionFragment = itemSpec.Fragments[0] as ItemExpressionFragment<P, I>;
                if (itemExpressionFragment == null)
                {
                    return false;
                }

                if (!itemExpressionFragment.Capture.ItemType.Equals(referencedItemType, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                return true;
            }
        }
    }
}
