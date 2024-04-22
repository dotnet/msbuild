// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.UnitTests;
using Xunit;

namespace Microsoft.Build.BuildCheck.UnitTests;

public class ParsedItemsAnalysisDataTests
{
    [Fact]
    public void ItemsHolder_GetItemsOfType_ShouldFilterProperly()
    {
        ProjectRootElement root = ProjectRootElement.Create();

        ProjectItemElement el1 = ProjectItemElement.CreateDisconnected("ItemB", root);
        ProjectItemElement el2 = ProjectItemElement.CreateDisconnected("ItemB", root);
        ProjectItemElement el3 = ProjectItemElement.CreateDisconnected("ItemA", root);
        ProjectItemElement el4 = ProjectItemElement.CreateDisconnected("ItemB", root);
        ProjectItemElement el5 = ProjectItemElement.CreateDisconnected("ItemA", root);

        var items = new List<ProjectItemElement>()
        {
            el1,
            el2,
            el3,
            el4,
            el5
        };
        var itemsHolder = new ItemsHolder(items, new List<ProjectItemGroupElement>());

        var itemsA = itemsHolder.GetItemsOfType("ItemA").ToList();
        var itemsB = itemsHolder.GetItemsOfType("ItemB").ToList();

        itemsA.ShouldBeSameIgnoringOrder(new List<ProjectItemElement>() { el3, el5 });
        itemsB.ShouldBeSameIgnoringOrder(new List<ProjectItemElement>() { el1, el2, el4 });
    }
}
