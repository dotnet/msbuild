// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Microsoft.Build.Globbing
{
    /// <summary>
    ///     A Composite glob
    /// </summary>
    public class CompositeGlob : IMSBuildGlob
    {
        /// <summary>
        /// The direct children of this composite
        /// </summary>
        public IEnumerable<IMSBuildGlob> Globs { get; }

        /// <summary>
        ///     Constructor
        /// </summary>
        /// <param name="globs">Children globs. Input gets shallow cloned</param>
        public CompositeGlob(IEnumerable<IMSBuildGlob> globs)
        {
            // ImmutableArray also does this check, but copied it here just in case they remove it
            if (globs is ImmutableArray<IMSBuildGlob>)
            {
                Globs = (ImmutableArray<IMSBuildGlob>)globs;
            }

            Globs = globs.ToImmutableArray();
        }

        /// <summary>
        ///     Constructor
        /// </summary>
        /// <param name="globs">Children globs. Input gets shallow cloned</param>
        public CompositeGlob(params IMSBuildGlob[] globs) : this(globs.ToImmutableArray())
        {}

        /// <inheritdoc />
        public bool IsMatch(string stringToMatch)
        {
            // Threadpools are a scarce resource in Visual Studio, do not use them.
            //return Globs.AsParallel().Any(g => g.IsMatch(stringToMatch));

            return Globs.Any(g => g.IsMatch(stringToMatch));
        }
    }
}