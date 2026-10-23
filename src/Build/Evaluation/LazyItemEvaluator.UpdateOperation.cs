// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET
using System;
using System.Buffers;
#endif
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.CodeAnalysis.Collections;

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    private sealed partial class UpdateOperation : ItemOperation
    {
#if NET
        private static readonly SearchValues<char> s_expansionMarkers = SearchValues.Create("*?%$@");
#else
        // Candidate bits are indexed relative to '$': '$'=0, '%'=1, '*'=6, '?'=27, '@'=28.
        private const uint ExpansionMarkerBitmask = 0x1800_0043u;
#endif

        private readonly ImmutableArray<ProjectMetadataElement> _metadata;

        private QualifiedUpdateMatcher? _qualifiedUpdateMatcher;
        private MatchStrategy _matchStrategy;
        private bool? _needToExpandMetadataForEachItem;

        /// <summary>
        ///  Records completed batch classification, including specifications that cannot be batched.
        /// </summary>
        private bool _literalKeysChecked;

        /// <summary>
        ///  The unique literal keys, or a default array before classification or when this Update cannot be batched.
        /// </summary>
        private ImmutableArray<NormalizedItemPath> _literalKeys;

        /// <summary>
        ///  Gets the reusable scratch contexts, initialized on first access.
        /// </summary>
        /// <remarks>
        ///  Metadata expansion can evaluate bookmarked lists, but their positions precede this Update.
        ///  It cannot reenter this operation and clear the list during metadata application, so the contexts
        ///  can be consumed without an immutable snapshot.
        /// </remarks>
        private SegmentedList<ItemBatchingContext> ItemsToUpdate
            => field ??= [];

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

        /// <summary>
        ///  Tries to get the cached literal keys, classifying the specification on the first batch-admission attempt.
        /// </summary>
        /// <param name="keys">
        ///  The unique normalized keys on success, including an empty array for an empty specification;
        ///  otherwise, a default array.
        /// </param>
        /// <returns>
        ///  <see langword="true"/> when the specification can be batched; otherwise, <see langword="false"/>
        ///  when expansion syntax or duplicate normalized keys prevent batching.
        /// </returns>
        public bool TryGetLiteralKeys(out ImmutableArray<NormalizedItemPath> keys)
        {
            if (_literalKeysChecked)
            {
                keys = _literalKeys;
                return !keys.IsDefault;
            }

            var fragments = _itemSpec.Fragments;
            int fragmentCount = fragments.Count;
            if (fragmentCount == 0)
            {
                _literalKeys = [];
                _literalKeysChecked = true;
                keys = _literalKeys;
                return true;
            }

            if (fragmentCount == 1)
            {
                ItemSpecFragment fragment = fragments[0];
                if (ContainsExpansionSyntax(fragment.TextFragment))
                {
                    _literalKeysChecked = true;
                    keys = default;
                    return false;
                }

                string normalizedPath = FileUtilities.NormalizePathForComparisonNoThrow(fragment.TextFragment, fragment.ProjectDirectory);
                _literalKeys = [new NormalizedItemPath(normalizedPath)];
                _literalKeysChecked = true;
                keys = _literalKeys;
                return true;
            }

            NormalizedItemPath[] keyArray = new NormalizedItemPath[fragmentCount];
            HashSet<NormalizedItemPath> uniqueKeys = [];

            for (int i = 0; i < fragmentCount; i++)
            {
                ItemSpecFragment fragment = fragments[i];
                if (ContainsExpansionSyntax(fragment.TextFragment))
                {
                    _literalKeysChecked = true;
                    keys = default;
                    return false;
                }

                string normalizedPath = FileUtilities.NormalizePathForComparisonNoThrow(fragment.TextFragment, fragment.ProjectDirectory);
                NormalizedItemPath key = new(normalizedPath);

                // Different literal spellings can identify the same path and must not enter one batch.
                if (!uniqueKeys.Add(key))
                {
                    _literalKeysChecked = true;
                    keys = default;
                    return false;
                }

                keyArray[i] = key;
            }

            // Transfer the private buffer without copying; it is never mutated after this point.
            _literalKeys = ImmutableCollectionsMarshal.AsImmutableArray(keyArray);
            _literalKeysChecked = true;
            keys = _literalKeys;
            return true;
        }

        /// <summary>
        ///  Checks for wildcard, escape, property, or item syntax that prevents literal batching.
        /// </summary>
        /// <param name="text">The escaped specification fragment to check.</param>
        /// <returns>
        ///  <see langword="true"/> if <paramref name="text"/> contains <c>*</c>, <c>?</c>, <c>%</c>,
        ///  <c>$(</c>, or <c>@(</c>; otherwise, <see langword="false"/>.
        /// </returns>
        private static bool ContainsExpansionSyntax(string text)
        {
            int index = 0;
            while (index < text.Length)
            {
#if NET
                int offset = text.AsSpan(index).IndexOfAny(s_expansionMarkers);
                if (offset < 0)
                {
                    return false;
                }

                index += offset;
#else
                int offset = text[index] - '$';
                if ((uint)offset > '@' - '$' || (ExpansionMarkerBitmask & (1u << offset)) == 0)
                {
                    index++;
                    continue;
                }
#endif
                char marker = text[index];
                if (marker is '*' or '?' or '%')
                {
                    return true;
                }

                // A literal '$' or '@' is allowed; keep searching unless it starts a reference.
                if (index + 1 < text.Length && text[index + 1] == '(')
                {
                    return true;
                }

                index++;
            }

            return false;
        }

        protected override void ApplyImpl(OrderedItemDataCollection.Builder listBuilder, ImmutableHashSet<string> laterRemoveGlobPatterns)
        {
            if (!_conditionResult)
            {
                return;
            }

            PrepareMatching();
            var itemsToUpdate = ItemsToUpdate;
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
                    itemData => UpdateItem(itemData, matchResult.MatchingItems, itemsToUpdate));

                DecorateItemsWithMetadata(itemsToUpdate, _metadata, _needToExpandMetadataForEachItem);
            }
            finally
            {
                itemsToUpdate.Clear();
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
                var itemsToUpdate = ItemsToUpdate;
                try
                {
                    MatchResult matchResult = MatchItemSpec(item);
                    if (matchResult.IsMatch)
                    {
                        ItemData clonedData = UpdateItem(item, matchResult.MatchingItems, itemsToUpdate);
                        DecorateItemsWithMetadata(itemsToUpdate, _metadata, _needToExpandMetadataForEachItem);
                        return clonedData;
                    }
                }
                finally
                {
                    itemsToUpdate.Clear();
                }
            }
            return item;
        }

        private ItemData UpdateItem(
            ItemData item,
            Dictionary<string, I>? matchingItems,
            SegmentedList<ItemBatchingContext> itemsToUpdate)
        {
            // Clone before metadata mutation to preserve earlier snapshots, using the originating Include
            // so defining-project metadata does not start referring to this Update.
            ProjectItemElement originatingItemElement = _lazyEvaluator.GetIncludeData(item.IncludeId).ItemElement;
            _itemFactory.ItemElement = originatingItemElement;
            var clonedItem = _itemFactory.CreateItem(item.Item, originatingItemElement.ContainingProject.FullPath);
            _itemFactory.ItemElement = _itemElement;

            ItemData clonedData = item.WithItem(clonedItem);
            itemsToUpdate.Add(new ItemBatchingContext(clonedData.Item, matchingItems));
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
                MatchStrategy.QualifiedUpdate when _qualifiedUpdateMatcher is not null => _qualifiedUpdateMatcher.Match(itemData),
                _ => Assumed.Unreachable<MatchResult>(),
            };
    }
}
