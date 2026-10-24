// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    private abstract partial class ItemOperation
    {
        private class ItemOperationMetadataTable : IMetadataTable
        {
            private readonly I _operationItem;
            private readonly Dictionary<string, I> _matchingItems;

            public ItemOperationMetadataTable(I operationItem, Dictionary<string, I> matchingItems)
            {
                Assumed.Equal(matchingItems.Comparer, StringComparer.OrdinalIgnoreCase, "MSBuild assumes case insensitive item name comparison");

                _operationItem = operationItem;
                _matchingItems = matchingItems;
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
                // An existing table's null result is distinct from an absent matching item type.
                return table is null ? string.Empty : table.GetEscapedValueIfPresent(itemType, name);
            }

            private IMetadataTable GetMetadataTable(string itemType)
            {
                if (itemType?.Equals(_operationItem.Key, StringComparison.OrdinalIgnoreCase) != false)
                {
                    return _operationItem;
                }
                else if (_matchingItems.TryGetValue(itemType, out var item))
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
