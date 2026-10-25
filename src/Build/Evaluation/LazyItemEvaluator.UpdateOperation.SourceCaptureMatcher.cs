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
        ///  Matches target identities and captures source items used to expand qualified metadata.
        /// </summary>
        /// <param name="referenceFragments">The nonempty sequence of item-reference fragments, in specification order.</param>
        /// <param name="nonReferenceFragments">The literal and glob fragments that can match without capturing source items.</param>
        /// <param name="expander">The fixed expansion context for the captured source lists.</param>
        private sealed class SourceCaptureMatcher(
            ImmutableArray<ItemVectorFragment> referenceFragments,
            ImmutableArray<ItemSpecFragment> nonReferenceFragments,
            Expander<P, I> expander)
        {
            private bool _sourceIndexChecked;
            private SourceState[]? _sourceStates;
            private FileSpecMatcherTester?[]? _nonReferenceMatchers;

            /// <summary>
            ///  The completed source index, or <see langword="null"/> before checking eligibility or when ordered matching is required.
            /// </summary>
            private Dictionary<NormalizedItemPath, Dictionary<string, I>>? _matchingSources;

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

                PrepareSourceIndex();
                if (_matchingSources is not null)
                {
                    if (_matchingSources.Count == 0)
                    {
                        return new MatchResult(isMatch, capturedItemsFromReferencedItemTypes: null);
                    }

                    string directory = referenceFragments[0].ProjectDirectory;

                    // The item's cached key may be rooted elsewhere than the Update's matching directory.
                    NormalizedItemPath key = string.Equals(item.ProjectDirectory, directory, StringComparison.Ordinal)
                        ? itemData.NormalizedItemValue
                        : new NormalizedItemPath(FileUtilities.NormalizePathForComparisonNoThrow(item.EvaluatedInclude, directory));
                    bool sourceMatched = _matchingSources.TryGetValue(key, out var sources);
                    return new MatchResult(isMatch || sourceMatched, sources);
                }

                Dictionary<string, I>? capturedItems = null;
                for (int i = 0; i < referenceFragments.Length; i++)
                {
                    var sources = GetSources(i);
                    if (sources is null)
                    {
                        continue;
                    }

                    ref SourceState state = ref GetSourceState(i);
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
                            capturedItems ??= [with(StringComparer.OrdinalIgnoreCase)];
                            capturedItems[source.Key] = source;
                        }
                    }
                }

                return new MatchResult(isMatch, capturedItems);
            }

            /// <summary>
            ///  Indexes literal transformed identities, retaining the last matching source of each item type.
            /// </summary>
            /// <remarks>
            ///  Wildcard sources keep ordered matching for the whole operation so literal and wildcard winners
            ///  cannot be reordered. Completed source tables are read-only and shared between target items.
            ///  Sources with different matching directories also keep ordered matching because a single
            ///  normalized target lookup cannot represent all of those directory contexts.
            /// </remarks>
            private void PrepareSourceIndex()
            {
                if (_sourceIndexChecked)
                {
                    return;
                }

                string directory = referenceFragments[0].ProjectDirectory;
                for (int i = 0; i < referenceFragments.Length; i++)
                {
                    ItemVectorFragment fragment = referenceFragments[i];
                    if (!string.Equals(directory, fragment.ProjectDirectory, StringComparison.Ordinal))
                    {
                        _sourceIndexChecked = true;
                        return;
                    }

                    var sources = GetSources(i);
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
                            _sourceIndexChecked = true;
                            return;
                        }
                    }
                }

                Dictionary<NormalizedItemPath, Dictionary<string, I>> matchingSources = [];
                for (int i = 0; i < referenceFragments.Length; i++)
                {
                    var entries = GetSources(i);
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

                _matchingSources = matchingSources;

                // The index owns the needed associations; expanded entries are no longer needed.
                _sourceStates = null;
                _sourceIndexChecked = true;
            }

            private ref SourceState GetSourceState(int index)
            {
                _sourceStates ??= new SourceState[referenceFragments.Length];
                return ref _sourceStates[index];
            }

            private List<Expander<P, I>.TransformEntry>? GetSources(int index)
            {
                ref SourceState state = ref GetSourceState(index);
                if (!state.Expanded)
                {
                    ItemVectorFragment fragment = referenceFragments[index];
                    var entries = expander.ExpandItemVector(fragment.ItemVector, fragment.ItemSpecLocation);
                    state.Entries = entries;
                    state.Expanded = true;
                }

                return state.Entries;
            }

            private struct SourceState
            {
                // A null entry list is a valid empty expansion, not an uninitialized cache.
                public bool Expanded;
                public List<Expander<P, I>.TransformEntry>? Entries;
                public FileSpecMatcherTester?[]? Matchers;
            }
        }
    }
}
