// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    private partial class UpdateOperation
    {
        private readonly struct MatchResult
        {
            public bool IsMatch { get; }

            /// <summary>
            ///  Gets the last matching item of each item type, or <see langword="null"/> when none matched.
            /// </summary>
            public Dictionary<string, I>? MatchingItems { get; }

            public MatchResult(bool isMatch, Dictionary<string, I>? matchingItems)
            {
                IsMatch = isMatch;
                MatchingItems = matchingItems;
            }
        }
    }
}
