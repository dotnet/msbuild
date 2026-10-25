// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Internal;

namespace Microsoft.Build.Evaluation;

internal sealed partial class ItemSpecMatcher<P, I>
{
    /// <summary>
    ///  Holds matching or vector-resolution state for one parsed fragment.
    /// </summary>
    private struct FragmentState
    {
        /// <summary>
        ///  The compiled matcher for a literal or glob fragment.
        /// </summary>
        public FileSpecMatcherTester Matcher;

        /// <summary>
        ///  Indicates whether <see cref="Matcher"/> has been initialized.
        /// </summary>
        public bool MatcherInitialized;

        /// <summary>
        ///  The resolved vector values, or <see langword="null"/> before resolution.
        ///  An empty array represents a completed expansion with no values.
        /// </summary>
        public ValueState[]? Values;
    }
}
