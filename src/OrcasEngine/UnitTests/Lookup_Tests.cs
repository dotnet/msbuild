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
using System.Threading;

namespace Microsoft.Build.UnitTests
{  
    [TestFixture]
    public class Lookup_Tests
    {
        /// <summary>
        /// Primary group contains an item for a type and secondary does;
        /// primary item should be returned instead of the secondary item.
        /// </summary>
        [Test]
        public void SecondaryItemShadowedByPrimaryItem()
        {
            BuildItemGroup group1 = new BuildItemGroup();
            group1.AddNewItem("i1", "a1");
            Hashtable table1 = new Hashtable(StringComparer.OrdinalIgnoreCase);
            table1.Add("i1", group1);
            Lookup lookup = LookupHelpers.CreateLookup(table1);

            lookup.EnterScope();
            BuildItemGroup group2 = new BuildItemGroup();
            group2.AddNewItem("i1", "a2");
            lookup.PopulateWithItems("i1", group2);

            // Should return the item from the primary, not the secondary table
            Assertion.AssertEquals("a2", lookup.GetItems("i1")[0].FinalItemSpec);
        }

        /// <summary>
        /// Primary group does not contain an item for a type but secondary does;
        /// secondary item should be returned.
        /// </summary>
        [Test]
        public void SecondaryItemNotShadowedByPrimaryItem()
        {
            BuildItemGroup group1 = new BuildItemGroup();
            group1.AddNewItem("i1", "a1");
            Hashtable table1 = new Hashtable(StringComparer.OrdinalIgnoreCase);
            table1.Add("i1", group1);
            Lookup lookup = LookupHelpers.CreateLookup(table1);

            lookup.EnterScope();

            // Should return item from the secondary table.
            Assertion.AssertEquals("a1", lookup.GetItems("i1")[0].FinalItemSpec);
        }

        /// <summary>
        /// No items of that type: should return empty group rather than null
        /// </summary>
        [Test]
        public void UnknownItemType()
        {
            Lookup lookup = LookupHelpers.CreateEmptyLookup();

            lookup.EnterScope(); // Doesn't matter really

            Assertion.AssertEquals(0, lookup.GetItems("i1").Count);
        }

        /// <summary>
        /// Adds accumulate as we lookup in the tables
        /// </summary>
        [Test]
        public void AddsAreCombinedWithPopulates()
        {
            // One item in the project
            BuildItemGroup group1 = new BuildItemGroup();
            group1.AddNewItem("i1", "a1");
            Hashtable table1 = new Hashtable(StringComparer.OrdinalIgnoreCase);
            table1.Add("i1", group1);
            Lookup lookup = LookupHelpers.CreateLookup(table1);

            // We see the one item
            Assertion.AssertEquals("a1", lookup.GetItems("i1")[0].FinalItemSpec);

            // One item in the project
            Assertion.AssertEquals("a1", group1[0].FinalItemSpec);
            Assertion.AssertEquals(1, group1.Count);

            // Start a target
            lookup.EnterScope();

            // We see the one item 
            Assertion.AssertEquals("a1", lookup.GetItems("i1")[0].FinalItemSpec);

            // One item in the project
            Assertion.AssertEquals("a1", group1[0].FinalItemSpec);
            Assertion.AssertEquals(1, group1.Count);
            
            // Start a task (eg) and add a new item
            lookup.EnterScope();
            lookup.AddNewItem(new BuildItem("i1", "a2"));

            // Now we see two items
            Assertion.AssertEquals("a1", lookup.GetItems("i1")[0].FinalItemSpec);
            Assertion.AssertEquals("a2", lookup.GetItems("i1")[1].FinalItemSpec);

            // But there's still one item in the project
            Assertion.AssertEquals("a1", group1[0].FinalItemSpec);
            Assertion.AssertEquals(1, group1.Count);

            // Finish the task
            lookup.LeaveScope();
          
            // We still see two items
            Assertion.AssertEquals("a1", lookup.GetItems("i1")[0].FinalItemSpec);
            Assertion.AssertEquals("a2", lookup.GetItems("i1")[1].FinalItemSpec);

            // But there's still one item in the project
            Assertion.AssertEquals("a1", group1[0].FinalItemSpec);
            Assertion.AssertEquals(1, group1.Count);

            // Finish the target
            lookup.LeaveScope();

            // We still see two items
            Assertion.AssertEquals("a1", lookup.GetItems("i1")[0].FinalItemSpec);
            Assertion.AssertEquals("a2", lookup.GetItems("i1")[1].FinalItemSpec);

            // And now the items have gotten put into the global group
            Assertion.AssertEquals("a1", group1[0].FinalItemSpec);
            Assertion.AssertEquals("a2", group1[1].FinalItemSpec);
            Assertion.AssertEquals(2, group1.Count);
        }

        [Test]
        public void Removes()
        {
            // One item in the project
            BuildItemGroup group1 = new BuildItemGroup();
            BuildItem item1 = new BuildItem("i1", "a1");
            group1.AddItem(item1);
            Hashtable table1 = new Hashtable(StringComparer.OrdinalIgnoreCase);
            table1.Add("i1", group1);
            Lookup lookup = LookupHelpers.CreateLookup(table1);

            // Start a target
            lookup.EnterScope();

            // Start a task (eg) and add a new item
            lookup.EnterScope();
            BuildItem item2 = new BuildItem("i1", "a2");
            lookup.AddNewItem(item2);

            // Remove one item
            lookup.RemoveItem(item1);

            // We see one item
            Assertion.AssertEquals(1, lookup.GetItems("i1").Count);
            Assertion.AssertEquals("a2", lookup.GetItems("i1")[0].FinalItemSpec);

            // Remove the other item
            lookup.RemoveItem(item2);

            // We see no items
            Assertion.AssertEquals(0, lookup.GetItems("i1").Count);

            // Finish the task
            lookup.LeaveScope();

            // We still see no items
            Assertion.AssertEquals(0, lookup.GetItems("i1").Count);

            // But there's still one item in the project
            Assertion.AssertEquals("a1", group1[0].FinalItemSpec);
            Assertion.AssertEquals(1, group1.Count);

            // Finish the target
            lookup.LeaveScope();

            // We still see no items
            Assertion.AssertEquals(0, lookup.GetItems("i1").Count);

            // And now there are no items in the project either
            Assertion.AssertEquals(0, group1.Count);
        }

// These tests used to have an #if DEBUG around them, because the method they
// are testing only gets called in chk builds; they have been removed
// entirely due to BVT bug 527712:  Main is attempting to run chk unit tests 
// against ret bits.  Please only uncomment these tests if you have verified
// that that scenario works.
#if NULL    
        /// <summary>
        /// Lookup class should never be asked to add an item that was already removed;
        /// this is not something that is possible through a project file: all adds create 
        /// brand new items.
        /// </summary>
        [Test]
        [ExpectedException(typeof(InternalErrorException))]
        public void RemoveBeforeAnAddShouldBeInvalid()
        {
            Hashtable table1 = new Hashtable(StringComparer.OrdinalIgnoreCase);
            Lookup lookup = LookupHelpers.CreateLookup(table1);

            // Start a target
            lookup.EnterScope();

            // Start a task (eg)
            lookup.EnterScope();
            BuildItem item1 = new BuildItem("i1", "a2");

            // Remove an item then add it
            lookup.RemoveItem(item1);
            lookup.AddNewItem(item1);
        }

        /// <summary>
        /// Lookup class should never be asked to modify an item that was already removed;
        /// this is not something that is possible through a project file
        /// </summary>
        [Test]
        [ExpectedException(typeof(InternalErrorException))]
        public void RemoveBeforeModifyShouldBeInvalid()
        {
            Hashtable table1 = new Hashtable(StringComparer.OrdinalIgnoreCase);
            Lookup lookup = LookupHelpers.CreateLookup(table1);

            // Start a target
            lookup.EnterScope();

            // Start a task (eg)
            lookup.EnterScope();
            BuildItem item1 = new BuildItem("i1", "a2");
            BuildItemGroup group = new BuildItemGroup();
            group.AddItem(item1);

            // Remove an item then modify it
            lookup.RemoveItem(item1);

            Dictionary<string, string> metadataChanges = new Dictionary<string, string>();
            metadataChanges.Add("x", "y");
            lookup.ModifyItems("i1", group, metadataChanges);
        }
#endif

        [Test]
        public void RemoveItemPopulatedInLowerScope()
        {
            Hashtable table1 = new Hashtable(StringComparer.OrdinalIgnoreCase);
            Lookup lookup = LookupHelpers.CreateLookup(table1);
            BuildItem item1 = new BuildItem("i1", "a2");

            // Start a target
            lookup.EnterScope();

            // There's one item in this batch
            lookup.PopulateWithItem(item1);

            // We see it
            Assertion.AssertEquals(1, lookup.GetItems("i1").Count);

            // Make a clone so we can keep an eye on that item
            Lookup lookup2 = lookup.Clone();

            // We can see the item in the clone
            Assertion.AssertEquals(1, lookup2.GetItems("i1").Count);

                // Start a task (eg)
                lookup.EnterScope();

                // We see the item below
                Assertion.AssertEquals(1, lookup.GetItems("i1").Count);

                // Remove that item
                lookup.RemoveItem(item1);

                // We see no items
                Assertion.AssertEquals(0, lookup.GetItems("i1").Count);

                // The clone is unaffected so far
                Assertion.AssertEquals(1, lookup2.GetItems("i1").Count);

                // Finish the task
                lookup.LeaveScope();

            // We still see no items
            Assertion.AssertEquals(0, lookup.GetItems("i1").Count);

            // But now the clone doesn't either
            Assertion.AssertEquals(0, lookup2.GetItems("i1").Count);

            // Finish the target
            lookup.LeaveScope();

            // We still see no items
            Assertion.AssertEquals(0, lookup.GetItems("i1").Count);
            Assertion.AssertEquals(0, lookup2.GetItems("i1").Count);
        }

        [Test]
        public void RemoveItemAddedInLowerScope()
        {
            Hashtable table1 = new Hashtable(StringComparer.OrdinalIgnoreCase);
            Lookup lookup = LookupHelpers.CreateLookup(table1);

            // Start a target
            lookup.EnterScope();

            // Add an item
            BuildItem item1 = new BuildItem("i1", "a2");
            lookup.AddNewItem(item1);

            // Start a task (eg)
            lookup.EnterScope();

            // We see the item below
            Assertion.AssertEquals(1, lookup.GetItems("i1").Count);

            // Remove that item
            lookup.RemoveItem(item1);

            // We see no items
            Assertion.AssertEquals(0, lookup.GetItems("i1").Count);

            // Finish the task
            lookup.LeaveScope();

            // We still see no items
            Assertion.AssertEquals(0, lookup.GetItems("i1").Count);

            // Finish the target
            lookup.LeaveScope();

            // We still see no items
            Assertion.AssertEquals(0, lookup.GetItems("i1").Count);
        }

        [Test]
        public void ModifyItem()
        {
            Hashtable table1 = new Hashtable(StringComparer.OrdinalIgnoreCase);
            Lookup lookup = LookupHelpers.CreateLookup(table1);

            lookup.EnterScope();

            // Add an item with m=m1
            BuildItem item1 = new BuildItem("i1", "a2");
            item1.SetMetadata("m", "m1");
            lookup.AddNewItem(item1);

            lookup.EnterScope();

            // Change the item to be m=m2
            Dictionary<string, string> newMetadata = new Dictionary<string, string>();
            newMetadata.Add("m", "m2");
            BuildItemGroup group = new BuildItemGroup();
            group.AddItem(item1);
            lookup.ModifyItems(item1.Name, group, newMetadata);

            // Now it has m=m2
            group = lookup.GetItems("i1");
            Assertion.AssertEquals(1, group.Count);
            Assertion.AssertEquals("m2", group[0].GetMetadata("m"));

            // But the original item hasn't changed yet
            Assertion.AssertEquals("m1", item1.GetMetadata("m"));

            lookup.LeaveScope();

            // It still has m=m2
            group = lookup.GetItems("i1");
            Assertion.AssertEquals(1, group.Count);
            Assertion.AssertEquals("m2", group[0].GetMetadata("m"));

            // The original item still hasn't changed
            // even though it was added in this scope
            Assertion.AssertEquals("m1", item1.GetMetadata("m"));

            lookup.LeaveScope();

            // It still has m=m2
            group = lookup.GetItems("i1");
            Assertion.AssertEquals(1, group.Count);
            Assertion.AssertEquals("m2", group[0].GetMetadata("m"));

            // But now the original item has changed
            Assertion.AssertEquals("m2", item1.GetMetadata("m"));
        }

        /// <summary>
        /// Modifications should be merged
        /// </summary>
        [Test]
        public void ModifyItemModifiedInPreviousScope()
        {
            Hashtable table1 = new Hashtable(StringComparer.OrdinalIgnoreCase);
            Lookup lookup = LookupHelpers.CreateLookup(table1);

            // Add an item with m=m1 and n=n1 
            BuildItem item1 = new BuildItem("i1", "a2");
            item1.SetMetadata("m", "m1");
            lookup.PopulateWithItem(item1);

            lookup.EnterScope();

            // Make a modification to the item to be m=m2
            Dictionary<string, string> newMetadata = new Dictionary<string, string>();
            newMetadata.Add("m", "m2");
            newMetadata.Add("n", "n2");
            BuildItemGroup group = new BuildItemGroup();
            group.AddItem(item1);
            lookup.ModifyItems(item1.Name, group, newMetadata);

            lookup.EnterScope();

            // Make another modification to the item
            newMetadata = new Dictionary<string, string>();
            newMetadata.Add("m", "m3");
            newMetadata.Add("o", "o3");
            lookup.ModifyItems(item1.Name, group, newMetadata);

            // It's now m=m3, n=n2, o=o3
            group = lookup.GetItems("i1");
            Assertion.AssertEquals(1, group.Count);
            Assertion.AssertEquals("m3", group[0].GetMetadata("m"));
            Assertion.AssertEquals("n2", group[0].GetMetadata("n"));
            Assertion.AssertEquals("o3", group[0].GetMetadata("o"));
        }

        /// <summary>
        /// Modifications should be merged
        /// </summary>
        [Test]
        public void ModifyItemTwiceInSameScope1()
        {
            Hashtable table1 = new Hashtable(StringComparer.OrdinalIgnoreCase);
            Lookup lookup = LookupHelpers.CreateLookup(table1);

            // Add an item with m=m1 and n=n1 
            BuildItem item1 = new BuildItem("i1", "a2");
            item1.SetMetadata("m", "m1");
            lookup.PopulateWithItem(item1);

            lookup.EnterScope();

            // Make a modification to the item to be m=m2
            Dictionary<string, string> newMetadata = new Dictionary<string, string>();
            newMetadata.Add("m", "m2");
            BuildItemGroup group = new BuildItemGroup();
            group.AddItem(item1);
            lookup.ModifyItems(item1.Name, group, newMetadata);

            // Make an unrelated modification to the item
            newMetadata = new Dictionary<string, string>();
            newMetadata.Add("n", "n1");
            lookup.ModifyItems(item1.Name, group, newMetadata);

            // It's now m=m2
            group = lookup.GetItems("i1");
            Assertion.AssertEquals(1, group.Count);
            Assertion.AssertEquals("m2", group[0].GetMetadata("m"));
        }

        /// <summary>
        /// Modifications should be merged
        /// </summary>
        [Test]
        public void ModifyItemTwiceInSameScope2()
        {
            Hashtable table1 = new Hashtable(StringComparer.OrdinalIgnoreCase);
            Lookup lookup = LookupHelpers.CreateLookup(table1);

            // Add an item with m=m1 and n=n1 and o=o1
            BuildItem item1 = new BuildItem("i1", "a2");
            item1.SetMetadata("m", "m1");
            item1.SetMetadata("n", "n1");
            item1.SetMetadata("o", "o1");
            lookup.PopulateWithItem(item1);

            lookup.EnterScope();

            // It's still m=m1, n=n1, o=o1
            BuildItemGroup group = lookup.GetItems("i1");
            Assertion.AssertEquals(1, group.Count);
            Assertion.AssertEquals("m1", group[0].GetMetadata("m"));
            Assertion.AssertEquals("n1", group[0].GetMetadata("n"));
            Assertion.AssertEquals("o1", group[0].GetMetadata("o"));

            // Make a modification to the item to be m=m2 and n=n2
            Dictionary<string, string> newMetadata = new Dictionary<string, string>();
            newMetadata.Add("m", "m2");
            newMetadata.Add("n", "n2");
            group = new BuildItemGroup();
            group.AddItem(item1);
            lookup.ModifyItems("i1", group, newMetadata);

            // It's now m=m2, n=n2, o=o1
            BuildItemGroup foundGroup = lookup.GetItems("i1");
            Assertion.AssertEquals(1, foundGroup.Count);
            Assertion.AssertEquals("m2", foundGroup[0].GetMetadata("m"));
            Assertion.AssertEquals("n2", foundGroup[0].GetMetadata("n"));
            Assertion.AssertEquals("o1", foundGroup[0].GetMetadata("o"));

            // Make a modification to the item to be n=n3 
            newMetadata = new Dictionary<string, string>();
            newMetadata.Add("n", "n3");
            lookup.ModifyItems("i1", group, newMetadata);

            // It's now m=m2, n=n3, o=o1
            foundGroup = lookup.GetItems("i1");
            Assertion.AssertEquals(1, foundGroup.Count);
            Assertion.AssertEquals("m2", foundGroup[0].GetMetadata("m"));
            Assertion.AssertEquals("n3", foundGroup[0].GetMetadata("n"));
            Assertion.AssertEquals("o1", foundGroup[0].GetMetadata("o"));

            // But the original item hasn't changed yet
            Assertion.AssertEquals("m1", item1.GetMetadata("m"));
            Assertion.AssertEquals("n1", item1.GetMetadata("n"));
            Assertion.AssertEquals("o1", item1.GetMetadata("o"));

            lookup.LeaveScope();

            // It's still m=m2, n=n3, o=o1
            foundGroup = lookup.GetItems("i1");
            Assertion.AssertEquals(1, foundGroup.Count);
            Assertion.AssertEquals("m2", foundGroup[0].GetMetadata("m"));
            Assertion.AssertEquals("n3", foundGroup[0].GetMetadata("n"));
            Assertion.AssertEquals("o1", foundGroup[0].GetMetadata("o"));

            // And the original item has changed
            Assertion.AssertEquals("m2", item1.GetMetadata("m"));
            Assertion.AssertEquals("n3", item1.GetMetadata("n"));
            Assertion.AssertEquals("o1", item1.GetMetadata("o"));
        }


        [Test]
        public void ModifyItemThatWasAddedInSameScope()
        {
            Hashtable table1 = new Hashtable(StringComparer.OrdinalIgnoreCase);
            Lookup lookup = LookupHelpers.CreateLookup(table1);

            lookup.EnterScope();

            // Add an item with m=m1
            BuildItem item1 = new BuildItem("i1", "a2");
            item1.SetMetadata("m", "m1");
            lookup.AddNewItem(item1);

            // Change the item to be m=m2
            Dictionary<string, string> newMetadata = new Dictionary<string, string>();
            newMetadata.Add("m", "m2");
            BuildItemGroup group = new BuildItemGroup();
            group.AddItem(item1);
            lookup.ModifyItems(item1.Name, group, newMetadata);

            // Now it has m=m2
            group = lookup.GetItems("i1");
            Assertion.AssertEquals(1, group.Count);
            Assertion.AssertEquals("m2", group[0].GetMetadata("m"));

            // But the original item hasn't changed yet
            Assertion.AssertEquals("m1", item1.GetMetadata("m"));

            lookup.LeaveScope();

            // It still has m=m2
            group = lookup.GetItems("i1");
            Assertion.AssertEquals(1, group.Count);
            Assertion.AssertEquals("m2", group[0].GetMetadata("m"));

            // But now the original item has changed as well
            Assertion.AssertEquals("m2", item1.GetMetadata("m"));
        }

        /// <summary>
        /// Modifying an item in the outside scope is prohibited-
        /// purely because we don't need to do it in our code
        /// </summary>
        [Test]
        [ExpectedException(typeof(InternalErrorException))]
        public void ModifyItemInOutsideScope()
        {
            Lookup lookup = LookupHelpers.CreateLookup(new Hashtable());
            lookup.AddNewItem(new BuildItem("x", "y"));
        }

        /// <summary>
        /// After modification, should be able to GetItem and then modify it again
        /// </summary>
        [Test]
        public void ModifyItemPreviouslyModifiedAndGottenThroughGetItem()
        {
            Hashtable table1 = new Hashtable(StringComparer.OrdinalIgnoreCase);
            Lookup lookup = LookupHelpers.CreateLookup(table1);

            // Add an item with m=m1 and n=n1 
            BuildItem item1 = new BuildItem("i1", "a2");
            item1.SetMetadata("m", "m1");
            lookup.PopulateWithItem(item1);

            lookup.EnterScope();

            // Make a modification to the item to be m=m2
            Dictionary<string, string> newMetadata = new Dictionary<string, string>();
            newMetadata.Add("m", "m2");
            BuildItemGroup group = new BuildItemGroup();
            group.AddItem(item1);
            lookup.ModifyItems(item1.Name, group, newMetadata);

            // Get the item (under the covers, it cloned it in order to apply the modification)
            BuildItemGroup group2 = lookup.GetItems(item1.Name);
            Assertion.AssertEquals(1, group2.Count);
            BuildItem item1b = group2[0];

            // Modify to m=m3
            Dictionary<string, string> newMetadata2 = new Dictionary<string, string>();
            newMetadata2.Add("m", "m3");
            BuildItemGroup group3 = new BuildItemGroup();
            group3.AddItem(item1b);
            lookup.ModifyItems(item1b.Name, group3, newMetadata2);

            // Modifications are visible
            BuildItemGroup group4 = lookup.GetItems(item1b.Name);
            Assertion.AssertEquals(1, group4.Count);
            Assertion.AssertEquals("m3", group4[0].GetMetadata("m"));

            // Leave scope
            lookup.LeaveScope();

            // Still visible
            BuildItemGroup group5 = lookup.GetItems(item1b.Name);
            Assertion.AssertEquals(1, group5.Count);
            Assertion.AssertEquals("m3", group5[0].GetMetadata("m"));
        }


        /// <summary>
        /// After modification, should be able to GetItem and then modify it again
        /// </summary>
        [Test]
        public void ModifyItemInProjectPreviouslyModifiedAndGottenThroughGetItem()
        {
            // Create some project state with an item with m=m1 and n=n1 
            Hashtable table1 = new Hashtable(StringComparer.OrdinalIgnoreCase);
            BuildItem item1 = new BuildItem("i1", "a2");
            item1.SetMetadata("m", "m1");
            BuildItemGroup group0 = new BuildItemGroup();
            group0.AddExistingItem(item1);
            table1["i1"] = group0;

            Lookup lookup = LookupHelpers.CreateLookup(table1);

            lookup.EnterScope();

            // Make a modification to the item to be m=m2
            Dictionary<string, string> newMetadata = new Dictionary<string, string>();
            newMetadata.Add("m", "m2");
            BuildItemGroup group = new BuildItemGroup();
            group.AddItem(item1);
            lookup.ModifyItems(item1.Name, group, newMetadata);

            // Get the item (under the covers, it cloned it in order to apply the modification)
            BuildItemGroup group2 = lookup.GetItems(item1.Name);
            Assertion.AssertEquals(1, group2.Count);
            BuildItem item1b = group2[0];

            // Modify to m=m3
            Dictionary<string, string> newMetadata2 = new Dictionary<string, string>();
            newMetadata2.Add("m", "m3");
            BuildItemGroup group3 = new BuildItemGroup();
            group3.AddItem(item1b);
            lookup.ModifyItems(item1b.Name, group3, newMetadata2);

            // Modifications are visible
            BuildItemGroup group4 = lookup.GetItems(item1b.Name);
            Assertion.AssertEquals(1, group4.Count);
            Assertion.AssertEquals("m3", group4[0].GetMetadata("m"));

            // Leave scope
            lookup.LeaveScope();

            // Still visible
            BuildItemGroup group5 = lookup.GetItems(item1b.Name);
            Assertion.AssertEquals(1, group5.Count);
            Assertion.AssertEquals("m3", group5[0].GetMetadata("m"));

            // And the one in the project is changed
            Assertion.AssertEquals("m3", item1.GetMetadata("m"));
        }

        /// <summary>
        /// After modification, should be able to GetItem and then remove it
        /// </summary>
        [Test]
        public void RemoveItemPreviouslyModifiedAndGottenThroughGetItem()
        {
            Hashtable table1 = new Hashtable(StringComparer.OrdinalIgnoreCase);
            Lookup lookup = LookupHelpers.CreateLookup(table1);

            // Add an item with m=m1 and n=n1 
            BuildItem item1 = new BuildItem("i1", "a2");
            item1.SetMetadata("m", "m1");
            lookup.PopulateWithItem(item1);

            lookup.EnterScope();

            // Make a modification to the item to be m=m2
            Dictionary<string, string> newMetadata = new Dictionary<string, string>();
            newMetadata.Add("m", "m2");
            BuildItemGroup group = new BuildItemGroup();
            group.AddItem(item1);
            lookup.ModifyItems(item1.Name, group, newMetadata);

            // Get the item (under the covers, it cloned it in order to apply the modification)
            BuildItemGroup group2 = lookup.GetItems(item1.Name);
            Assertion.AssertEquals(1, group2.Count);
            BuildItem item1b = group2[0];

            // Remove the item
            lookup.RemoveItem(item1b);

            // There's now no items at all
            BuildItemGroup group3 = lookup.GetItems(item1.Name);
            Assertion.AssertEquals(0, group3.Count);
        }

        /// <summary>
        /// After modification, should be able to GetItem and then remove it
        /// </summary>
        [Test]
        public void RemoveItemFromProjectPreviouslyModifiedAndGottenThroughGetItem()
        {
            // Create some project state with an item with m=m1 and n=n1 
            Hashtable table1 = new Hashtable(StringComparer.OrdinalIgnoreCase);
            BuildItem item1 = new BuildItem("i1", "a2");
            item1.SetMetadata("m", "m1");
            BuildItemGroup group0 = new BuildItemGroup();
            group0.AddExistingItem(item1);
            table1["i1"] = group0;

            Lookup lookup = LookupHelpers.CreateLookup(table1);

            lookup.EnterScope();

            // Make a modification to the item to be m=m2
            Dictionary<string, string> newMetadata = new Dictionary<string, string>();
            newMetadata.Add("m", "m2");
            BuildItemGroup group = new BuildItemGroup();
            group.AddItem(item1);
            lookup.ModifyItems(item1.Name, group, newMetadata);

            // Get the item (under the covers, it cloned it in order to apply the modification)
            BuildItemGroup group2 = lookup.GetItems(item1.Name);
            Assertion.AssertEquals(1, group2.Count);
            BuildItem item1b = group2[0];

            // Remove the item
            lookup.RemoveItem(item1b);

            // There's now no items at all
            BuildItemGroup group3 = lookup.GetItems(item1.Name);
            Assertion.AssertEquals(0, group3.Count);

            // Leave scope
            lookup.LeaveScope();

            // And now none left in the project either
            Assertion.AssertEquals(0, ((BuildItemGroup)table1["i1"]).Count);
        }

        /// <summary>
        /// If the property isn't modified, the initial property
        /// should be returned
        /// </summary>
        [Test]
        public void UnmodifiedProperty()
        {
            BuildPropertyGroup group = new BuildPropertyGroup();
            BuildProperty property = new BuildProperty("p1", "v1");
            group.SetProperty(property);
            Lookup lookup = LookupHelpers.CreateLookup(group);

            Assertion.AssertEquals(property, lookup.GetProperty("p1"));

            lookup.EnterScope();

            Assertion.AssertEquals(property, lookup.GetProperty("p1"));
        }

        /// <summary>
        /// If the property isn't found, should return null
        /// </summary>
        [Test]
        public void NonexistentProperty()
        {
            BuildPropertyGroup group = new BuildPropertyGroup();
            Lookup lookup = LookupHelpers.CreateLookup(group);

            Assertion.AssertEquals(null, lookup.GetProperty("p1"));

            lookup.EnterScope();

            Assertion.AssertEquals(null, lookup.GetProperty("p1"));
        }

        /// <summary>
        /// If the property is modified, the updated value should be returned,
        /// both before and after leaving scope.
        /// </summary>
        [Test]
        public void ModifiedProperty()
        {
            BuildPropertyGroup group = new BuildPropertyGroup();
            group.SetProperty(new BuildProperty("p1", "v1"));
            Lookup lookup = LookupHelpers.CreateLookup(group);
            // Enter scope so that property sets are allowed on it
            lookup.EnterScope();

            // Change the property value
            lookup.SetProperty(new BuildProperty("p1", "v2", PropertyType.OutputProperty));

            // Lookup is updated, but not original item group
            Assertion.AssertEquals("v2", lookup.GetProperty("p1").FinalValue);
            Assertion.AssertEquals("v1", group["p1"].FinalValue);

            lookup.EnterScope();

            // Change the value again in the new scope 
            lookup.SetProperty(new BuildProperty("p1", "v3", PropertyType.OutputProperty));

            // Lookup is updated, but not the original item group
            Assertion.AssertEquals("v3", lookup.GetProperty("p1").FinalValue);
            Assertion.AssertEquals("v1", group["p1"].FinalValue);

            lookup.EnterScope();

            // Change the value again in the new scope 
            lookup.SetProperty(new BuildProperty("p1", "v4", PropertyType.OutputProperty));

            Assertion.AssertEquals("v4", lookup.GetProperty("p1").FinalValue);

            lookup.LeaveScope();

            Assertion.AssertEquals("v4", lookup.GetProperty("p1").FinalValue);              

            // Leave to the outer scope
            lookup.LeaveScope();
            lookup.LeaveScope();

            // Now the lookup and original group are updated
            Assertion.AssertEquals("v4", lookup.GetProperty("p1").FinalValue);
            Assertion.AssertEquals("v4", group["p1"].FinalValue);
        }

        [Test]
        [ExpectedException(typeof(InternalErrorException))]
        public void LeaveTooMuch()
        {
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            lookup.EnterScope();
            lookup.LeaveScope();
            lookup.LeaveScope();
        }

        [Test]
        [ExpectedException(typeof(InternalErrorException))]
        public void RemoveScopeOnDifferentThread()
        {
            Thread thread = new Thread(CreateLookupAndEnterScope);
            thread.Start();
            thread.Join();

            Assertion.AssertNotNull(lookupPassedBetweenThreads);
            lookupPassedBetweenThreads.LeaveScope();
        }

        [Test]
        [ExpectedException(typeof(InternalErrorException))]
        public void PopulateWithItemOnDifferentThread()
        {
            Thread thread = new Thread(CreateLookupAndEnterScope);
            thread.Start();
            thread.Join();

            Assertion.AssertNotNull(lookupPassedBetweenThreads);
            lookupPassedBetweenThreads.PopulateWithItem(new BuildItem("x", "y"));
        }

        [Test]
        [ExpectedException(typeof(InternalErrorException))]
        public void PopulateWithItemsOnDifferentThread()
        {
            Thread thread = new Thread(CreateLookupAndEnterScope);
            thread.Start();
            thread.Join();

            Assertion.AssertNotNull(lookupPassedBetweenThreads);
            lookupPassedBetweenThreads.PopulateWithItems("x", new BuildItemGroup());
        }

        [Test]
        [ExpectedException(typeof(InternalErrorException))]
        public void AddNewItemOnDifferentThread()
        {
            Thread thread = new Thread(CreateLookupAndEnterScope);
            thread.Start();
            thread.Join();

            Assertion.AssertNotNull(lookupPassedBetweenThreads);
            lookupPassedBetweenThreads.AddNewItem(new BuildItem("x", "y"));
        }

        [Test]
        [ExpectedException(typeof(InternalErrorException))]
        public void AddNewItemsOnDifferentThread()
        {
            Thread thread = new Thread(CreateLookupAndEnterScope);
            thread.Start();
            thread.Join();

            Assertion.AssertNotNull(lookupPassedBetweenThreads);
            lookupPassedBetweenThreads.AddNewItems(new BuildItemGroup());
        }

        [Test]
        [ExpectedException(typeof(InternalErrorException))]
        public void RemoveItemOnDifferentThread()
        {
            Thread thread = new Thread(CreateLookupAndEnterScope);
            thread.Start();
            thread.Join();

            Assertion.AssertNotNull(lookupPassedBetweenThreads);
            lookupPassedBetweenThreads.RemoveItem(new BuildItem("x", "y"));
        }

        [Test]
        [ExpectedException(typeof(InternalErrorException))]
        public void RemoveItemsOnDifferentThread()
        {
            Thread thread = new Thread(CreateLookupAndEnterScope);
            thread.Start();
            thread.Join();

            Assertion.AssertNotNull(lookupPassedBetweenThreads);
            List<BuildItem> list = new List<BuildItem>();
            list.Add(new BuildItem("x", "y"));
            lookupPassedBetweenThreads.RemoveItems(list);
        }

        [Test]
        [ExpectedException(typeof(InternalErrorException))]
        public void ModifyItemOnDifferentThread()
        {
            Thread thread = new Thread(CreateLookupAndEnterScope);
            thread.Start();
            thread.Join();

            Assertion.AssertNotNull(lookupPassedBetweenThreads);
            lookupPassedBetweenThreads.ModifyItems("x", new BuildItemGroup(), new Dictionary<string,string>());
        }

        [Test]
        [ExpectedException(typeof(InternalErrorException))]
        public void SetPropertyOnDifferentThread()
        {
            Thread thread = new Thread(CreateLookupAndEnterScope);
            thread.Start();
            thread.Join();

            Assertion.AssertNotNull(lookupPassedBetweenThreads);
            lookupPassedBetweenThreads.SetProperty(new BuildProperty("x", "y", PropertyType.OutputProperty));
        }

        /// <summary>
        /// Lame but simple way to get the lookup from another thread
        /// </summary>
        private static Lookup lookupPassedBetweenThreads;

        private void CreateLookupAndEnterScope()
        {
            lookupPassedBetweenThreads = LookupHelpers.CreateEmptyLookup();
            lookupPassedBetweenThreads.EnterScope();
        }
    }

    internal class LookupHelpers
    {
        internal static Lookup CreateEmptyLookup()
        {
            ItemDefinitionLibrary itemDefinitionLibrary = CreateEmptyEvaluatedItemDefinitionLibrary();
            Lookup lookup = new Lookup(null, null, itemDefinitionLibrary);
            return lookup;
        }

        internal static Lookup CreateLookup(Hashtable items)
        {
            ItemDefinitionLibrary itemDefinitionLibrary = CreateEmptyEvaluatedItemDefinitionLibrary();
            Lookup lookup = new Lookup(items, null, itemDefinitionLibrary);
            return lookup;
        }

        internal static Lookup CreateLookup(BuildPropertyGroup properties)
        {
            ItemDefinitionLibrary itemDefinitionLibrary = CreateEmptyEvaluatedItemDefinitionLibrary();
            Lookup lookup = new Lookup(null, properties, itemDefinitionLibrary);
            return lookup;
        }

        internal static Lookup CreateLookup(BuildPropertyGroup properties, Hashtable items)
        {
            ItemDefinitionLibrary itemDefinitionLibrary = CreateEmptyEvaluatedItemDefinitionLibrary();
            Lookup lookup = new Lookup(items, properties, itemDefinitionLibrary);
            return lookup;
        }

        internal static ItemDefinitionLibrary CreateEmptyEvaluatedItemDefinitionLibrary()
        {
            ItemDefinitionLibrary itemDefinitionLibrary = new ItemDefinitionLibrary(new Project());
            itemDefinitionLibrary.Evaluate(new BuildPropertyGroup());
            return itemDefinitionLibrary;
        }    
    }
}
