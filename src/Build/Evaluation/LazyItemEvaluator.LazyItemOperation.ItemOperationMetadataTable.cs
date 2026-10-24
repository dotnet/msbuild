// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    private abstract partial class LazyItemOperation
    {
        private class ItemOperationMetadataTable : IMetadataTable
        {
            private readonly I _operationItem;
            private readonly Dictionary<string, I> _capturedItems;

            public ItemOperationMetadataTable(I operationItem, Dictionary<string, I> capturedItems)
            {
                Assumed.Equal(capturedItems.Comparer, StringComparer.OrdinalIgnoreCase, "MSBuild assumes case insensitive item name comparison");

                _operationItem = operationItem;
                _capturedItems = capturedItems;
            }

            public string GetEscapedValue(string name)
            {
                return _operationItem.GetEscapedValue(name);
            }

            public string GetEscapedValue(string itemType, string name)
            {
                IMetadataTable table = GetMetadataTable(itemType);
                return table is null ? string.Empty : table.GetEscapedValue(itemType, name);
            }

            public string GetEscapedValueIfPresent(string itemType, string name)
            {
                IMetadataTable table = GetMetadataTable(itemType);
                // An existing table's null result is distinct from an absent captured item type.
                return table is null ? string.Empty : table.GetEscapedValueIfPresent(itemType, name);
            }

            private IMetadataTable GetMetadataTable(string itemType)
            {
                if (itemType?.Equals(_operationItem.Key, StringComparison.OrdinalIgnoreCase) != false)
                {
                    return _operationItem;
                }
                else if (_capturedItems.TryGetValue(itemType, out var item))
                {
                    return item;
                }
                else
                {
                    return null;
                }
            }
        }
    }
}
