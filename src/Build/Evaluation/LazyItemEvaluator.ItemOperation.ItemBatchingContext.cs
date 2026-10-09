// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    private abstract partial class ItemOperation
    {
        [DebuggerDisplay(@"{DebugString()}")]
        protected readonly struct ItemBatchingContext
        {
            public I OperationItem { get; }
            private Dictionary<string, I> MatchingItems { get; }

            public ItemBatchingContext(I operationItem, Dictionary<string, I> matchingItems = null)
            {
                OperationItem = operationItem;

                MatchingItems = matchingItems == null || matchingItems.Count == 0
                    ? null
                    : matchingItems;
            }

            public IMetadataTable GetMetadataTable()
            {
                return MatchingItems == null
                    ? (IMetadataTable)OperationItem
                    : new ItemOperationMetadataTable(OperationItem, MatchingItems);
            }

            private string DebugString()
            {
                var referencedItemsString = MatchingItems == null
                    ? "none"
                    : string.Join(";", MatchingItems.Select(kvp => $"{kvp.Key} : {kvp.Value.EvaluatedInclude}"));

                return $"{OperationItem.Key} : {OperationItem.EvaluatedInclude}; MatchingItems: {referencedItemsString}";
            }
        }
    }
}
