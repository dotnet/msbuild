// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Construction;

namespace Microsoft.Build.Experimental;

public static class ItemTypeExtensions
{
    public static IEnumerable<ProjectItemElement> GetItemsOfType(this IEnumerable<ProjectItemElement> items,
        string itemType)
    {
        return items.Where(i =>
            i.ItemType.Equals(itemType, StringComparison.CurrentCultureIgnoreCase));
    }
}

public class ItemsHolder(IEnumerable<ProjectItemElement> items, IEnumerable<ProjectItemGroupElement> itemGroups)
{
    public IEnumerable<ProjectItemElement> Items { get; } = items;
    public IEnumerable<ProjectItemGroupElement> ItemGroups { get; } = itemGroups;

    public IEnumerable<ProjectItemElement> GetItemsOfType(string itemType)
    {
        return Items.GetItemsOfType(itemType);
    }
}

public class ParsedItemsContext : BuildAnalysisContext
{
    internal ParsedItemsContext(
        LoggingContext loggingContext,
        ItemsHolder itemsHolder) :
        base(loggingContext) => ItemsHolder = itemsHolder;

    public ItemsHolder ItemsHolder { get; }
}
