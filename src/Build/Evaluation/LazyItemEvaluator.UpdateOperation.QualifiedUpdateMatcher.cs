// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    private partial class UpdateOperation
    {
        /// <summary>
        ///  Matches Update identities and identifies the matching items used for qualified metadata.
        /// </summary>
        /// <param name="referenceFragments">The nonempty sequence of item-reference fragments, in specification order.</param>
        /// <param name="nonReferenceFragments">The literal and glob fragments that can match independently of item-vector results.</param>
        private sealed class QualifiedUpdateMatcher(
            ImmutableArray<ItemSpec<P, I>.ItemExpressionFragment> referenceFragments,
            ImmutableArray<ItemSpecFragment> nonReferenceFragments)
        {
            private bool _itemIndexChecked;

            /// <summary>
            ///  The completed matching-item index, or <see langword="null"/> before checking eligibility or when ordered matching is required.
            /// </summary>
            private Dictionary<NormalizedItemPath, Dictionary<string, I>>? _matchingItemsByPath;

            public MatchResult Match(ItemData itemData)
            {
                I item = itemData.Item;
                bool isMatch = false;
                foreach (ItemSpecFragment fragment in nonReferenceFragments)
                {
                    if (fragment.IsMatch(item.EvaluatedInclude))
                    {
                        isMatch = true;
                        break;
                    }
                }

                PrepareItemIndex();
                if (_matchingItemsByPath is not null)
                {
                    if (_matchingItemsByPath.Count == 0)
                    {
                        return new MatchResult(isMatch, matchingItems: null);
                    }

                    string directory = referenceFragments[0].ProjectDirectory;

                    // The item's cached key may be rooted elsewhere than the Update's matching directory.
                    NormalizedItemPath key = string.Equals(item.ProjectDirectory, directory, StringComparison.Ordinal)
                        ? itemData.NormalizedItemValue
                        : new NormalizedItemPath(FileUtilities.NormalizePathForComparisonNoThrow(item.EvaluatedInclude, directory));
                    bool sourceMatched = _matchingItemsByPath.TryGetValue(key, out var sources);
                    return new MatchResult(isMatch || sourceMatched, sources);
                }

                Dictionary<string, I>? matchingItems = null;
                foreach (var fragment in referenceFragments)
                {
                    foreach (var source in fragment.ReferencedItems)
                    {
                        if (source.ItemAsValueFragment.IsMatch(item.EvaluatedInclude))
                        {
                            isMatch = true;
                            matchingItems ??= [with(StringComparer.OrdinalIgnoreCase)];
                            matchingItems[source.Item.Key] = source.Item;
                        }
                    }
                }

                return new MatchResult(isMatch, matchingItems);
            }

            /// <summary>
            ///  Indexes literal transformed identities, retaining the last matching item of each item type.
            /// </summary>
            /// <remarks>
            ///  Wildcard vector values keep ordered matching for the operation so literal and wildcard winners
            ///  cannot be reordered. Completed matching-item tables are read-only and shared between updated items.
            ///  Vector fragments with different matching directories also keep ordered matching because one
            ///  normalized target lookup cannot represent all of those directory contexts.
            /// </remarks>
            private void PrepareItemIndex()
            {
                if (_itemIndexChecked)
                {
                    return;
                }

                string directory = referenceFragments[0].ProjectDirectory;
                foreach (var fragment in referenceFragments)
                {
                    if (!string.Equals(directory, fragment.ProjectDirectory, StringComparison.Ordinal))
                    {
                        _itemIndexChecked = true;
                        return;
                    }

                    foreach (var source in fragment.ReferencedItems)
                    {
                        if (EngineFileUtilities.FilespecHasWildcards(source.ItemAsValueFragment.TextFragment))
                        {
                            _itemIndexChecked = true;
                            return;
                        }
                    }
                }

                Dictionary<NormalizedItemPath, Dictionary<string, I>> matchingSources = [];
                foreach (var fragment in referenceFragments)
                {
                    foreach (var source in fragment.ReferencedItems)
                    {
                        string unescapedSourceIdentity = EscapingUtilities.UnescapeAll(source.ItemAsValueFragment.TextFragment);
                        string normalizedSourceIdentity = FileUtilities.NormalizePathForComparisonNoThrow(
                            unescapedSourceIdentity, fragment.ProjectDirectory);
                        NormalizedItemPath key = new(normalizedSourceIdentity);

                        if (!matchingSources.TryGetValue(key, out var sources))
                        {
                            sources = new Dictionary<string, I>(StringComparer.OrdinalIgnoreCase);
                            matchingSources.Add(key, sources);
                        }

                        sources[source.Item.Key] = source.Item;
                    }
                }

                _matchingItemsByPath = matchingSources;
                _itemIndexChecked = true;
            }
        }
    }
}
