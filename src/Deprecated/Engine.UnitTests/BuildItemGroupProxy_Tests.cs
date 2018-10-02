// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class ItemGroupProxy_Tests
    {
        [Test]
        public void BasicProxying()
        {          
            BuildItemGroup ig = new BuildItemGroup();
            BuildItem i1 = new BuildItem("name1", "value1");
            i1.SetMetadata("myMetaName", "myMetaValue");
            BuildItem i2 = new BuildItem("name2", "value2");
            ig.AddItem(i1);
            ig.AddItem(i2);

            BuildItemGroupProxy proxy = new BuildItemGroupProxy(ig);

            // Gather everything into our own table
            Hashtable list = new Hashtable(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry item in proxy)
            {
                list.Add(item.Key, item.Value);
            }

            // Check we got all the items
            Assertion.AssertEquals(2, list.Count);
            Assertion.AssertEquals("value1", ((TaskItem)list["name1"]).ItemSpec);
            Assertion.AssertEquals("value2", ((TaskItem)list["name2"]).ItemSpec);

            // Check they have all their metadata
            int builtInMetadata = FileUtilities.ItemSpecModifiers.All.Length;
            Assertion.AssertEquals(1 + builtInMetadata, ((TaskItem)list["name1"]).MetadataCount);
            Assertion.AssertEquals(0 + builtInMetadata, ((TaskItem)list["name2"]).MetadataCount);
            Assertion.AssertEquals("myMetaValue", ((TaskItem)list["name1"]).GetMetadata("myMetaName"));
        }

        [Test]
        public void CantModifyThroughEnumerator()
        {
            BuildItemGroup ig = new BuildItemGroup();
            BuildItem i1 = new BuildItem("name1", "value1");
            i1.SetMetadata("myMetaName", "myMetaValue");
            ig.AddItem(i1);

            BuildItemGroupProxy proxy = new BuildItemGroupProxy(ig);

            Hashtable list = new Hashtable(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry item in proxy)
            {
                list.Add(item.Key, item.Value);
            }

            // Change the item
            Assertion.AssertEquals("value1", ((TaskItem)list["name1"]).ItemSpec);
            ((TaskItem)list["name1"]).ItemSpec = "newItemSpec";
            ((TaskItem)list["name1"]).SetMetadata("newMetadata", "newMetadataValue");

            // We did change our copy
            Assertion.AssertEquals("newItemSpec", ((TaskItem)list["name1"]).ItemSpec);
            Assertion.AssertEquals("newMetadataValue", ((TaskItem)list["name1"]).GetMetadata("newMetadata"));
            Assertion.AssertEquals("myMetaValue", ((TaskItem)list["name1"]).GetMetadata("myMetaName"));

            // But get the item again
            list = new Hashtable(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry item in proxy)
            {
                list.Add(item.Key, item.Value);
            }

            // Item value and metadata hasn't changed
            Assertion.AssertEquals("value1", ((TaskItem)list["name1"]).ItemSpec);
            Assertion.AssertEquals("", ((TaskItem)list["name1"]).GetMetadata("newMetadata"));
            Assertion.AssertEquals("myMetaValue", ((TaskItem)list["name1"]).GetMetadata("myMetaName"));
        }
    }
}
