// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    private sealed partial class UpdateOperation : ItemOperation
    {
        private readonly ImmutableArray<ProjectMetadataElement> _metadata;
        private ImmutableList<ItemBatchingContext>.Builder _itemsToUpdate = null;
        private ItemSpecMatchesItem _matchItemSpec = null;
        private bool? _needToExpandMetadataForEachItem = null;

        public UpdateOperation(
            LazyItemEvaluator<P, I, M, D> lazyEvaluator,
            ProjectItemElement itemElement,
            ItemSpec<P, I> itemSpec,
            bool conditionResult,
            ImmutableDictionary<string, LazyItemList> referencedItemList,
            ImmutableArray<ProjectMetadataElement> metadata)
            : base(lazyEvaluator, itemElement, itemSpec, conditionResult, referencedItemList)
        {
            _metadata = metadata;
        }

        protected override void ApplyImpl(OrderedItemDataCollection.Builder listBuilder, ImmutableHashSet<string> laterRemoveGlobPatterns)
        {
            if (!_conditionResult)
            {
                return;
            }

            SetMatchItemSpec();
            _itemsToUpdate ??= ImmutableList.CreateBuilder<ItemBatchingContext>();
            _itemsToUpdate.Clear();

            for (int i = 0; i < listBuilder.Count; i++)
            {
                var itemData = listBuilder[i];

                var matchResult = _matchItemSpec(_itemSpec, itemData.Item);

                if (matchResult.IsMatch)
                {
                    listBuilder[i] = UpdateItem(listBuilder[i], matchResult.MatchingItems);
                }
            }

            DecorateItemsWithMetadata(_itemsToUpdate.ToImmutableList(), _metadata, _needToExpandMetadataForEachItem);
        }

        /// <summary>
        /// Apply the Update operation to the item if it matches.
        /// </summary>
        /// <param name="item">The item to check for a match.</param>
        /// <returns>The updated item.</returns>
        internal ItemData UpdateItem(ItemData item)
        {
            if (_conditionResult)
            {
                SetMatchItemSpec();
                _itemsToUpdate ??= ImmutableList.CreateBuilder<ItemBatchingContext>();
                _itemsToUpdate.Clear();
                MatchResult matchResult = _matchItemSpec(_itemSpec, item.Item);
                if (matchResult.IsMatch)
                {
                    ItemData clonedData = UpdateItem(item, matchResult.MatchingItems);
                    DecorateItemsWithMetadata(_itemsToUpdate.ToImmutableList(), _metadata, _needToExpandMetadataForEachItem);
                    return clonedData;
                }
            }
            return item;
        }

        private ItemData UpdateItem(ItemData item, Dictionary<string, I> matchingItems)
        {
            // items should be deep immutable, so clone and replace items before mutating them
            // otherwise, with GetItems caching enabled, the mutations would leak into the cache causing
            // future operations to mutate the state of past operations
            ItemData clonedData = item.Clone(_itemFactory, _itemElement);
            _itemsToUpdate.Add(new ItemBatchingContext(clonedData.Item, matchingItems));
            return clonedData;
        }

        /// <summary>
        /// This sets the function used to determine whether an item matches an item spec.
        /// </summary>
        private void SetMatchItemSpec()
        {
            if (ItemspecContainsASingleBareItemReference(_itemSpec, _itemElement.ItemType))
            {
                // Perf optimization: If the Update operation references itself (e.g. <I Update="@(I)"/>)
                // then all items are updated and matching is not necessary
                _matchItemSpec = (itemSpec, item) => new MatchResult(true, null);
            }
            else if (ItemSpecContainsItemReferences(_itemSpec)
                     && QualifiedMetadataReferencesExist(_metadata, out _needToExpandMetadataForEachItem)
                     && !Traits.Instance.EscapeHatches.DoNotExpandQualifiedMetadataInUpdateOperation)
            {
                var itemReferenceFragments = _itemSpec.Fragments.OfType<ItemSpec<P, I>.ItemExpressionFragment>().ToArray();
                var nonItemReferenceFragments = _itemSpec.Fragments.Where(f => !(f is ItemSpec<P, I>.ItemExpressionFragment)).ToArray();

                _matchItemSpec = (itemSpec, item) =>
                {
                    var isMatch = nonItemReferenceFragments.Any(f => f.IsMatch(item.EvaluatedInclude));
                    Dictionary<string, I> matchingItems = null;

                    foreach (var itemReferenceFragment in itemReferenceFragments)
                    {
                        foreach (var referencedItem in itemReferenceFragment.ReferencedItems)
                        {
                            if (referencedItem.ItemAsValueFragment.IsMatch(item.EvaluatedInclude))
                            {
                                isMatch = true;

                                matchingItems ??= new Dictionary<string, I>(StringComparer.OrdinalIgnoreCase);

                                matchingItems[referencedItem.Item.Key] = referencedItem.Item;
                            }
                        }
                    }

                    return new MatchResult(isMatch, matchingItems);
                };
            }
            else
            {
                _matchItemSpec = (itemSpec, item) => new MatchResult(itemSpec.MatchesItem(item), null);
            }
        }

        private bool QualifiedMetadataReferencesExist(ImmutableArray<ProjectMetadataElement> metadata, out bool? needToExpandMetadataForEachItem)
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
            return itemSpec.Fragments.Any(f => f is ItemSpec<P, I>.ItemExpressionFragment);
        }
    }
}
