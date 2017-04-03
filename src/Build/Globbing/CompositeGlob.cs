// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------


using System.Collections.Generic;
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
        /// <param name="globs">Children globs</param>
        public CompositeGlob(IEnumerable<IMSBuildGlob> globs)
        {
            Globs = globs;
        }

        /// <summary>
        ///     Constructor
        /// </summary>
        /// <param name="globs">children globs</param>
        public CompositeGlob(params IMSBuildGlob[] globs) : this(globs.AsEnumerable())
        {}

        /// <inheritdoc />
        public bool IsMatch(string stringToMatch)
        {
            return Globs.AsParallel().Any(g => g.IsMatch(stringToMatch));
        }
    }
}