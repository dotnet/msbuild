// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.BuildEngine.Shared;
using System.Xml;

using NUnit.Framework;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class ItemBucket_Tests
    {
        /// <summary>
        /// When a bucket is created, it's created for specific item names. After it's created
        /// no items of those types should be visible in the bucket (unless they are subsequently added)
        /// </summary>
        [Test]
        public void InitiallyNoItemsInBucketOfTypesInItemNames()
        {
            // This bucket is for items of type "i"
            string[] itemNames = new string[] { "i" };

            // There are items of type "i" and "j" available in the project, though
            BuildItemGroup group1 = new BuildItemGroup();
            BuildItemGroup group2 = new BuildItemGroup();
            group1.AddNewItem("i", "i1");
            group2.AddNewItem("j", "j1");
            Hashtable items = new Hashtable(StringComparer.OrdinalIgnoreCase);
            items.Add("i", group1);
            items.Add("j", group2);
            Lookup lookup = LookupHelpers.CreateLookup(items);

            ItemBucket bucket = new ItemBucket(itemNames, new Dictionary<string, string>(), lookup, 0);

            // No items of type i
            Assertion.AssertEquals(0, bucket.Lookup.GetItems("i1").Count);
            // Items of type j, however, are visible
            Assertion.AssertEquals(1, bucket.Lookup.GetItems("j").Count);
        }

    }
}
