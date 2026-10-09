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
                return RouteCall(itemType, name, (t, it, n) => t.GetEscapedValue(it, n));
            }

            public string GetEscapedValueIfPresent(string itemType, string name)
            {
                return RouteCall(itemType, name, (t, it, n) => t.GetEscapedValueIfPresent(it, n));
            }

            private string RouteCall(string itemType, string name, Func<IMetadataTable, string, string, string> getEscapedValueFunc)
            {
                if (itemType?.Equals(_operationItem.Key, StringComparison.OrdinalIgnoreCase) != false)
                {
                    return getEscapedValueFunc(_operationItem, itemType, name);
                }
                else if (_matchingItems.TryGetValue(itemType, out var item))
                {
                    return getEscapedValueFunc(item, itemType, name);
                }
                else
                {
                    return string.Empty;
                }
            }
        }
    }
}
