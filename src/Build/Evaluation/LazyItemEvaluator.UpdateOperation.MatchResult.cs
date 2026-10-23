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
            ///  Gets the last matching source of each referenced item type, or <see langword="null"/> when no sources were captured.
            /// </summary>
            public Dictionary<string, I>? CapturedItemsFromReferencedItemTypes { get; }

            public MatchResult(bool isMatch, Dictionary<string, I>? capturedItemsFromReferencedItemTypes)
            {
                IsMatch = isMatch;
                CapturedItemsFromReferencedItemTypes = capturedItemsFromReferencedItemTypes;
            }
        }
    }
}
