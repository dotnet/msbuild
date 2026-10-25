// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Microsoft.Build.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.Globbing;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Matches parsed syntax against one fixed expansion context, resolving and compiling fragments on demand.
/// </summary>
/// <typeparam name="P">The property representation used by the expansion context.</typeparam>
/// <typeparam name="I">The item representation used by the expansion context.</typeparam>
/// <remarks>
///  Each operation or query owns its matcher. Parsed syntax can be shared, but resolution caches cannot.
///  Construction does not resolve vectors or compile patterns; only visited fragments are initialized.
///  The supplied context must continue to represent the same item lists throughout the matcher's lifetime;
///  changing its providers does not invalidate cached results. Instances are not thread-safe.
/// </remarks>
internal sealed partial class ItemSpecMatcher<P, I>
    where P : class, IProperty
    where I : class, IItem
{
    /// <summary>
    ///  The fixed context used when an item vector is first resolved.
    /// </summary>
    private readonly Expander<P, I> _expander;

    /// <summary>
    ///  Per-fragment matching and resolution state, allocated on first demand.
    /// </summary>
    private FragmentState[]? _states;

    /// <summary>
    ///  The complete glob projection, or <see langword="null"/> before it is successfully created.
    /// </summary>
    private IMSBuildGlob? _glob;

    /// <summary>
    ///  Gets the parsed specification whose derived state belongs to this matcher.
    /// </summary>
    public ItemSpec ItemSpec { get; }

    /// <summary>
    ///  Creates matching state for a parsed specification and a fixed expansion context.
    /// </summary>
    /// <param name="itemSpec">The parsed syntax to match without modifying it.</param>
    /// <param name="expander">The context used to resolve vectors, typically backed by bookmarked item lists.</param>
    public ItemSpecMatcher(ItemSpec itemSpec, Expander<P, I> expander)
    {
        Assumed.NotNull(itemSpec);
        Assumed.NotNull(expander);

        ItemSpec = itemSpec;
        _expander = expander;
    }

    /// <summary>
    ///  Determines whether any fragment matches the candidate's evaluated identity.
    /// </summary>
    /// <param name="item">The item whose <see cref="IItem.EvaluatedInclude"/> is matched.</param>
    /// <returns>
    ///  <see langword="true"/> if a literal, glob, or resolved vector value matches;
    ///  otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  Fragments are visited in specification order and matching stops at the first success.
    ///  Later vectors are not resolved unless matching reaches them.
    /// </remarks>
    public bool MatchesItem(IItem item)
    {
        string evaluatedInclude = item.EvaluatedInclude;
        var fragments = ItemSpec.Fragments;
        for (int i = 0; i < fragments.Length; i++)
        {
            if (fragments[i] is ItemVectorFragment vector)
            {
                ValueState[] values = GetValues(i, vector);
                for (int j = 0; j < values.Length; j++)
                {
                    if (GetValueMatcher(values, j, vector.ProjectDirectory).IsMatch(evaluatedInclude))
                    {
                        return true;
                    }
                }
            }
            else if (GetFragmentMatcher(i).IsMatch(evaluatedInclude))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///  Finds matching syntax fragments and counts all matching occurrences, including vector duplicates.
    /// </summary>
    /// <param name="itemToMatch">The identity to match.</param>
    /// <param name="matches">The total number of matching occurrences, including duplicate vector values.</param>
    /// <returns>
    ///  The original matching fragments in specification order, or an empty array when none match.
    /// </returns>
    /// <remarks>
    ///  All fragments are inspected to compute the complete count. Each matching fragment appears once
    ///  in the result, but <paramref name="matches"/> can exceed the result's length when a vector
    ///  contributes several matching values.
    /// </remarks>
    public ImmutableArray<ItemSpecFragment> FragmentsMatchingItem(string itemToMatch, out int matches)
    {
        var fragments = ItemSpec.Fragments;
        using RefArrayBuilder<ItemSpecFragment> result = default;
        matches = 0;

        for (int i = 0; i < fragments.Length; i++)
        {
            ItemSpecFragment fragment = fragments[i];
            int fragmentMatches = 0;
            if (fragment is ItemVectorFragment vector)
            {
                ValueState[] values = GetValues(i, vector);
                for (int j = 0; j < values.Length; j++)
                {
                    if (GetValueMatcher(values, j, vector.ProjectDirectory).IsMatch(itemToMatch))
                    {
                        fragmentMatches++;
                    }
                }
            }
            else if (GetFragmentMatcher(i).IsMatch(itemToMatch))
            {
                fragmentMatches = 1;
            }

            if (fragmentMatches > 0)
            {
                result.Add(fragment);
                matches += fragmentMatches;
            }
        }

        return result.ToImmutable();
    }

    /// <summary>
    ///  Matches this specification against an index of already-normalized candidate paths.
    /// </summary>
    /// <param name="normalizedItemPaths">The candidate paths and the comparer used for membership checks.</param>
    /// <returns>
    ///  The subset of indexed paths matched by this specification, or an empty set when none match.
    /// </returns>
    /// <remarks>
    ///  Literals and resolved vector values use normalized-path membership checks. Glob fragments test
    ///  the indexed paths directly rather than enumerating the filesystem.
    /// </remarks>
    public ISet<NormalizedItemPath> IntersectsWith(HashSet<NormalizedItemPath> normalizedItemPaths)
    {
        HashSet<NormalizedItemPath>? matches = null;
        var fragments = ItemSpec.Fragments;
        for (int i = 0; i < fragments.Length; i++)
        {
            ItemSpecFragment fragment = fragments[i];
            if (fragment is GlobFragment)
            {
                foreach (NormalizedItemPath path in normalizedItemPaths)
                {
                    if (GetFragmentMatcher(i).IsMatchNormalized(path.Value))
                    {
                        matches ??= [with(normalizedItemPaths.Comparer)];
                        matches.Add(path);
                    }
                }
            }
            else if (fragment is ItemVectorFragment vector)
            {
                foreach (ValueState value in GetValues(i, vector))
                {
                    AddIndexedMatch(value.Text, vector.ProjectDirectory, normalizedItemPaths, ref matches);
                }
            }
            else
            {
                AddIndexedMatch(fragment.Text, fragment.ProjectDirectory, normalizedItemPaths, ref matches);
            }
        }

        return matches is not null
            ? matches
            : FrozenSet<NormalizedItemPath>.Empty;
    }

    /// <summary>
    ///  Creates and caches a glob representing the complete specification.
    /// </summary>
    /// <returns>
    ///  A glob matching any projected fragment, or a never-matching glob for an empty projection.
    /// </returns>
    /// <remarks>
    ///  The first call resolves the vectors needed to construct the complete projection; later calls
    ///  reuse it. Glob fragments become predicates, not enumerated file lists.
    /// </remarks>
    public IMSBuildGlob ToMSBuildGlob()
    {
        return _glob ??= ComputeGlob(ItemSpec.Fragments);

        IMSBuildGlob ComputeGlob(ImmutableArray<ItemSpecFragment> fragments)
            => fragments.Length switch
            {
                0 => CompositeGlob.Empty,
                1 => CreateFragmentGlob(0),
                _ => ComputeCompositeGlob(fragments),
            };

        CompositeGlob ComputeCompositeGlob(ImmutableArray<ItemSpecFragment> fragments)
        {
            IMSBuildGlob[] globs = new IMSBuildGlob[fragments.Length];

            for (int i = 0; i < fragments.Length; i++)
            {
                globs[i] = CreateFragmentGlob(i);
            }

            // Transfer ownership without copying; no writes occur after wrapping the array.
            return new CompositeGlob(ImmutableCollectionsMarshal.AsImmutableArray(globs));
        }
    }

    /// <summary>
    ///  Adds fragment text to a set, substituting resolved values for item vectors.
    /// </summary>
    /// <param name="builder">The destination set builder, whose existing entries and comparer are preserved.</param>
    /// <remarks>
    ///  All vectors are resolved, reusing this matcher's cache.
    ///  Literal and resolved vector values remain escaped; glob patterns were unescaped during parsing.
    ///  Glob patterns are added without enumerating files.
    /// </remarks>
    public void AddFragmentStringsTo(ImmutableHashSet<string>.Builder builder)
    {
        var fragments = ItemSpec.Fragments;
        for (int i = 0; i < fragments.Length; i++)
        {
            ItemSpecFragment fragment = fragments[i];
            if (fragment is ItemVectorFragment vector)
            {
                ValueState[] values = GetValues(i, vector);
                for (int j = 0; j < values.Length; j++)
                {
                    builder.Add(values[j].Text);
                }
            }
            else
            {
                builder.Add(fragment.Text);
            }
        }
    }

    /// <summary>
    ///  Gets mutable cache storage for a syntax fragment, allocating the state array on demand.
    /// </summary>
    /// <param name="index">The fragment's index in the parsed specification.</param>
    /// <returns>
    ///  A reference to the fragment's cache entry.
    /// </returns>
    private ref FragmentState GetState(int index)
    {
        _states ??= new FragmentState[ItemSpec.Fragments.Length];
        return ref _states[index];
    }

    /// <summary>
    ///  Gets or compiles the matcher for a literal or glob fragment.
    /// </summary>
    /// <param name="index">The non-vector fragment's index in the parsed specification.</param>
    /// <returns>
    ///  A reference to the cached matcher.
    /// </returns>
    private ref FileSpecMatcherTester GetFragmentMatcher(int index)
    {
        ref FragmentState state = ref GetState(index);
        if (!state.MatcherInitialized)
        {
            ItemSpecFragment fragment = ItemSpec.Fragments[index];
            state.Matcher = FileSpecMatcherTester.Parse(fragment.ProjectDirectory, fragment.Text);
            state.MatcherInitialized = true;
        }

        return ref state.Matcher;
    }

    /// <summary>
    ///  Gets or compiles a matcher for one resolved vector value.
    /// </summary>
    /// <param name="values">The vector's cached resolution results.</param>
    /// <param name="index">The value's index in <paramref name="values"/>.</param>
    /// <param name="directory">The base directory used to interpret the value as a path or pattern.</param>
    /// <returns>
    ///  A reference to the value's cached matcher.
    /// </returns>
    private static ref FileSpecMatcherTester GetValueMatcher(ValueState[] values, int index, string directory)
    {
        ref ValueState value = ref values[index];
        if (!value.MatcherInitialized)
        {
            value.Matcher = FileSpecMatcherTester.Parse(directory, value.Text);
            value.MatcherInitialized = true;
        }

        return ref value.Matcher;
    }

    /// <summary>
    ///  Resolves a vector on first use and returns its cached escaped values.
    /// </summary>
    /// <param name="index">The vector's index in the parsed specification.</param>
    /// <param name="fragment">The vector at <paramref name="index"/>.</param>
    /// <returns>
    ///  The resolved value cache, or an empty array when the vector produces no values.
    /// </returns>
    /// <remarks>
    ///  Results are returned only after expansion succeeds. Each entry retains escaped text and
    ///  lazy matching state, not its originating item.
    /// </remarks>
    private ValueState[] GetValues(int index, ItemVectorFragment fragment)
    {
        ref FragmentState state = ref GetState(index);
        if (state.Values is null)
        {
            var entries = _expander.ExpandItemVector(fragment.ItemVector, fragment.ItemSpecLocation);

            ValueState[] values = [];
            if (entries is not null && entries.Count > 0)
            {
                values = new ValueState[entries.Count];
                for (int i = 0; i < values.Length; i++)
                {
                    string? text = entries[i].Value;
                    Assumed.NotNull(text);
                    values[i] = new ValueState(text);
                }
            }

            // Publish only after expansion succeeds, so failures cannot leave a partial cache.
            state.Values = values;
        }

        return state.Values;
    }

    /// <summary>
    ///  Creates the glob projection for one fragment, resolving vector values as needed.
    /// </summary>
    /// <param name="index">The fragment's index in the parsed specification.</param>
    /// <returns>
    ///  A pattern, the disjunction of a vector's expanded patterns, or <see cref="CompositeGlob.Empty"/>
    ///  for an empty vector.
    /// </returns>
    private IMSBuildGlob CreateFragmentGlob(int index)
    {
        ItemSpecFragment fragment = ItemSpec.Fragments[index];
        if (fragment is ItemVectorFragment vector)
        {
            ValueState[] values = GetValues(index, vector);

            if (values.Length == 0)
            {
                return CompositeGlob.Empty;
            }

            if (values.Length == 1)
            {
                return MSBuildGlob.Parse(vector.ProjectDirectory, EscapingUtilities.UnescapeAll(values[0].Text));
            }

            IMSBuildGlob[] globs = new IMSBuildGlob[values.Length];

            for (int i = 0; i < values.Length; i++)
            {
                globs[i] = MSBuildGlob.Parse(vector.ProjectDirectory, EscapingUtilities.UnescapeAll(values[i].Text));
            }

            // Transfer ownership without copying; no writes occur after wrapping the array.
            return CompositeGlob.Create(ImmutableCollectionsMarshal.AsImmutableArray(globs));
        }

        return MSBuildGlob.Parse(fragment.ProjectDirectory, EscapingUtilities.UnescapeAll(fragment.Text));
    }

    /// <summary>
    ///  Adds a literal or resolved identity when its normalized path exists in the candidate index.
    /// </summary>
    /// <param name="text">The escaped identity to normalize and match.</param>
    /// <param name="directory">The base directory used to interpret relative paths.</param>
    /// <param name="indexedPaths">The normalized candidate paths and their membership comparer.</param>
    /// <param name="matches">The result set, allocated with the index's comparer on the first match.</param>
    private static void AddIndexedMatch(
        string text,
        string directory,
        HashSet<NormalizedItemPath> indexedPaths,
        ref HashSet<NormalizedItemPath>? matches)
    {
        string unescaped = EscapingUtilities.UnescapeAll(text);
        NormalizedItemPath key = new(FileUtilities.NormalizePathForComparisonNoThrow(unescaped, directory));
        if (indexedPaths.Contains(key))
        {
            matches ??= [with(indexedPaths.Comparer)];
            matches.Add(key);
        }
    }
}
