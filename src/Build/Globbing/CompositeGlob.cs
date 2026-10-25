// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Build.Collections;

namespace Microsoft.Build.Globbing;

/// <summary>
///     A composite glob that returns a match for an input if any of its
///     inner globs match the input (disjunction).
/// </summary>
public class CompositeGlob : IMSBuildGlob
{
    internal static IMSBuildGlob Empty => NeverMatchingGlob.Instance;

    internal ImmutableArray<IMSBuildGlob> GlobArray { get; }

    /// <summary>
    /// The direct children of this composite
    /// </summary>
    public IEnumerable<IMSBuildGlob> Globs => GlobArray;

    /// <summary>
    ///     Constructor
    /// </summary>
    /// <param name="globs">Children globs. Input gets shallow cloned</param>
    public CompositeGlob(IEnumerable<IMSBuildGlob> globs)
        : this(globs.ToImmutableArray())
    {
    }

    /// <summary>
    ///     Constructor
    /// </summary>
    /// <param name="globs">Children globs. Input gets shallow cloned</param>
    public CompositeGlob(params IMSBuildGlob[] globs)
        : this(ImmutableArray.Create(globs))
    {
    }

    /// <summary>
    ///     Constructor
    /// </summary>
    /// <param name="glob1">First child glob.</param>
    /// <param name="glob2">Second child glob.</param>
    internal CompositeGlob(IMSBuildGlob glob1, IMSBuildGlob glob2)
        : this(ImmutableArray.Create(glob1, glob2))
    {
    }

    /// <summary>
    ///     Constructor
    /// </summary>
    /// <param name="globs">Children globs.</param>
    internal CompositeGlob(ImmutableArray<IMSBuildGlob> globs)
    {
        GlobArray = globs;
    }

    /// <inheritdoc />
    public bool IsMatch(string stringToMatch)
    {
        // Threadpools are a scarce resource in Visual Studio, do not use them.
        // return Globs.AsParallel().Any(g => g.IsMatch(stringToMatch));

        return GlobArray.Any(static (glob, str) => glob.IsMatch(str), stringToMatch);
    }

    /// <summary>
    ///  Combines immutable child globs without copying their backing array.
    /// </summary>
    /// <param name="globs">The initialized immutable array of child globs.</param>
    /// <returns>
    ///  A glob matching the disjunction of the supplied children.
    /// </returns>
    /// <remarks>
    ///  Empty inputs reuse the never-matching singleton; a single child is returned unchanged.
    /// </remarks>
    internal static IMSBuildGlob Create(ImmutableArray<IMSBuildGlob> globs)
        => globs.Length switch
        {
            0 => NeverMatchingGlob.Instance,
            1 => globs[0],
            _ => new CompositeGlob(globs),
        };

    /// <summary>
    ///     Creates an <see cref="IMSBuildGlob"/> that aggregates multiple other globs
    ///     such that the resulting glob matches when any inner glob matches (disjunction).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         When <paramref name="globs"/> contains no elements, a singleton glob is
    ///         returned that never matches, regardless of input.
    ///     </para>
    ///     <para>
    ///         When <paramref name="globs"/> contains one element, that single element is
    ///         returned directly. This avoids allocating a redundant wrapper instance.
    ///     </para>
    /// </remarks>
    /// <param name="globs">An enumeration of globs to compose.</param>
    /// <returns>The logical disjunction of the input globs.</returns>
    public static IMSBuildGlob Create(IEnumerable<IMSBuildGlob> globs)
    {
        ArgumentNullException.ThrowIfNull(globs);

        if (globs is ImmutableArray<IMSBuildGlob> immutableGlobs)
        {
            return Create(immutableGlobs);
        }

        // Use explicit enumeration so we can do minimal work in the case
        // that the input set of globs is either empty or only contains a
        // single item.
        using var enumerator = globs.GetEnumerator();

        if (!enumerator.MoveNext())
        {
            // The input is empty, so return our singleton that doesn't
            // match anything.
            return NeverMatchingGlob.Instance;
        }

        var first = enumerator.Current;

        if (!enumerator.MoveNext())
        {
            // The input contains only a single glob. Disjunction has no
            // effect on a single input, so return it directly and avoid
            // allocating a CompositeGlob instance.
            return first;
        }

        // We have more than one input glob, to add them all to a builder
        // and create a new CompositeGlob.
        using RefArrayBuilder<IMSBuildGlob> builder = new(initialCapacity: 4);

        builder.Add(first);
        builder.Add(enumerator.Current);

        while (enumerator.MoveNext())
        {
            builder.Add(enumerator.Current);
        }

        return new CompositeGlob(builder.ToImmutable());
    }

    /// <summary>
    ///    A glob that never returns a match.
    /// </summary>
    private sealed class NeverMatchingGlob : IMSBuildGlob
    {
        /// <summary>
        ///    Singleton instance of this type.
        /// </summary>
        public static NeverMatchingGlob Instance { get; } = new();

        public bool IsMatch(string stringToMatch) => false;
    }
}
