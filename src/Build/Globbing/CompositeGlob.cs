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
            : this(globs is ImmutableArray<IMSBuildGlob> immutableGlobs ? immutableGlobs : globs.ToImmutableArray())
        {}

        /// <summary>
        ///     Constructor
        /// </summary>
        /// <param name="globs">Children globs. Input gets shallow cloned</param>
        public CompositeGlob(params IMSBuildGlob[] globs) : this(globs.ToImmutableArray())
        {}

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
            //return Globs.AsParallel().Any(g => g.IsMatch(stringToMatch));

            return _globs.Any(g => g.IsMatch(stringToMatch));
        }
    }
}
