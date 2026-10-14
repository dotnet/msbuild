// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    /// <summary>
    ///  Identifies an earlier item state by its operation sequence and exclusive stopping position.
    /// </summary>
    /// <param name="sequence">The shared append-only operation sequence.</param>
    /// <param name="operationCount">The number of operations visible to this reference, not the number of items.</param>
    private readonly struct ItemListBookmark(ItemOperationSequence sequence, int operationCount)
    {
        /// <summary>
        ///  Gets the items at the bookmark, filtered by originating Include conditions.
        /// </summary>
        /// <param name="laterRemoveGlobPatterns">The later removed globs that may prune this computation.</param>
        /// <returns>
        ///  The filtered items in evaluation order.
        /// </returns>
        public ImmutableList<I> GetFilteredItems(ImmutableHashSet<string> laterRemoveGlobPatterns)
            => sequence.GetFilteredItems(operationCount, laterRemoveGlobPatterns);

        /// <summary>
        ///  Gets all items visible at the bookmarked position.
        /// </summary>
        /// <param name="laterRemoveGlobPatterns">The later removed globs that may prune this computation.</param>
        /// <returns>
        ///  A builder over the captured item result.
        /// </returns>
        public OrderedItemDataCollection.Builder GetItemData(ImmutableHashSet<string> laterRemoveGlobPatterns)
            => sequence.GetItemData(operationCount, laterRemoveGlobPatterns);

        /// <summary>
        ///  Announces that this position's result will be needed by another operation.
        /// </summary>
        public void MarkAsNeeded() => sequence.MarkAsNeeded(operationCount);
    }
}
