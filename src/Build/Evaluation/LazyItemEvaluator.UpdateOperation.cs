// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Construction;
using System.Collections.Immutable;

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

            public override void Apply(ImmutableList<ItemData>.Builder listBuilder, ImmutableHashSet<string> globsToIgnore)
            {
                if (!_conditionResult)
                {
                    return;
                }

                ItemSpecMatchesItem matchItemspec;

                if (ItemSpecOnlyReferencesOneItemType(_itemSpec, _itemElement.ItemType))
                {
                    // Perf optimization: If the Update operation references itself (e.g. <I Update="@(I)"/>)
                    // then all items are updated and matching is not necessary
                    matchItemspec = (itemSpec, item) => true;
                }
                else
                {
                    matchItemspec = (itemSpec, item) => itemSpec.MatchesItem(item);
                }

                var matchedItems = ImmutableList.CreateBuilder<I>();

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

                        matchedItems.Add(clonedItemData.Item);
                    }
                }

                DecorateItemsWithMetadata(matchedItems.ToImmutableList(), _metadata);
            }

            private static bool ItemSpecOnlyReferencesOneItemType(ItemSpec<P, I> itemSpec, string itemType)
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

                if (!itemExpressionFragment.Capture.ItemType.Equals(itemType, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                return true;
            }
        }
    }
}
