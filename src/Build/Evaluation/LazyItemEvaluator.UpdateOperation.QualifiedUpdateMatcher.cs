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
        /// <param name="expander">The fixed expansion context using the operation's bookmarks.</param>
        private sealed class QualifiedUpdateMatcher(
            ImmutableArray<ItemVectorFragment> referenceFragments,
            ImmutableArray<ItemSpecFragment> nonReferenceFragments,
            Expander<P, I> expander)
        {
            private bool _itemIndexChecked;
            private VectorState[]? _vectorStates;
            private FileSpecMatcherTester?[]? _nonReferenceMatchers;

            /// <summary>
            ///  The completed matching-item index, or <see langword="null"/> before checking eligibility or when ordered matching is required.
            /// </summary>
            private Dictionary<NormalizedItemPath, Dictionary<string, I>>? _matchingItemsByPath;

            public MatchResult Match(ItemData itemData)
            {
                I item = itemData.Item;
                bool isMatch = false;
                for (int i = 0; i < nonReferenceFragments.Length; i++)
                {
                    _nonReferenceMatchers ??= new FileSpecMatcherTester?[nonReferenceFragments.Length];
                    ItemSpecFragment fragment = nonReferenceFragments[i];
                    FileSpecMatcherTester? matcher = _nonReferenceMatchers[i];
                    if (!matcher.HasValue)
                    {
                        matcher = FileSpecMatcherTester.Parse(fragment.ProjectDirectory, fragment.Text);
                        _nonReferenceMatchers[i] = matcher;
                    }

                    if (matcher.Value.IsMatch(item.EvaluatedInclude))
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
                for (int i = 0; i < referenceFragments.Length; i++)
                {
                    var sources = GetVectorItems(i);
                    if (sources is null)
                    {
                        continue;
                    }

                    ref VectorState state = ref GetVectorState(i);
                    state.Matchers ??= new FileSpecMatcherTester?[sources.Count];
                    for (int j = 0; j < sources.Count; j++)
                    {
                        string? value = sources[j].Value;
                        Assumed.NotNull(value);
                        FileSpecMatcherTester? matcher = state.Matchers[j];
                        if (!matcher.HasValue)
                        {
                            matcher = FileSpecMatcherTester.Parse(referenceFragments[i].ProjectDirectory, value);
                            state.Matchers[j] = matcher;
                        }

                        if (matcher.Value.IsMatch(item.EvaluatedInclude))
                        {
                            I? source = sources[j].Item;
                            Assumed.NotNull(source);
                            isMatch = true;
                            matchingItems ??= [with(StringComparer.OrdinalIgnoreCase)];
                            matchingItems[source.Key] = source;
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
                for (int i = 0; i < referenceFragments.Length; i++)
                {
                    ItemVectorFragment fragment = referenceFragments[i];
                    if (!string.Equals(directory, fragment.ProjectDirectory, StringComparison.Ordinal))
                    {
                        _itemIndexChecked = true;
                        return;
                    }

                    var sources = GetVectorItems(i);
                    if (sources is null)
                    {
                        continue;
                    }

                    foreach (var source in sources)
                    {
                        string? value = source.Value;
                        Assumed.NotNull(value);
                        if (EngineFileUtilities.FilespecHasWildcards(value))
                        {
                            _itemIndexChecked = true;
                            return;
                        }
                    }
                }

                Dictionary<NormalizedItemPath, Dictionary<string, I>> matchingSources = [];
                for (int i = 0; i < referenceFragments.Length; i++)
                {
                    var entries = GetVectorItems(i);
                    if (entries is null)
                    {
                        continue;
                    }

                    foreach (var entry in entries)
                    {
                        string? value = entry.Value;
                        I? source = entry.Item;
                        Assumed.NotNull(value);
                        Assumed.NotNull(source);
                        string unescapedSourceIdentity = EscapingUtilities.UnescapeAll(value);
                        string normalizedSourceIdentity = FileUtilities.NormalizePathForComparisonNoThrow(
                            unescapedSourceIdentity, referenceFragments[i].ProjectDirectory);
                        NormalizedItemPath key = new(normalizedSourceIdentity);

                        if (!matchingSources.TryGetValue(key, out var sources))
                        {
                            sources = [with(StringComparer.OrdinalIgnoreCase)];
                            matchingSources.Add(key, sources);
                        }

                        sources[source.Key] = source;
                    }
                }

                _matchingItemsByPath = matchingSources;

                // The index owns the needed associations; expanded entries are no longer needed.
                _vectorStates = null;
                _itemIndexChecked = true;
            }

            private ref VectorState GetVectorState(int index)
            {
                _vectorStates ??= new VectorState[referenceFragments.Length];
                return ref _vectorStates[index];
            }

            private List<Expander<P, I>.TransformEntry>? GetVectorItems(int index)
            {
                ref VectorState state = ref GetVectorState(index);
                if (!state.Expanded)
                {
                    ItemVectorFragment fragment = referenceFragments[index];
                    var entries = expander.ExpandItemVector(fragment.ItemVector, fragment.ItemSpecLocation);
                    state.Entries = entries;
                    state.Expanded = true;
                }

                return state.Entries;
            }

            private struct VectorState
            {
                // A null entry list is a valid empty expansion, not an uninitialized cache.
                public bool Expanded;
                public List<Expander<P, I>.TransformEntry>? Entries;
                public FileSpecMatcherTester?[]? Matchers;
            }
        }
    }
}
