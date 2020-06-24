// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Construction;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Build.Utilities;

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

            readonly struct MatchResult
            {
                public bool IsMatch { get; }
                public Dictionary<string, I> CapturedItemsFromReferencedItemTypes { get; }

                public MatchResult(bool isMatch, Dictionary<string, I> capturedItemsFromReferencedItemTypes)
                {
                    IsMatch = isMatch;
                    CapturedItemsFromReferencedItemTypes = capturedItemsFromReferencedItemTypes;
                }
            }

            delegate MatchResult ItemSpecMatchesItem(ItemSpec<P, I> itemSpec, I itemToMatch);

            protected override void ApplyImpl(ImmutableList<ItemData>.Builder listBuilder, ImmutableHashSet<string> globsToIgnore)
            {
                if (!_conditionResult)
                {
                    return;
                }

                ItemSpecMatchesItem matchItemspec;
                bool? needToExpandMetadataForEachItem = null;

                if (ItemspecContainsASingleBareItemReference(_itemSpec, _itemElement.ItemType))
                {
                    // Perf optimization: If the Update operation references itself (e.g. <I Update="@(I)"/>)
                    // then all items are updated and matching is not necessary
                    matchItemspec = (itemSpec, item) => new MatchResult(true, null);
                }
                else if (ItemSpecContainsItemReferences(_itemSpec)
                         && QualifiedMetadataReferencesExist(_metadata, out needToExpandMetadataForEachItem)
                         && !Traits.Instance.EscapeHatches.DoNotExpandQualifiedMetadataInUpdateOperation)
                {
                    var itemReferenceFragments = _itemSpec.Fragments.OfType<ItemSpec<P,I>.ItemExpressionFragment>().ToArray();
                    var nonItemReferenceFragments = _itemSpec.Fragments.Where(f => !(f is ItemSpec<P,I>.ItemExpressionFragment)).ToArray();

                    matchItemspec = (itemSpec, item) =>
                    {
                        var isMatch = nonItemReferenceFragments.Any(f => f.IsMatch(item.EvaluatedInclude));
                        Dictionary<string, I> capturedItemsFromReferencedItemTypes = null;

                        foreach (var itemReferenceFragment in itemReferenceFragments)
                        {
                            foreach (var referencedItem in itemReferenceFragment.ReferencedItems)
                            {
                                if (referencedItem.ItemAsValueFragment.IsMatch(item.EvaluatedInclude))
                                {
                                    isMatch = true;

                                    capturedItemsFromReferencedItemTypes ??= new Dictionary<string, I>(StringComparer.OrdinalIgnoreCase);

                                    capturedItemsFromReferencedItemTypes[referencedItem.Item.Key] = referencedItem.Item;
                                }
                            }
                        }

                        return new MatchResult(isMatch, capturedItemsFromReferencedItemTypes);
                    };
                }
                else
                {
                    matchItemspec = (itemSpec, item) => new MatchResult(itemSpec.MatchesItem(item), null);
                }

                var itemsToUpdate = ImmutableList.CreateBuilder<ItemBatchingContext>();

                for (int i = 0; i < listBuilder.Count; i++)
                {
                    var itemData = listBuilder[i];

                    var matchResult = matchItemspec(_itemSpec, itemData.Item);

                    if (matchResult.IsMatch)
                    {
                        // items should be deep immutable, so clone and replace items before mutating them
                        // otherwise, with GetItems caching enabled, the mutations would leak into the cache causing
                        // future operations to mutate the state of past operations
                        var clonedItemData = listBuilder[i].Clone(_itemFactory, _itemElement);
                        listBuilder[i] = clonedItemData;

                        itemsToUpdate.Add(new ItemBatchingContext(clonedItemData.Item, matchResult.CapturedItemsFromReferencedItemTypes));
                    }
                }

                DecorateItemsWithMetadata(itemsToUpdate.ToImmutableList(), _metadata, needToExpandMetadataForEachItem);
            }

            private bool QualifiedMetadataReferencesExist(ImmutableList<ProjectMetadataElement> metadata, out bool? needToExpandMetadataForEachItem)
            {
                needToExpandMetadataForEachItem = NeedToExpandMetadataForEachItem(metadata, out var itemsAndMetadataFound);

                if (itemsAndMetadataFound.Metadata == null)
                {
                    return false;
                }

                foreach (var metadataReference in itemsAndMetadataFound.Metadata)
                {
                    if (!string.IsNullOrWhiteSpace(metadataReference.Value.ItemName))
                    {
                        return true;
                    }
                }

                return false;
            }

            private static bool ItemSpecContainsItemReferences(ItemSpec<P, I> itemSpec)
            {
                return itemSpec.Fragments.Any(f => f is ItemSpec<P,I>.ItemExpressionFragment);
            }
        }
    }
}
