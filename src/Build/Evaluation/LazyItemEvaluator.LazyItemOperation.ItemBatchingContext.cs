// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    private abstract partial class LazyItemOperation
    {
        [DebuggerDisplay(@"{DebugString()}")]
        protected readonly struct ItemBatchingContext
        {
            public I OperationItem { get; }
            private Dictionary<string, I> CapturedItems { get; }

            public ItemBatchingContext(I operationItem, Dictionary<string, I> capturedItems = null)
            {
                OperationItem = operationItem;

                CapturedItems = capturedItems == null || capturedItems.Count == 0
                    ? null
                    : capturedItems;
            }

            public IMetadataTable GetMetadataTable()
            {
                return CapturedItems == null
                    ? (IMetadataTable)OperationItem
                    : new ItemOperationMetadataTable(OperationItem, CapturedItems);
            }

            private string DebugString()
            {
                var referencedItemsString = CapturedItems == null
                    ? "none"
                    : string.Join(";", CapturedItems.Select(kvp => $"{kvp.Key} : {kvp.Value.EvaluatedInclude}"));

                return $"{OperationItem.Key} : {OperationItem.EvaluatedInclude}; CapturedItems: {referencedItemsString}";
            }
        }
    }
}
