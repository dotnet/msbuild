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
    private readonly struct ItemListReference(ItemOperationSequence sequence, int operationCount)
    {
        /// <summary>
        ///  Gets the condition-true items visible at the captured position.
        /// </summary>
        /// <returns>
        ///  The matching items in evaluation order.
        /// </returns>
        public ImmutableList<I> GetMatchedItems()
            => sequence.GetMatchedItems(operationCount);

        /// <summary>
        ///  Gets all items visible at the captured position.
        /// </summary>
        /// <returns>
        ///  A builder over the captured item result.
        /// </returns>
        public OrderedItemDataCollection.Builder GetItemData()
            => sequence.GetItemData(operationCount);

        /// <summary>
        ///  Announces that this position's result will be needed by another operation.
        /// </summary>
        public void MarkAsReferenced() => sequence.MarkAsReferenced(operationCount);
    }
}
