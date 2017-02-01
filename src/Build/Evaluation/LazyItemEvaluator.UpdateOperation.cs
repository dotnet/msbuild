// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

            public override void Apply(ImmutableList<ItemData>.Builder listBuilder, ImmutableHashSet<string> globsToIgnore)
            {
                if (!_conditionResult)
                {
                    return;
                }

                var matchedItems = ImmutableList.CreateBuilder<I>();

                for (int i = 0; i < listBuilder.Count; i++)
                {
                    var itemData = listBuilder[i];

                    if (_itemSpec.MatchesItem(itemData.Item))
                    {
                        // item lists should be deep immutable, so clone and replace items before mutating them
                        // otherwise, with GetItems caching enabled, future operations would mutate the state of past operations
                        var clonedItemData = listBuilder[i].Clone(_itemFactory, _itemElement);
                        listBuilder[i] = clonedItemData;

                        matchedItems.Add(clonedItemData.Item);
                    }
                }

                DecorateItemsWithMetadata(matchedItems, _metadata);
            }
        }
    }
}
