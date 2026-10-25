// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Internal;

namespace Microsoft.Build.Evaluation;

internal sealed partial class ItemSpecMatcher<P, I>
{
    /// <summary>
    ///  Holds escaped text and lazy matching state for one resolved vector value.
    /// </summary>
    /// <param name="text">The escaped value produced by vector resolution.</param>
    private struct ValueState(string text)
    {
        /// <summary>
        ///  The escaped value produced by vector resolution.
        /// </summary>
        public readonly string Text = text;

        /// <summary>
        ///  The compiled matcher for this value.
        /// </summary>
        public FileSpecMatcherTester Matcher;

        /// <summary>
        ///  Indicates whether <see cref="Matcher"/> has been initialized.
        /// </summary>
        public bool MatcherInitialized;
    }
}
