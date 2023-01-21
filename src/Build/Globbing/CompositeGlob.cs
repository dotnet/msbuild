// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Globbing
{
    /// <summary>
    ///     A composite glob that returns a match for an input if any of its
    ///     inner globs match the input (disjunction).
    /// </summary>
    public class CompositeGlob : IMSBuildGlob
    {
        private readonly ImmutableArray<IMSBuildGlob> _globs;

        /// <summary>
        /// The direct children of this composite
        /// </summary>
        public IEnumerable<IMSBuildGlob> Globs => _globs;

        /// <summary>
        ///     Constructor
        /// </summary>
        /// <param name="globs">Children globs. Input gets shallow cloned</param>
        public CompositeGlob(IEnumerable<IMSBuildGlob> globs)
            : this(globs.ToImmutableArray())
        { }

        /// <summary>
        ///     Constructor
        /// </summary>
        /// <param name="globs">Children globs. Input gets shallow cloned</param>
        public CompositeGlob(params IMSBuildGlob[] globs)
            : this(ImmutableArray.Create(globs))
        { }

        /// <summary>
        ///     Constructor
        /// </summary>
        /// <param name="glob1">First child glob.</param>
        /// <param name="glob2">Second child glob.</param>
        internal CompositeGlob(IMSBuildGlob glob1, IMSBuildGlob glob2)
            : this(ImmutableArray.Create(glob1, glob2))
        { }

        /// <summary>
        ///     Constructor
        /// </summary>
        /// <param name="globs">Children globs.</param>
        private CompositeGlob(ImmutableArray<IMSBuildGlob> globs)
        {
            _globs = globs;
        }

        /// <inheritdoc />
        public bool IsMatch(string stringToMatch)
        {
            // Threadpools are a scarce resource in Visual Studio, do not use them.
            // return Globs.AsParallel().Any(g => g.IsMatch(stringToMatch));

            return _globs.Any(static (glob, str) => glob.IsMatch(str), stringToMatch);
        }

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
            ErrorUtilities.VerifyThrowArgumentNull(globs, nameof(globs));

            if (globs is ImmutableArray<IMSBuildGlob> immutableGlobs)
            {
                // Avoid allocations in the case that the input is an ImmutableArray
                return immutableGlobs.Length switch
                {
                    0 => NeverMatchingGlob.Instance,
                    1 => immutableGlobs[0],
                    _ => new CompositeGlob(immutableGlobs)
                };
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

            var builder = ImmutableArray.CreateBuilder<IMSBuildGlob>();

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
}
