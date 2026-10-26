// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Stores the original text and property-expanded syntax of an item specification.
/// </summary>
/// <param name="text">The original escaped attribute text, before property expansion.</param>
/// <param name="fragments">The parsed, property-expanded fragments in specification order.</param>
/// <remarks>
///  Specifications can contain literals, globs, and item vectors. Parsing does not resolve vectors
///  or expand filesystem globs. Consumers supply their own expansion context and own derived matching state;
///  the specification and its fragments do not retain an expander or evaluation caches.
/// </remarks>
internal sealed class ItemSpec(string text, ImmutableArray<ItemSpecFragment> fragments)
{
    /// <summary>
    ///  Gets the original escaped specification text, before property expansion.
    /// </summary>
    public string Text { get; } = text;

    /// <summary>
    ///  Gets the property-expanded fragments in specification order.
    /// </summary>
    public ImmutableArray<ItemSpecFragment> Fragments { get; } = fragments;

    /// <summary>
    ///  Determines whether this specification is a single, untransformed vector of <paramref name="itemType"/>.
    /// </summary>
    /// <param name="itemType">The item type the vector must reference, compared case-insensitively.</param>
    /// <returns>
    ///  <see langword="true"/> if the specification is a simple vector of the requested type;
    ///  otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  This is a syntax-only test and does not resolve the vector. Item functions and transforms
    ///  are excluded, but an explicit separator currently does not disqualify the vector.
    /// </remarks>
    public bool IsSimpleItemVector(string itemType)
    {
        if (Fragments is not [ItemVectorFragment vector])
        {
            return false;
        }

        if (!vector.ItemVector.ItemType.Equals(itemType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // @(X->Something(...)) can return different identities from a bare @(X).
        // UNDONE: Explicit separators can also change identities, but currently still qualify as simple.
        return vector.ItemVector.Captures is null;
    }

    /// <summary>
    ///  Expands properties and creates a specification without resolving item vectors.
    /// </summary>
    /// <typeparam name="P">The property representation used by the expander.</typeparam>
    /// <typeparam name="I">The item representation used by the expander.</typeparam>
    /// <param name="itemSpec">The original escaped attribute text; an empty string produces no fragments.</param>
    /// <param name="expander">The property-expansion context used during parsing.</param>
    /// <param name="itemSpecLocation">The diagnostic location of the specification.</param>
    /// <param name="projectDirectory">The base directory used to interpret relative paths in the fragments.</param>
    /// <returns>
    ///  A specification retaining the original <paramref name="itemSpec"/> text and its parsed fragments.
    /// </returns>
    /// <exception cref="Exceptions.InvalidProjectFileException">
    ///  Property expansion fails or a recognized item vector is embedded in another fragment.
    /// </exception>
    public static ItemSpec Parse<P, I>(
        string itemSpec,
        Expander<P, I> expander,
        IElementLocation itemSpecLocation,
        string projectDirectory)
        where P : class, IProperty
        where I : class, IItem
        => new(itemSpec, ParseFragments(itemSpec, expander, itemSpecLocation, projectDirectory));

    /// <summary>
    ///  Expands properties and parses a specification without resolving its item vectors.
    /// </summary>
    /// <typeparam name="P">The property representation used by the expander.</typeparam>
    /// <typeparam name="I">The item representation used by the expander.</typeparam>
    /// <param name="itemSpec">The original escaped attribute text; an empty string produces no fragments.</param>
    /// <param name="expander">The property-expansion context used during parsing.</param>
    /// <param name="itemSpecLocation">The diagnostic location of the specification.</param>
    /// <param name="projectDirectory">The base directory used to interpret relative paths in the fragments.</param>
    /// <returns>
    ///  The parsed fragments in specification order, or an empty array when no nonempty fragments remain.
    /// </returns>
    /// <exception cref="Exceptions.InvalidProjectFileException">
    ///  Property expansion fails or a recognized item vector is embedded in another fragment.
    /// </exception>
    /// <remarks>
    ///  Properties are expanded before fragment classification. Fragments are split at semicolons
    ///  outside item-vector expressions; surrounding whitespace and empty fragments are discarded.
    ///  Literal and vector text remains escaped, while glob patterns are unescaped.
    ///  Empty and single-fragment results do not use a temporary collection buffer.
    /// </remarks>
    public static ImmutableArray<ItemSpecFragment> ParseFragments<P, I>(
        string itemSpec,
        Expander<P, I> expander,
        IElementLocation itemSpecLocation,
        string projectDirectory)
        where P : class, IProperty
        where I : class, IItem
    {
        if (itemSpec.Length == 0)
        {
            return [];
        }

        string expandedItemSpec = expander.ExpandIntoStringLeaveEscaped(
            itemSpec,
            ExpanderOptions.ExpandProperties,
            itemSpecLocation);

        using var splits = ExpressionShredder.SplitSemiColonSeparatedList(expandedItemSpec).GetEnumerator();
        if (!splits.MoveNext())
        {
            return [];
        }

        ItemSpecFragment firstFragment = CreateFragment(splits.Current, itemSpecLocation, projectDirectory);
        if (!splits.MoveNext())
        {
            return [firstFragment];
        }

        using RefArrayBuilder<ItemSpecFragment> fragments = default;
        fragments.Add(firstFragment);

        do
        {
            fragments.Add(CreateFragment(splits.Current, itemSpecLocation, projectDirectory));
        }
        while (splits.MoveNext());

        return fragments.ToImmutable();

        static ItemSpecFragment CreateFragment(string text, IElementLocation location, string projectDirectory)
        {
            if (Expander<P, I>.TryExpandSingleItemVectorExpression(
                text,
                ExpanderOptions.ExpandItems,
                location,
                out ExpressionShredder.ItemExpressionCapture itemVector))
            {
                return new ItemVectorFragment(itemVector, text, location, projectDirectory);
            }
            else if (!FileMatcher.HasWildcards(text))
            {
                // No real wildcards means the original, already escaped string is a literal.
                return new ValueFragment(text, projectDirectory);
            }
            else if (EscapingUtilities.ContainsEscapedWildcards(text))
            {
                // '*' is an illegal character to have in a filename.
                // todo: file-system assumption on legal path characters: https://github.com/dotnet/msbuild/issues/781
                // Preserve the original string as a literal, even when it also contains unescaped wildcards.
                return new ValueFragment(text, projectDirectory);
            }
            else
            {
                string filespecUnescaped = EscapingUtilities.UnescapeAll(text);
                return new GlobFragment(filespecUnescaped, projectDirectory);
            }
        }
    }

    /// <summary>
    ///  Expands properties and parses only item-vector fragments without resolving them.
    /// </summary>
    /// <typeparam name="P">The property representation used by the expander.</typeparam>
    /// <typeparam name="I">The item representation used by the expander.</typeparam>
    /// <param name="itemSpec">The original escaped attribute text; an empty string produces no fragments.</param>
    /// <param name="expander">The property-expansion context used during parsing.</param>
    /// <param name="itemSpecLocation">The diagnostic location of the specification.</param>
    /// <param name="projectDirectory">The base directory used to interpret relative paths in the fragments.</param>
    /// <returns>
    ///  The parsed vectors in specification order, or an empty array when no nonempty fragments remain.
    /// </returns>
    /// <exception cref="Exceptions.InvalidProjectFileException">
    ///  Property expansion fails or a nonempty fragment is not a standalone item vector.
    /// </exception>
    /// <remarks>
    ///  Every nonempty fragment must be an item vector; literals and globs are rejected rather than
    ///  filtered out. Empty fragments are discarded, and semicolons inside vector expressions are preserved.
    ///  The returned vectors retain their syntax and origin, not the supplied <paramref name="expander"/>.
    ///  Empty and single-vector results do not use a temporary collection buffer.
    /// </remarks>
    public static ImmutableArray<ItemVectorFragment> ParseItemVectorFragments<P, I>(
        string itemSpec,
        Expander<P, I> expander,
        IElementLocation itemSpecLocation,
        string projectDirectory)
        where P : class, IProperty
        where I : class, IItem
    {
        if (itemSpec.Length == 0)
        {
            return [];
        }

        string expandedItemSpec = expander.ExpandIntoStringLeaveEscaped(
            itemSpec,
            ExpanderOptions.ExpandProperties,
            itemSpecLocation);

        using var splits = ExpressionShredder.SplitSemiColonSeparatedList(expandedItemSpec).GetEnumerator();
        if (!splits.MoveNext())
        {
            return [];
        }

        ItemVectorFragment firstFragment = CreateFragment(splits.Current, itemSpecLocation, projectDirectory);
        if (!splits.MoveNext())
        {
            return [firstFragment];
        }

        using RefArrayBuilder<ItemVectorFragment> fragments = default;
        fragments.Add(firstFragment);

        do
        {
            fragments.Add(CreateFragment(splits.Current, itemSpecLocation, projectDirectory));
        }
        while (splits.MoveNext());

        return fragments.ToImmutable();

        static ItemVectorFragment CreateFragment(string text, IElementLocation location, string projectDirectory)
        {
            if (!Expander<P, I>.TryExpandSingleItemVectorExpression(
                text,
                ExpanderOptions.ExpandItems,
                location,
                out ExpressionShredder.ItemExpressionCapture itemVector))
            {
                ProjectErrorUtilities.ThrowInvalidProject(
                    ElementLocation.Empty,
                    "OM_MatchOnMetadataIsRestrictedToReferencedItems");
            }

            return new ItemVectorFragment(itemVector, text, location, projectDirectory);
        }
    }

    /// <summary>
    ///  Returns the original specification text.
    /// </summary>
    /// <returns>
    ///  The value of <see cref="Text"/>.
    /// </returns>
    public override string ToString()
        => Text;
}

/// <summary>
///  Stores one parsed component of a property-expanded item specification.
/// </summary>
/// <param name="text">The fragment text after property expansion.</param>
/// <param name="projectDirectory">The base directory used to interpret relative paths.</param>
internal abstract class ItemSpecFragment(string text, string projectDirectory)
{
    /// <summary>
    ///  Gets the fragment text after property expansion.
    /// </summary>
    /// <remarks>
    ///  <see cref="ValueFragment"/> and <see cref="ItemVectorFragment"/> retain escaped text.
    ///  <see cref="GlobFragment"/> stores an unescaped pattern.
    /// </remarks>
    public string Text { get; } = text;

    /// <summary>
    ///  Gets the base directory used to interpret relative paths in this fragment.
    /// </summary>
    internal string ProjectDirectory { get; } = projectDirectory;
}

/// <summary>
///  Stores an item vector's syntax and origin without owning its expansion context or results.
/// </summary>
/// <param name="itemVector">The parsed item type, transform chain, and optional separator.</param>
/// <param name="text">The escaped vector expression after property expansion.</param>
/// <param name="itemSpecLocation">The location used for diagnostics when the vector is resolved.</param>
/// <param name="projectDirectory">The base directory used to interpret relative paths.</param>
internal sealed class ItemVectorFragment(
    ExpressionShredder.ItemExpressionCapture itemVector,
    string text,
    IElementLocation itemSpecLocation,
    string projectDirectory) : ItemSpecFragment(text, projectDirectory)
{
    /// <summary>
    ///  Gets the parsed item type, transform chain, and optional separator.
    /// </summary>
    public ExpressionShredder.ItemExpressionCapture ItemVector { get; } = itemVector;

    /// <summary>
    ///  Gets the location used for diagnostics when the vector is resolved.
    /// </summary>
    public IElementLocation ItemSpecLocation { get; } = itemSpecLocation;
}

/// <summary>
///  Represents a fragment classified as a literal value rather than a glob or item vector.
/// </summary>
/// <param name="text">The escaped literal value after property expansion.</param>
/// <param name="projectDirectory">The base directory used to interpret relative paths.</param>
internal sealed class ValueFragment(string text, string projectDirectory)
    : ItemSpecFragment(text, projectDirectory)
{
}

/// <summary>
///  Represents an unescaped filesystem glob parsed from an item specification.
/// </summary>
/// <param name="text">The unescaped glob pattern.</param>
/// <param name="projectDirectory">The base directory used to interpret relative paths.</param>
internal sealed class GlobFragment(string text, string projectDirectory)
    : ItemSpecFragment(text, projectDirectory)
{
    /// <summary>
    ///  Gets whether the pattern starts with a rooted recursive-directory wildcard such as <c>/**/</c>.
    /// </summary>
    /// <remarks>
    ///  Both forward slashes and backslashes are recognized as separators.
    /// </remarks>
    public bool IsFullFileSystemScan
        => Text.Length >= 4
        && FileUtilities.IsAnySlash(Text[0])
        && Text[1] == '*'
        && Text[2] == '*'
        && FileUtilities.IsAnySlash(Text[3]);
}
