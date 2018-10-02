// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Globbing
{
    /// <summary>
    ///     A glob with gaps. The gaps are represented as other globs.
    ///     For example, to express a glob that matches all .cs files except the ones containing "foo" and the ones under bin directories, one can use:
    ///     <code>
    /// new MSBuildGlobWithGaps(
    ///    MSBuildGlob.Parse("**/*.cs"),            // matches all .cs files
    ///    new CompositeGlob(                       // a composite glob to combine all the gaps
    ///       MSBuildGlob.Parse("**/*foo*.cs"),     // matches .cs files containing "foo"
    ///       MSBuildGlob.Parse("**/bin/**/*.cs")   // matches .cs files under bin directories
    ///    )
    /// )
    ///     </code>
    /// </summary>
    public class MSBuildGlobWithGaps : IMSBuildGlob
    {
        /// <summary>
        ///     The main glob used for globbing operations.
        /// </summary>
        public IMSBuildGlob MainGlob { get; }

        /// <summary>
        ///     Glob which will be subtracted from the <see cref="MainGlob" />.
        /// </summary>
        public IMSBuildGlob Gaps { get; }

        /// <summary>
        ///     Constructor
        /// </summary>
        /// <param name="mainGlob">The main glob</param>
        /// <param name="gaps">The gap glob</param>
        public MSBuildGlobWithGaps(IMSBuildGlob mainGlob, IEnumerable<IMSBuildGlob> gaps)
        {
            ErrorUtilities.VerifyThrowArgumentNull(mainGlob, nameof(mainGlob));
            ErrorUtilities.VerifyThrowArgumentNull(gaps, nameof(gaps));

            MainGlob = mainGlob;
            Gaps = new CompositeGlob(gaps);
        }

        /// <summary>
        ///     Constructor
        /// </summary>
        /// <param name="mainGlob">The main glob</param>
        /// <param name="gaps">The gap glob</param>
        public MSBuildGlobWithGaps(IMSBuildGlob mainGlob, params IMSBuildGlob[] gaps) : this(mainGlob, gaps.AsEnumerable())
        {}

        /// <inheritdoc />
        public bool IsMatch(string stringToMatch)
        {
            return MainGlob.IsMatch(stringToMatch) && !Gaps.IsMatch(stringToMatch);
        }
    }
}