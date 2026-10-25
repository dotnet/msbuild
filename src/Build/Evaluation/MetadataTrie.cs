// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Stores ordered tuples of metadata values from the referenced source items.
/// </summary>
/// <remarks>
///  Each level represents one metadata name in the caller's sequence, and each edge is a complete
///  value for that name. Tuples such as <c>(shared, left)</c> and <c>(shared, right)</c> share their
///  first edge. Following a complete path requires all values to match the same source tuple;
///  independent sets of values could incorrectly combine metadata from different sources.
///  <para>
///   For example, sources with tuples <c>(a, x)</c> and <c>(b, y)</c> must not match a candidate
///   with <c>(a, y)</c>. Separate sets for each metadata name would accept both candidate values,
///   but no single source has that combination. A flat set of values would also lose the
///   association between a value and its metadata name. The trie preserves both the tuple
///   boundaries and the position of each value within the tuple.
///  </para>
///  <para>
///   Construction and lookup must use the same ordered metadata names. There are no terminal
///   markers because all tuples have the same width. Null or empty candidate values never match.
///   The tree captures values during creation and is only read afterward; subsequent source
///   item changes do not alter it. Leaf nodes have no child dictionary.
///  </para>
///  <para>
///   Metadata ordering affects prefix sharing and memory use. For <c>N</c> distinct two-value
///   tuples sharing their first value, the tree needs one shared node and <c>N</c> second-level
///   nodes, excluding the root. If those metadata names are reversed, the unique values come
///   first and each needs its own child for the shared value, producing <c>2N</c> nodes instead.
///   Thus, users can affect the tree's shape by ordering names in <c>MatchOnMetadata</c>.
///  </para>
///  <para>
///   A future optimization could choose an order that improves prefix sharing, but it would
///   need to apply the same permutation to source construction and candidate lookup. Reordering
///   only one side would compare different metadata names at corresponding levels. No such
///   reordering is currently performed.
///  </para>
/// </remarks>
internal sealed class MetadataTrie
{
    private readonly Dictionary<string, MetadataTrie>? _children;

    /// <summary>
    ///  The captured directory for path-like comparisons, or <see langword="null"/> when values are compared unchanged.
    /// </summary>
    private readonly string? _baseDirectory;

    private MetadataTrie(Dictionary<string, MetadataTrie>? children, string? baseDirectory)
    {
        _children = children;
        _baseDirectory = baseDirectory;
    }

    /// <summary>
    ///  Creates a trie from the captured source items using the specified metadata comparison policy.
    /// </summary>
    /// <typeparam name="P">The property representation used by the expansion context.</typeparam>
    /// <typeparam name="I">The item representation used by the expansion context.</typeparam>
    /// <param name="options">The comparison and normalization policy.</param>
    /// <param name="metadataNames">The metadata names, whose enumeration order must remain consistent during construction and lookup.</param>
    /// <param name="sourceFragments">The parsed source item expressions, in specification order.</param>
    /// <param name="expander">The captured item-list expansion context.</param>
    /// <returns>
    ///  The completed trie, including an empty trie when no source tuples contribute child edges.
    /// </returns>
    public static MetadataTrie Create<P, I>(
        MatchOnMetadataOptions options,
        ImmutableArray<string> metadataNames,
        ImmutableArray<ItemVectorFragment> sourceFragments,
        Expander<P, I> expander)
        where P : class, IProperty
        where I : class, IItem
    {
        var (comparer, baseDirectory) = GetComparisonPolicy(options);
        int metadataCount = metadataNames.Length;
        Dictionary<string, MetadataTrie>? rootChildren = null;

        // Keep this enumeration concrete to avoid boxing the immutable array or its enumerator.
        foreach (ItemVectorFragment fragment in sourceFragments)
        {
            var entries = expander.ExpandItemVector(fragment.ItemVector, fragment.ItemSpecLocation);

            if (entries is null)
            {
                continue;
            }

            foreach (var source in entries)
            {
                if (metadataCount == 0)
                {
                    continue;
                }

                I? sourceItem = source.Item;
                Assumed.NotNull(sourceItem);
                rootChildren ??= [with(comparer)];
                Dictionary<string, MetadataTrie> currentChildren = rootChildren;
                int level = 0;
                foreach (string name in metadataNames)
                {
                    string value = sourceItem.GetMetadataValue(name);
                    bool hasMoreMetadata = level < metadataCount - 1;
                    MetadataTrie child = GetOrAddChild(currentChildren, value, hasMoreMetadata, comparer, baseDirectory);
                    if (hasMoreMetadata)
                    {
                        Assumed.NotNull(child._children);
                        currentChildren = child._children;
                    }
                    level++;
                }
            }
        }

        return new(rootChildren, baseDirectory);
    }

    /// <summary>
    ///  Creates a trie from the captured source items using the specified metadata comparison policy.
    /// </summary>
    /// <typeparam name="P">The property representation used by the expansion context.</typeparam>
    /// <typeparam name="I">The item representation used by the expansion context.</typeparam>
    /// <param name="options">The comparison and normalization policy.</param>
    /// <param name="metadataNames">The metadata names, whose enumeration order must remain consistent during construction and lookup.</param>
    /// <param name="sourceFragments">The parsed source item expressions, in specification order.</param>
    /// <param name="expander">The captured item-list expansion context.</param>
    /// <returns>
    ///  The completed trie, including an empty trie when no source tuples contribute child edges.
    /// </returns>
    public static MetadataTrie Create<P, I>(
        MatchOnMetadataOptions options,
        HashSet<string> metadataNames,
        ImmutableArray<ItemVectorFragment> sourceFragments,
        Expander<P, I> expander)
        where P : class, IProperty
        where I : class, IItem
    {
        var (comparer, baseDirectory) = GetComparisonPolicy(options);
        int metadataCount = metadataNames.Count;
        Dictionary<string, MetadataTrie>? rootChildren = null;

        // The execution-time caller's HashSet enumerator also stays unboxed.
        foreach (ItemVectorFragment fragment in sourceFragments)
        {
            var entries = expander.ExpandItemVector(fragment.ItemVector, fragment.ItemSpecLocation);

            if (entries is null)
            {
                continue;
            }

            foreach (var source in entries)
            {
                if (metadataCount == 0)
                {
                    continue;
                }

                I? sourceItem = source.Item;
                Assumed.NotNull(sourceItem);
                rootChildren ??= [with(comparer)];
                Dictionary<string, MetadataTrie> currentChildren = rootChildren;
                int level = 0;
                foreach (string name in metadataNames)
                {
                    string value = sourceItem.GetMetadataValue(name);
                    bool hasMoreMetadata = level < metadataCount - 1;
                    MetadataTrie child = GetOrAddChild(currentChildren, value, hasMoreMetadata, comparer, baseDirectory);
                    if (hasMoreMetadata)
                    {
                        Assumed.NotNull(child._children);
                        currentChildren = child._children;
                    }
                    level++;
                }
            }
        }

        return new(rootChildren, baseDirectory);
    }

    private static (StringComparer Comparer, string? BaseDirectory) GetComparisonPolicy(MatchOnMetadataOptions options)
    {
        StringComparer comparer = options switch
        {
            MatchOnMetadataOptions.CaseSensitive => StringComparer.Ordinal,
            MatchOnMetadataOptions.CaseInsensitive => StringComparer.OrdinalIgnoreCase,
            _ => FileUtilities.PathComparison == StringComparison.OrdinalIgnoreCase
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal,
        };

        string? baseDirectory = null;
        if (options == MatchOnMetadataOptions.PathLike)
        {
            // Capture the project's thread-local directory rather than an unrelated process directory.
            // Construction and every later lookup must normalize against this same context.
            baseDirectory = FileUtilities.CurrentThreadWorkingDirectory is { Length: > 0 } threadWorkingDirectory
                ? threadWorkingDirectory
                : Environment.CurrentDirectory;
        }

        return (comparer, baseDirectory);
    }

    /// <summary>
    ///  Finds or creates one edge, allocating child storage only when the source tuple has another level.
    /// </summary>
    /// <param name="children">The current level's child dictionary.</param>
    /// <param name="value">The source item's metadata value.</param>
    /// <param name="hasMoreMetadata">Whether another metadata value follows this one.</param>
    /// <param name="comparer">The comparison policy for newly created child dictionaries.</param>
    /// <param name="baseDirectory">The captured directory for path-like values, or <see langword="null"/> for unchanged values.</param>
    /// <returns>
    ///  The child node for the normalized metadata value.
    /// </returns>
    private static MetadataTrie GetOrAddChild(
        Dictionary<string, MetadataTrie> children,
        string value,
        bool hasMoreMetadata,
        StringComparer comparer,
        string? baseDirectory)
    {
        string normalizedValue = NormalizeValue(value, baseDirectory);
        if (!children.TryGetValue(normalizedValue, out var child))
        {
            Dictionary<string, MetadataTrie>? childDictionary = null;
            if (hasMoreMetadata)
            {
                childDictionary = [with(comparer)];
            }

            child = new MetadataTrie(childDictionary, baseDirectory: null);
            children.Add(normalizedValue, child);
        }

        return child;
    }

    private static string NormalizeValue(string value, string? baseDirectory)
        => baseDirectory is null ? value : FileUtilities.NormalizePathForComparisonNoThrow(value, baseDirectory);

    internal bool Contains(IEnumerable<string?> metadata)
    {
        MetadataTrie current = this;

        foreach (string? m in metadata)
        {
            if (m is null || m.Length == 0)
            {
                return false;
            }

            string normalizedValue = NormalizeValue(m, _baseDirectory);
            if (current._children is null || !current._children.TryGetValue(normalizedValue, out var child))
            {
                return false;
            }

            current = child;
        }

        return true;
    }

    /// <summary>
    ///  Looks up an item's metadata tuple without allocating a projected sequence of values.
    /// </summary>
    /// <param name="item">The candidate item.</param>
    /// <param name="metadataNames">The same ordered metadata names used when constructing this trie.</param>
    /// <returns>
    ///  <see langword="true"/> when all nonempty values follow a stored tuple; otherwise, <see langword="false"/>.
    /// </returns>
    public bool Contains(IItem item, ImmutableArray<string> metadataNames)
    {
        MetadataTrie current = this;

        foreach (string name in metadataNames)
        {
            string value = item.GetMetadataValue(name);
            if (value.IsNullOrEmpty())
            {
                return false;
            }

            string normalizedValue = NormalizeValue(value, _baseDirectory);
            if (current._children is null || !current._children.TryGetValue(normalizedValue, out var child))
            {
                return false;
            }

            current = child;
        }

        return true;
    }
}
