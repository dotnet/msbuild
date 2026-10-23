// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.Build.Collections;
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
        private QualifiedUpdateMatcher _qualifiedUpdateMatcher;
        private MatchStrategy _matchStrategy;
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

            PrepareMatching();
            _itemsToUpdate ??= [];
            try
            {
                // Each replacement immediately follows its match, retaining matching items without matching twice.
                MatchResult matchResult = default;
                listBuilder.UpdateAll(
                    itemData =>
                    {
                        matchResult = MatchItemSpec(itemData);
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
                PrepareMatching();
                _itemsToUpdate ??= [];
                try
                {
                    MatchResult matchResult = MatchItemSpec(item);
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
        ///  Prepares matching and metadata-expansion requirements on the first true-condition application.
        /// </summary>
        private void PrepareMatching()
        {
            if (_matchStrategy != MatchStrategy.Uninitialized)
            {
                return;
            }

            _needToExpandMetadataForEachItem = NeedToExpandMetadataForEachItem(_metadata, out var references);

            if (ItemspecContainsASingleBareItemReference(_itemSpec, _itemElement.ItemType))
            {
                // Perf optimization: If the Update operation references itself (e.g. <I Update="@(I)"/>)
                // then all items are updated and matching is not necessary
                _matchStrategy = MatchStrategy.All;
                return;
            }

            bool hasQualifiedMetadata = false;
            if (references.Metadata is not null)
            {
                foreach (var reference in references.Metadata.Values)
                {
                    if (!string.IsNullOrWhiteSpace(reference.ItemName))
                    {
                        hasQualifiedMetadata = true;
                        break;
                    }
                }
            }

            if (hasQualifiedMetadata
                && !Traits.Instance.EscapeHatches.DoNotExpandQualifiedMetadataInUpdateOperation
                && ItemSpecContainsItemReferences())
            {
                using RefArrayBuilder<ItemSpec<P, I>.ItemExpressionFragment> referenceFragments = default;
                using RefArrayBuilder<ItemSpecFragment> nonReferenceFragments = default;
                foreach (ItemSpecFragment fragment in _itemSpec.Fragments)
                {
                    if (fragment is ItemSpec<P, I>.ItemExpressionFragment referenceFragment)
                    {
                        referenceFragments.Add(referenceFragment);
                    }
                    else
                    {
                        nonReferenceFragments.Add(fragment);
                    }
                }

                _qualifiedUpdateMatcher = new QualifiedUpdateMatcher(referenceFragments.ToImmutable(), nonReferenceFragments.ToImmutable());
                _matchStrategy = MatchStrategy.QualifiedUpdate;
                return;
            }

            _matchStrategy = MatchStrategy.Specification;
        }

        private bool ItemSpecContainsItemReferences()
        {
            foreach (ItemSpecFragment fragment in _itemSpec.Fragments)
            {
                if (fragment is ItemSpec<P, I>.ItemExpressionFragment)
                {
                    return true;
                }
            }

            return false;
        }

        private MatchResult MatchItemSpec(ItemData itemData)
            => _matchStrategy switch
            {
                MatchStrategy.All => new MatchResult(isMatch: true, matchingItems: null),
                MatchStrategy.Specification => new MatchResult(_itemSpec.MatchesItem(itemData.Item), matchingItems: null),
                MatchStrategy.QualifiedUpdate => _qualifiedUpdateMatcher.Match(itemData),
                _ => Assumed.Unreachable<MatchResult>(),
            };
    }
}
