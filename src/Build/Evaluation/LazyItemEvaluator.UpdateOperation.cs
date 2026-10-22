// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.CodeAnalysis.Collections;

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    private sealed partial class UpdateOperation : ItemOperation
    {
        private readonly ImmutableArray<ProjectMetadataElement> _metadata;

        // Metadata expansion may evaluate bookmarked item lists, but their item states exclude this Update.
        // It therefore cannot reenter this operation and clear the list while it is being enumerated,
        // so metadata application can consume the mutable scratch list without an immutable snapshot.
        private SegmentedList<ItemBatchingContext> _itemsToUpdate = null;
        private ItemSpecMatchesItem _matchItemSpec = null;
        private bool? _needToExpandMetadataForEachItem = null;

        public UpdateOperation(
            LazyItemEvaluator<P, I, M, D> lazyEvaluator,
            ProjectItemElement itemElement,
            ItemSpec<P, I> itemSpec,
            bool conditionResult,
            ItemListBookmarks itemListBookmarks,
            ImmutableArray<ProjectMetadataElement> metadata)
            : base(lazyEvaluator, itemElement, itemSpec, conditionResult, itemListBookmarks)
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
            _itemsToUpdate ??= [];
            try
            {
                // Each replacement immediately follows its match, retaining matching items without matching twice.
                MatchResult matchResult = default;
                listBuilder.UpdateAll(
                    itemData =>
                    {
                        matchResult = _matchItemSpec(_itemSpec, itemData.Item);
                        return matchResult.IsMatch;
                    },
                    itemData => UpdateItem(itemData, matchResult.MatchingItems));

                DecorateItemsWithMetadata(_itemsToUpdate, _metadata, _needToExpandMetadataForEachItem);
            }
            finally
            {
                _itemsToUpdate.Clear();
            }
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
                _itemsToUpdate ??= [];
                try
                {
                    MatchResult matchResult = _matchItemSpec(_itemSpec, item.Item);
                    if (matchResult.IsMatch)
                    {
                        ItemData clonedData = UpdateItem(item, matchResult.MatchingItems);
                        DecorateItemsWithMetadata(_itemsToUpdate, _metadata, _needToExpandMetadataForEachItem);
                        return clonedData;
                    }
                }
                finally
                {
                    _itemsToUpdate.Clear();
                }
            }
            return item;
        }

        private ItemData UpdateItem(ItemData item, Dictionary<string, I> matchingItems)
        {
            // Clone before metadata mutation to preserve earlier snapshots, using the originating Include
            // so defining-project metadata does not start referring to this Update.
            ProjectItemElement originatingItemElement = _lazyEvaluator.GetIncludeData(item.IncludeId).ItemElement;
            _itemFactory.ItemElement = originatingItemElement;
            var clonedItem = _itemFactory.CreateItem(item.Item, originatingItemElement.ContainingProject.FullPath);
            _itemFactory.ItemElement = _itemElement;

            ItemData clonedData = item.WithItem(clonedItem);
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
