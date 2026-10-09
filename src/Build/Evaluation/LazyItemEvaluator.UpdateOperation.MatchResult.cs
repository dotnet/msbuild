// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    private partial class UpdateOperation
    {
        private delegate MatchResult ItemSpecMatchesItem(ItemSpec<P, I> itemSpec, I itemToMatch);

        private readonly struct MatchResult
        {
            public bool IsMatch { get; }
            public Dictionary<string, I> CapturedItemsFromReferencedItemTypes { get; }

            public MatchResult(bool isMatch, Dictionary<string, I> capturedItemsFromReferencedItemTypes)
            {
                IsMatch = isMatch;
                CapturedItemsFromReferencedItemTypes = capturedItemsFromReferencedItemTypes;
            }
        }
    }
}
