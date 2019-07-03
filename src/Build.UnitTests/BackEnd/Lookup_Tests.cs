// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using System.Xml;
using System.Threading;
using System.Diagnostics;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd
{
    public class Lookup_Tests
    {
        /// <summary>
        /// Primary group contains an item for a type and secondary does;
        /// primary item should be returned instead of the secondary item.
        /// </summary>
        [Fact]
        public void SecondaryItemShadowedByPrimaryItem()
        {
            ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
            ItemDictionary<ProjectItemInstance> table1 = new ItemDictionary<ProjectItemInstance>();
            table1.Add(new ProjectItemInstance(project, "i1", "a1", project.FullPath));
            table1.Add(new ProjectItemInstance(project, "i2", "a%3b1", project.FullPath));
            Lookup lookup = LookupHelpers.CreateLookup(table1);

            lookup.EnterScope("x");
            lookup.PopulateWithItem(new ProjectItemInstance(project, "i1", "a2", project.FullPath));
            lookup.PopulateWithItem(new ProjectItemInstance(project, "i2", "a%282", project.FullPath));

            // Should return the item from the primary, not the secondary table
            Assert.Equal("a2", lookup.GetItems("i1").First().EvaluatedInclude);
            Assert.Equal("a(2", lookup.GetItems("i2").First().EvaluatedInclude);
        }

        /// <summary>
        /// Primary group does not contain an item for a type but secondary does;
        /// secondary item should be returned.
        /// </summary>
        [Fact]
        public void SecondaryItemNotShadowedByPrimaryItem()
        {
            ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
            ItemDictionary<ProjectItemInstance> table1 = new ItemDictionary<ProjectItemInstance>();
            table1.Add(new ProjectItemInstance(project, "i1", "a1", project.FullPath));
            table1.Add(new ProjectItemInstance(project, "i2", "a%3b1", project.FullPath));
            Lookup lookup = LookupHelpers.CreateLookup(table1);

            lookup.EnterScope("x");

            // Should return item from the secondary table.
            Assert.Equal("a1", lookup.GetItems("i1").First().EvaluatedInclude);
            Assert.Equal("a;1", lookup.GetItems("i2").First().EvaluatedInclude);
        }

        /// <summary>
        /// No items of that type: should return empty group rather than null
        /// </summary>
        [Fact]
        public void UnknownItemType()
        {
            Lookup lookup = LookupHelpers.CreateEmptyLookup();

            lookup.EnterScope("x"); // Doesn't matter really

            Assert.Empty(lookup.GetItems("i1"));
        }

        /// <summary>
        /// Adds accumulate as we lookup in the tables
        /// </summary>
        [Fact]
        public void AddsAreCombinedWithPopulates()
        {
            ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
            // One item in the project
            ItemDictionary<ProjectItemInstance> table1 = new ItemDictionary<ProjectItemInstance>();
            table1.Add(new ProjectItemInstance(project, "i1", "a1", project.FullPath));
            Lookup lookup = LookupHelpers.CreateLookup(table1);

            // We see the one item
            Assert.Equal("a1", lookup.GetItems("i1").First().EvaluatedInclude);
            Assert.Single(lookup.GetItems("i1"));

            // One item in the project
            Assert.Equal("a1", table1["i1"].First().EvaluatedInclude);
            Assert.Single(table1["i1"]);

            // Start a target
            Lookup.Scope enteredScope = lookup.EnterScope("x");

            // We see the one item 
            Assert.Equal("a1", lookup.GetItems("i1").First().EvaluatedInclude);
            Assert.Single(lookup.GetItems("i1"));

            // One item in the project
            Assert.Equal("a1", table1["i1"].First().EvaluatedInclude);
            Assert.Single(table1["i1"]);

            // Start a task (eg) and add a new item
            Lookup.Scope enteredScope2 = lookup.EnterScope("x");
            lookup.AddNewItem(new ProjectItemInstance(project, "i1", "a2", project.FullPath));

            // Now we see two items
            Assert.Equal("a1", lookup.GetItems("i1").First().EvaluatedInclude);
            Assert.Equal("a2", lookup.GetItems("i1").ElementAt(1).EvaluatedInclude);
            Assert.Equal(2, lookup.GetItems("i1").Count);

            // But there's still one item in the project
            Assert.Equal("a1", table1["i1"].First().EvaluatedInclude);
            Assert.Single(table1["i1"]);

            // Finish the task
            enteredScope2.LeaveScope();

            // We still see two items
            Assert.Equal("a1", lookup.GetItems("i1").First().EvaluatedInclude);
            Assert.Equal("a2", lookup.GetItems("i1").ElementAt(1).EvaluatedInclude);
            Assert.Equal(2, lookup.GetItems("i1").Count);

            // But there's still one item in the project
            Assert.Equal("a1", table1["i1"].First().EvaluatedInclude);
            Assert.Single(table1["i1"]);

            // Finish the target
            enteredScope.LeaveScope();

            // We still see two items
            Assert.Equal("a1", lookup.GetItems("i1").First().EvaluatedInclude);
            Assert.Equal("a2", lookup.GetItems("i1").ElementAt(1).EvaluatedInclude);
            Assert.Equal(2, lookup.GetItems("i1").Count);

            // And now the items have gotten put into the global group
            Assert.Equal("a1", table1["i1"].First().EvaluatedInclude);
            Assert.Equal("a2", table1["i1"].ElementAt(1).EvaluatedInclude);
            Assert.Equal(2, table1["i1"].Count);
        }

        /// <summary>
        /// Adds when duplicate removal is enabled removes only duplicates.  Tests only item specs, not metadata differences
        /// </summary>
        [Fact]
        public void AddsWithDuplicateRemovalItemSpecsOnly()
        {
            ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
            // One item in the project
            ItemDictionary<ProjectItemInstance> table1 = new ItemDictionary<ProjectItemInstance>();
            table1.Add(new ProjectItemInstance(project, "i1", "a1", project.FullPath));

            // Add an existing duplicate
            table1.Add(new ProjectItemInstance(project, "i1", "a1", project.FullPath));
            Lookup lookup = LookupHelpers.CreateLookup(table1);

            var scope = lookup.EnterScope("test");

            // This one should not get added
            ProjectItemInstance[] newItems = new ProjectItemInstance[]
            {
                new ProjectItemInstance(project, "i1", "a1", project.FullPath), // Should not get added
                new ProjectItemInstance(project, "i1", "a2", project.FullPath), // Should get added               
            };

            // Perform the addition
            lookup.AddNewItemsOfItemType("i1", newItems, doNotAddDuplicates: true);

            var group = lookup.GetItems("i1");

            // We should have the original two duplicates plus one new addition.
            Assert.Equal(3, group.Count);

            // Only two of the items should have the 'a1' include.
            Assert.Equal(2, group.Where(item => item.EvaluatedInclude == "a1").Count());
            // And ensure the other item got added.
            Assert.Single(group.Where(item => item.EvaluatedInclude == "a2"));

            scope.LeaveScope();

            group = lookup.GetItems("i1");

            // We should have the original two duplicates plus one new addition.
            Assert.Equal(3, group.Count);

            // Only two of the items should have the 'a1' include.
            Assert.Equal(2, group.Where(item => item.EvaluatedInclude == "a1").Count());
            // And ensure the other item got added.
            Assert.Single(group.Where(item => item.EvaluatedInclude == "a2"));
        }

        /// <summary>
        /// Adds when duplicate removal is enabled removes only duplicates.  Tests only item specs, not metadata differences
        /// </summary>
        [Fact]
        public void AddsWithDuplicateRemovalWithMetadata()
        {
            ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();

            ItemDictionary<ProjectItemInstance> table1 = new ItemDictionary<ProjectItemInstance>();

            // Two items, differ only by metadata
            table1.Add(new ProjectItemInstance(project, "i1", "a1", new KeyValuePair<string, string>[] { new KeyValuePair<string, string>("m1", "m1") }, project.FullPath));
            table1.Add(new ProjectItemInstance(project, "i1", "a1", new KeyValuePair<string, string>[] { new KeyValuePair<string, string>("m1", "m2") }, project.FullPath));
            Lookup lookup = LookupHelpers.CreateLookup(table1);

            var scope = lookup.EnterScope("test");

            // This one should not get added
            ProjectItemInstance[] newItems = new ProjectItemInstance[]
            {
                new ProjectItemInstance(project, "i1", "a1", project.FullPath), // Should get added
                new ProjectItemInstance(project, "i1", "a2", new KeyValuePair<string, string>[] { new KeyValuePair<string, string>( "m1", "m1" ) }, project.FullPath), // Should get added               
                new ProjectItemInstance(project, "i1", "a1", new KeyValuePair<string, string>[] { new KeyValuePair<string, string>( "m1", "m1" ) }, project.FullPath), // Should not get added               
                new ProjectItemInstance(project, "i1", "a1", new KeyValuePair<string, string>[] { new KeyValuePair<string, string>( "m1", "m3" ) }, project.FullPath), // Should get added               
            };

            // Perform the addition
            lookup.AddNewItemsOfItemType("i1", newItems, doNotAddDuplicates: true);

            var group = lookup.GetItems("i1");

            // We should have the original two duplicates plus one new addition.
            Assert.Equal(5, group.Count);

            // Four of the items will have the a1 include
            Assert.Equal(4, group.Where(item => item.EvaluatedInclude == "a1").Count());

            // One item will have the a2 include
            Assert.Single(group.Where(item => item.EvaluatedInclude == "a2"));

            scope.LeaveScope();

            group = lookup.GetItems("i1");

            // We should have the original two duplicates plus one new addition.
            Assert.Equal(5, group.Count);

            // Four of the items will have the a1 include
            Assert.Equal(4, group.Where(item => item.EvaluatedInclude == "a1").Count());

            // One item will have the a2 include
            Assert.Single(group.Where(item => item.EvaluatedInclude == "a2"));
        }

        [Fact]
        public void Removes()
        {
            ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
            // One item in the project
            ItemDictionary<ProjectItemInstance> table1 = new ItemDictionary<ProjectItemInstance>();
            ProjectItemInstance item1 = new ProjectItemInstance(project, "i1", "a1", project.FullPath);
            table1.Add(item1);
            Lookup lookup = LookupHelpers.CreateLookup(table1);

            // Start a target
            Lookup.Scope enteredScope = lookup.EnterScope("x");

            // Start a task (eg) and add a new item
            Lookup.Scope enteredScope2 = lookup.EnterScope("x");
            ProjectItemInstance item2 = new ProjectItemInstance(project, "i1", "a2", project.FullPath);
            lookup.AddNewItem(item2);

            // Remove one item
            lookup.RemoveItem(item1);

            // We see one item
            Assert.Single(lookup.GetItems("i1"));
            Assert.Equal("a2", lookup.GetItems("i1").First().EvaluatedInclude);

            // Remove the other item
            lookup.RemoveItem(item2);

            // We see no items
            Assert.Empty(lookup.GetItems("i1"));

            // Finish the task
            enteredScope2.LeaveScope();

            // We still see no items
            Assert.Empty(lookup.GetItems("i1"));

            // But there's still one item in the project
            Assert.Equal("a1", table1["i1"].First().EvaluatedInclude);
            Assert.Single(table1["i1"]);

            // Finish the target
            enteredScope.LeaveScope();

            // We still see no items
            Assert.Empty(lookup.GetItems("i1"));

            // And now there are no items in the project either
            Assert.Empty(table1["i1"]);
        }

        [Fact]
        public void RemoveItemPopulatedInLowerScope()
        {
            ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
            ItemDictionary<ProjectItemInstance> table1 = new ItemDictionary<ProjectItemInstance>();
            Lookup lookup = LookupHelpers.CreateLookup(table1);
            ProjectItemInstance item1 = new ProjectItemInstance(project, "i1", "a2", project.FullPath);

            // Start a target
            Lookup.Scope enteredScope = lookup.EnterScope("x");

            // There's one item in this batch
            lookup.PopulateWithItem(item1);

            // We see it
            Assert.Single(lookup.GetItems("i1"));

            // Make a clone so we can keep an eye on that item
            Lookup lookup2 = lookup.Clone();

            // We can see the item in the clone
            Assert.Single(lookup2.GetItems("i1"));

            // Start a task (eg)
            Lookup.Scope enteredScope2 = lookup.EnterScope("x");

            // We see the item below
            Assert.Single(lookup.GetItems("i1"));

            // Remove that item
            lookup.RemoveItem(item1);

            // We see no items
            Assert.Empty(lookup.GetItems("i1"));

            // The clone is unaffected so far
            Assert.Single(lookup2.GetItems("i1"));

            // Finish the task
            enteredScope2.LeaveScope();

            // We still see no items
            Assert.Empty(lookup.GetItems("i1"));

            // But now the clone doesn't either
            Assert.Empty(lookup2.GetItems("i1"));

            // Finish the target
            enteredScope.LeaveScope();

            // We still see no items
            Assert.Empty(lookup.GetItems("i1"));
            Assert.Empty(lookup2.GetItems("i1"));
        }

        [Fact]
        public void RemoveItemAddedInLowerScope()
        {
            ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
            ItemDictionary<ProjectItemInstance> table1 = new ItemDictionary<ProjectItemInstance>();
            Lookup lookup = LookupHelpers.CreateLookup(table1);

            // Start a target
            Lookup.Scope enteredScope = lookup.EnterScope("x");

            // Add an item
            ProjectItemInstance item1 = new ProjectItemInstance(project, "i1", "a2", project.FullPath);
            lookup.AddNewItem(item1);

            // Start a task (eg)
            Lookup.Scope enteredScope2 = lookup.EnterScope("x");

            // We see the item below
            Assert.Single(lookup.GetItems("i1"));

            // Remove that item
            lookup.RemoveItem(item1);

            // We see no items
            Assert.Empty(lookup.GetItems("i1"));

            // Finish the task
            enteredScope2.LeaveScope();

            // We still see no items
            Assert.Empty(lookup.GetItems("i1"));

            // Finish the target
            enteredScope.LeaveScope();

            // We still see no items
            Assert.Empty(lookup.GetItems("i1"));
        }

        /// <summary>
        /// Ensure that once keepOnlySpecified is set to true, it remains in effect.
        /// </summary>
        [Fact]
        public void KeepMetadataOnlySpecifiedPropagate1()
        {
            ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
            ItemDictionary<ProjectItemInstance> table1 = new ItemDictionary<ProjectItemInstance>();
            Lookup lookup = LookupHelpers.CreateLookup(table1);

            Lookup.Scope enteredScope = lookup.EnterScope("x");

            // Add an item with m=m1
            ProjectItemInstance item1 = new ProjectItemInstance(project, "i1", "a2", project.FullPath);
            item1.SetMetadata("m1", "m1");
            item1.SetMetadata("m2", "m2");
            lookup.AddNewItem(item1);

            Lookup.Scope enteredScope2 = lookup.EnterScope("x");

            // Get rid of all of the metadata.
            Lookup.MetadataModifications newMetadata = new Lookup.MetadataModifications(keepOnlySpecified: true);
            ICollection<ProjectItemInstance> group = lookup.GetItems(item1.ItemType);
            lookup.ModifyItems(item1.ItemType, group, newMetadata);

            group = lookup.GetItems("i1");
            Assert.Single(group);

            // m1 and m2 are gone.
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m1"));
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m2"));

            enteredScope2.LeaveScope();

            // Add metadata m3.
            Lookup.MetadataModifications newMetadata2 = new Lookup.MetadataModifications(keepOnlySpecified: false);
            newMetadata2.Add("m3", "m3");
            group = lookup.GetItems(item1.ItemType);
            lookup.ModifyItems(item1.ItemType, group, newMetadata2);

            group = lookup.GetItems("i1");
            Assert.Single(group);

            // m1 and m2 are gone
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m1"));
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m2"));

            // m3 is still there.
            Assert.Equal("m3", group.First().GetMetadataValue("m3"));

            enteredScope.LeaveScope();

            group = lookup.GetItems("i1");
            Assert.Single(group);

            // m1 and m2 are gone
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m1"));
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m2"));

            // m3 is still there.
            Assert.Equal("m3", group.First().GetMetadataValue("m3"));
        }

        /// <summary>
        /// Ensure that if keepOnlySpecified is specified after some metadata have been set in a higher scope that it will
        /// eliminate that metadata are the current scope and beyond.
        /// </summary>
        [Fact]
        public void KeepMetadataOnlySpecifiedPropagate2()
        {
            ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
            ItemDictionary<ProjectItemInstance> table1 = new ItemDictionary<ProjectItemInstance>();
            Lookup lookup = LookupHelpers.CreateLookup(table1);

            Lookup.Scope enteredScope = lookup.EnterScope("x");

            // Add an item with m=m1
            ProjectItemInstance item1 = new ProjectItemInstance(project, "i1", "a2", project.FullPath);
            item1.SetMetadata("m1", "m1");
            item1.SetMetadata("m2", "m2");
            lookup.AddNewItem(item1);

            Lookup.Scope enteredScope2 = lookup.EnterScope("x");

            // Add m3 metadata
            Lookup.MetadataModifications newMetadata = new Lookup.MetadataModifications(keepOnlySpecified: false);
            newMetadata.Add("m3", "m3");
            ICollection<ProjectItemInstance> group = lookup.GetItems(item1.ItemType);
            lookup.ModifyItems(item1.ItemType, group, newMetadata);

            group = lookup.GetItems("i1");
            Assert.Single(group);

            // All metadata are present
            Assert.Equal("m1", group.First().GetMetadataValue("m1"));
            Assert.Equal("m2", group.First().GetMetadataValue("m2"));
            Assert.Equal("m3", group.First().GetMetadataValue("m3"));

            enteredScope2.LeaveScope();

            // Now clear metadata
            Lookup.MetadataModifications newMetadata2 = new Lookup.MetadataModifications(keepOnlySpecified: true);
            group = lookup.GetItems(item1.ItemType);
            lookup.ModifyItems(item1.ItemType, group, newMetadata2);

            group = lookup.GetItems("i1");
            Assert.Single(group);

            // All metadata are gone
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m1"));
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m2"));
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m3"));

            enteredScope.LeaveScope();

            group = lookup.GetItems("i1");
            Assert.Single(group);

            // All metadata are gone
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m1"));
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m2"));
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m3"));
        }

        /// <summary>
        /// Ensure that once keepOnlySpecified is set to true, it remains in effect, but that metadata explicitly added at subsequent levels is still retained.
        /// </summary>
        [Fact]
        public void KeepMetadataOnlySpecifiedPropagate3()
        {
            ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
            ItemDictionary<ProjectItemInstance> table1 = new ItemDictionary<ProjectItemInstance>();
            Lookup lookup = LookupHelpers.CreateLookup(table1);

            Lookup.Scope enteredScope = lookup.EnterScope("x");

            // Add an item with m=m1
            ProjectItemInstance item1 = new ProjectItemInstance(project, "i1", "a2", project.FullPath);
            item1.SetMetadata("m1", "m1");
            item1.SetMetadata("m2", "m2");
            lookup.AddNewItem(item1);

            Lookup.Scope enteredScope2 = lookup.EnterScope("x");

            // Get rid of all of the metadata, then add m3
            Lookup.MetadataModifications newMetadata = new Lookup.MetadataModifications(keepOnlySpecified: true);
            newMetadata.Add("m3", "m3");
            ICollection<ProjectItemInstance> group = lookup.GetItems(item1.ItemType);
            lookup.ModifyItems(item1.ItemType, group, newMetadata);

            group = lookup.GetItems("i1");
            Assert.Single(group);

            // m1 and m2 are gone.
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m1"));
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m2"));

            // m3 is still there.
            Assert.Equal("m3", group.First().GetMetadataValue("m3"));

            enteredScope2.LeaveScope();

            // Add metadata m4.
            Lookup.MetadataModifications newMetadata2 = new Lookup.MetadataModifications(keepOnlySpecified: true);
            newMetadata2.Add("m4", "m4");
            group = lookup.GetItems(item1.ItemType);
            lookup.ModifyItems(item1.ItemType, group, newMetadata2);

            group = lookup.GetItems("i1");
            Assert.Single(group);

            // m1, m2 and m3 are gone
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m1"));
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m2"));
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m3"));

            // m4 is still there.
            Assert.Equal("m4", group.First().GetMetadataValue("m4"));

            enteredScope.LeaveScope();

            group = lookup.GetItems("i1");
            Assert.Single(group);

            // m1, m2 and m3 are gone
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m1"));
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m2"));
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m3"));

            // m4 is still there.
            Assert.Equal("m4", group.First().GetMetadataValue("m4"));
        }


        /// <summary>
        /// Ensure that once keepOnlySpecified is set to true, it remains in effect, and that if a metadata modification is declared as 'keep value' that
        /// the value as lower scopes is retained.
        /// </summary>
        [Fact]
        public void KeepMetadataOnlySpecifiedPropagate4()
        {
            ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
            ItemDictionary<ProjectItemInstance> table1 = new ItemDictionary<ProjectItemInstance>();
            Lookup lookup = LookupHelpers.CreateLookup(table1);

            Lookup.Scope enteredScope = lookup.EnterScope("x");

            // Add an item with m=m1
            ProjectItemInstance item1 = new ProjectItemInstance(project, "i1", "a2", project.FullPath);
            item1.SetMetadata("m1", "m1");
            item1.SetMetadata("m2", "m2");
            lookup.AddNewItem(item1);

            Lookup.Scope enteredScope2 = lookup.EnterScope("x");

            // Get rid of all of the metadata, then add m3
            Lookup.MetadataModifications newMetadata = new Lookup.MetadataModifications(keepOnlySpecified: true);
            newMetadata.Add("m3", "m3");
            ICollection<ProjectItemInstance> group = lookup.GetItems(item1.ItemType);
            lookup.ModifyItems(item1.ItemType, group, newMetadata);

            group = lookup.GetItems("i1");
            Assert.Single(group);

            // m1 and m2 are gone.
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m1"));
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m2"));

            // m3 is still there.
            Assert.Equal("m3", group.First().GetMetadataValue("m3"));

            enteredScope2.LeaveScope();

            // Keep m3.
            Lookup.MetadataModifications newMetadata2 = new Lookup.MetadataModifications(keepOnlySpecified: true);
            newMetadata2["m3"] = Lookup.MetadataModification.CreateFromNoChange();
            group = lookup.GetItems(item1.ItemType);
            lookup.ModifyItems(item1.ItemType, group, newMetadata2);

            group = lookup.GetItems("i1");
            Assert.Single(group);

            // m1 and m2 are gone
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m1"));
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m2"));

            // m3 is still there
            Assert.Equal("m3", group.First().GetMetadataValue("m3"));

            enteredScope.LeaveScope();

            group = lookup.GetItems("i1");
            Assert.Single(group);

            // m1 and m2 are gone
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m1"));
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m2"));

            // m3 is still there.
            Assert.Equal("m3", group.First().GetMetadataValue("m3"));
        }

        /// <summary>
        /// Ensure that when keepOnlySpecified is true, we will clear all metadata unless it is retained using the 'NoChange' modification type.
        /// </summary>
        [Fact]
        public void KeepMetadataOnlySpecified()
        {
            ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
            ItemDictionary<ProjectItemInstance> table1 = new ItemDictionary<ProjectItemInstance>();
            Lookup lookup = LookupHelpers.CreateLookup(table1);

            Lookup.Scope enteredScope = lookup.EnterScope("x");

            // Add an item with m=m1
            ProjectItemInstance item1 = new ProjectItemInstance(project, "i1", "a2", project.FullPath);
            item1.SetMetadata("m1", "m1");
            item1.SetMetadata("m2", "m2");
            lookup.AddNewItem(item1);

            Lookup.Scope enteredScope2 = lookup.EnterScope("x");

            // Test keeping only specified metadata
            Lookup.MetadataModifications newMetadata = new Lookup.MetadataModifications(keepOnlySpecified: true);
            newMetadata["m1"] = Lookup.MetadataModification.CreateFromNoChange();
            ICollection<ProjectItemInstance> group = lookup.GetItems(item1.ItemType);
            lookup.ModifyItems(item1.ItemType, group, newMetadata);

            group = lookup.GetItems("i1");
            Assert.Single(group);

            // m1 is still here.
            Assert.Equal("m1", group.First().GetMetadataValue("m1"));

            // m2 is gone
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m2"));

            enteredScope2.LeaveScope();

            group = lookup.GetItems("i1");
            Assert.Single(group);

            // m1 should still be here
            Assert.Equal("m1", group.First().GetMetadataValue("m1"));

            // m2 is gone.
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m2"));

            enteredScope.LeaveScope();

            group = lookup.GetItems("i1");
            Assert.Single(group);

            // m1 should still be here
            Assert.Equal("m1", group.First().GetMetadataValue("m1"));

            // m2 should not persist here either
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m2"));
        }

        [Fact]
        public void KeepMetadataOnlySpecifiedNoneSpecified()
        {
            ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
            ItemDictionary<ProjectItemInstance> table1 = new ItemDictionary<ProjectItemInstance>();
            Lookup lookup = LookupHelpers.CreateLookup(table1);

            Lookup.Scope enteredScope = lookup.EnterScope("x");

            // Add an item with m=m1
            ProjectItemInstance item1 = new ProjectItemInstance(project, "i1", "a2", project.FullPath);
            item1.SetMetadata("m1", "m1");
            item1.SetMetadata("m2", "m2");
            lookup.AddNewItem(item1);

            Lookup.Scope enteredScope2 = lookup.EnterScope("x");

            // Test keeping only specified metadata
            Lookup.MetadataModifications newMetadata = new Lookup.MetadataModifications(keepOnlySpecified: true);
            ICollection<ProjectItemInstance> group = lookup.GetItems(item1.ItemType);
            lookup.ModifyItems(item1.ItemType, group, newMetadata);

            group = lookup.GetItems("i1");
            Assert.Single(group);

            // m1 and m2 are gone.
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m1"));
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m2"));

            enteredScope2.LeaveScope();

            group = lookup.GetItems("i1");
            Assert.Single(group);

            // m1 and m2 are gone.
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m1"));
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m2"));

            enteredScope.LeaveScope();

            group = lookup.GetItems("i1");
            Assert.Single(group);

            // m1 and m2 are gone.
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m1"));
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m2"));
        }

        [Fact]
        public void ModifyItem()
        {
            ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
            ItemDictionary<ProjectItemInstance> table1 = new ItemDictionary<ProjectItemInstance>();
            Lookup lookup = LookupHelpers.CreateLookup(table1);

            Lookup.Scope enteredScope = lookup.EnterScope("x");

            // Add an item with m=m1
            ProjectItemInstance item1 = new ProjectItemInstance(project, "i1", "a2", project.FullPath);
            item1.SetMetadata("m", "m1");
            lookup.AddNewItem(item1);

            Lookup.Scope enteredScope2 = lookup.EnterScope("x");

            // Change the item to be m=m2
            Lookup.MetadataModifications newMetadata = new Lookup.MetadataModifications(keepOnlySpecified: false);
            newMetadata.Add("m", "m2");
            ICollection<ProjectItemInstance> group = new List<ProjectItemInstance>();
            group.Add(item1);
            lookup.ModifyItems(item1.ItemType, group, newMetadata);

            // Now it has m=m2
            group = lookup.GetItems("i1");
            Assert.Single(group);
            Assert.Equal("m2", group.First().GetMetadataValue("m"));

            // But the original item hasn't changed yet
            Assert.Equal("m1", item1.GetMetadataValue("m"));

            enteredScope2.LeaveScope();

            // It still has m=m2
            group = lookup.GetItems("i1");
            Assert.Single(group);
            Assert.Equal("m2", group.First().GetMetadataValue("m"));

            // The original item still hasn't changed
            // even though it was added in this scope
            Assert.Equal("m1", item1.GetMetadataValue("m"));

            enteredScope.LeaveScope();

            // It still has m=m2
            group = lookup.GetItems("i1");
            Assert.Single(group);
            Assert.Equal("m2", group.First().GetMetadataValue("m"));

            // But now the original item has changed
            Assert.Equal("m2", item1.GetMetadataValue("m"));
        }

        /// <summary>
        /// Modifications should be merged
        /// </summary>
        [Fact]
        public void ModifyItemModifiedInPreviousScope()
        {
            ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
            ItemDictionary<ProjectItemInstance> table1 = new ItemDictionary<ProjectItemInstance>();
            Lookup lookup = LookupHelpers.CreateLookup(table1);

            // Add an item with m=m1 and n=n1 
            ProjectItemInstance item1 = new ProjectItemInstance(project, "i1", "a2", project.FullPath);
            item1.SetMetadata("m", "m1");
            lookup.PopulateWithItem(item1);

            lookup.EnterScope("x");

            // Make a modification to the item to be m=m2
            Lookup.MetadataModifications newMetadata = new Lookup.MetadataModifications(keepOnlySpecified: false);
            newMetadata.Add("m", "m2");
            newMetadata.Add("n", "n2");
            ICollection<ProjectItemInstance> group = new List<ProjectItemInstance>();
            group.Add(item1);
            lookup.ModifyItems(item1.ItemType, group, newMetadata);

            lookup.EnterScope("x");

            // Make another modification to the item
            newMetadata = new Lookup.MetadataModifications(keepOnlySpecified: false);
            newMetadata.Add("m", "m3");
            newMetadata.Add("o", "o3");
            lookup.ModifyItems(item1.ItemType, group, newMetadata);

            // It's now m=m3, n=n2, o=o3
            group = lookup.GetItems("i1");
            Assert.Single(group);
            Assert.Equal("m3", group.First().GetMetadataValue("m"));
            Assert.Equal("n2", group.First().GetMetadataValue("n"));
            Assert.Equal("o3", group.First().GetMetadataValue("o"));
        }

        /// <summary>
        /// Modifications should be merged
        /// </summary>
        [Fact]
        public void ModifyItemTwiceInSameScope1()
        {
            ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
            ItemDictionary<ProjectItemInstance> table1 = new ItemDictionary<ProjectItemInstance>();
            Lookup lookup = LookupHelpers.CreateLookup(table1);

            // Add an item with m=m1 and n=n1 
            ProjectItemInstance item1 = new ProjectItemInstance(project, "i1", "a2", project.FullPath);
            item1.SetMetadata("m", "m1");
            lookup.PopulateWithItem(item1);

            lookup.EnterScope("x");

            // Make a modification to the item to be m=m2
            Lookup.MetadataModifications newMetadata = new Lookup.MetadataModifications(keepOnlySpecified: false);
            newMetadata.Add("m", "m2");
            ICollection<ProjectItemInstance> group = new List<ProjectItemInstance>();
            group.Add(item1);
            lookup.ModifyItems(item1.ItemType, group, newMetadata);

            // Make an unrelated modification to the item
            newMetadata = new Lookup.MetadataModifications(keepOnlySpecified: false);
            newMetadata.Add("n", "n1");
            lookup.ModifyItems(item1.ItemType, group, newMetadata);

            // It's now m=m2
            group = lookup.GetItems("i1");
            Assert.Single(group);
            Assert.Equal("m2", group.First().GetMetadataValue("m"));
        }

        /// <summary>
        /// Modifications should be merged
        /// </summary>
        [Fact]
        public void ModifyItemTwiceInSameScope2()
        {
            ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
            ItemDictionary<ProjectItemInstance> table1 = new ItemDictionary<ProjectItemInstance>();
            Lookup lookup = LookupHelpers.CreateLookup(table1);

            // Add an item with m=m1 and n=n1 and o=o1
            ProjectItemInstance item1 = new ProjectItemInstance(project, "i1", "a2", project.FullPath);
            item1.SetMetadata("m", "m1");
            item1.SetMetadata("n", "n1");
            item1.SetMetadata("o", "o1");
            lookup.PopulateWithItem(item1);

            Lookup.Scope enteredScope = lookup.EnterScope("x");

            // It's still m=m1, n=n1, o=o1
            ICollection<ProjectItemInstance> group = lookup.GetItems("i1");
            Assert.Single(group);
            Assert.Equal("m1", group.First().GetMetadataValue("m"));
            Assert.Equal("n1", group.First().GetMetadataValue("n"));
            Assert.Equal("o1", group.First().GetMetadataValue("o"));

            // Make a modification to the item to be m=m2 and n=n2
            Lookup.MetadataModifications newMetadata = new Lookup.MetadataModifications(keepOnlySpecified: false);
            newMetadata.Add("m", "m2");
            newMetadata.Add("n", "n2");
            group = new List<ProjectItemInstance>();
            group.Add(item1);
            lookup.ModifyItems("i1", group, newMetadata);

            // It's now m=m2, n=n2, o=o1
            ICollection<ProjectItemInstance> foundGroup = lookup.GetItems("i1");
            Assert.Single(foundGroup);
            Assert.Equal("m2", foundGroup.First().GetMetadataValue("m"));
            Assert.Equal("n2", foundGroup.First().GetMetadataValue("n"));
            Assert.Equal("o1", foundGroup.First().GetMetadataValue("o"));

            // Make a modification to the item to be n=n3 
            newMetadata = new Lookup.MetadataModifications(keepOnlySpecified: false);
            newMetadata.Add("n", "n3");
            lookup.ModifyItems("i1", group, newMetadata);

            // It's now m=m2, n=n3, o=o1
            foundGroup = lookup.GetItems("i1");
            Assert.Single(foundGroup);
            Assert.Equal("m2", foundGroup.First().GetMetadataValue("m"));
            Assert.Equal("n3", foundGroup.First().GetMetadataValue("n"));
            Assert.Equal("o1", foundGroup.First().GetMetadataValue("o"));

            // But the original item hasn't changed yet
            Assert.Equal("m1", item1.GetMetadataValue("m"));
            Assert.Equal("n1", item1.GetMetadataValue("n"));
            Assert.Equal("o1", item1.GetMetadataValue("o"));

            enteredScope.LeaveScope();

            // It's still m=m2, n=n3, o=o1
            foundGroup = lookup.GetItems("i1");
            Assert.Single(foundGroup);
            Assert.Equal("m2", foundGroup.First().GetMetadataValue("m"));
            Assert.Equal("n3", foundGroup.First().GetMetadataValue("n"));
            Assert.Equal("o1", foundGroup.First().GetMetadataValue("o"));

            // And the original item has changed
            Assert.Equal("m2", item1.GetMetadataValue("m"));
            Assert.Equal("n3", item1.GetMetadataValue("n"));
            Assert.Equal("o1", item1.GetMetadataValue("o"));
        }


        [Fact]
        public void ModifyItemThatWasAddedInSameScope()
        {
            ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
            ItemDictionary<ProjectItemInstance> table1 = new ItemDictionary<ProjectItemInstance>();
            Lookup lookup = LookupHelpers.CreateLookup(table1);

            Lookup.Scope enteredScope = lookup.EnterScope("x");

            // Add an item with m=m1
            ProjectItemInstance item1 = new ProjectItemInstance(project, "i1", "a2", project.FullPath);
            item1.SetMetadata("m", "m1");
            lookup.AddNewItem(item1);

            // Change the item to be m=m2
            Lookup.MetadataModifications newMetadata = new Lookup.MetadataModifications(keepOnlySpecified: false);
            newMetadata.Add("m", "m2");
            ICollection<ProjectItemInstance> group = new List<ProjectItemInstance>();
            group.Add(item1);
            lookup.ModifyItems(item1.ItemType, group, newMetadata);

            // Now it has m=m2
            group = lookup.GetItems("i1");
            Assert.Single(group);
            Assert.Equal("m2", group.First().GetMetadataValue("m"));

            // But the original item hasn't changed yet
            Assert.Equal("m1", item1.GetMetadataValue("m"));

            enteredScope.LeaveScope();

            // It still has m=m2
            group = lookup.GetItems("i1");
            Assert.Single(group);
            Assert.Equal("m2", group.First().GetMetadataValue("m"));

            // But now the original item has changed as well
            Assert.Equal("m2", item1.GetMetadataValue("m"));
        }

        /// <summary>
        /// Modifying an item in the outside scope is prohibited-
        /// purely because we don't need to do it in our code
        /// </summary>
        [Fact]
        public void ModifyItemInOutsideScope()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
                Lookup lookup = LookupHelpers.CreateLookup(new ItemDictionary<ProjectItemInstance>());
                lookup.AddNewItem(new ProjectItemInstance(project, "x", "y", project.FullPath));
            }
           );
        }
        /// <summary>
        /// After modification, should be able to GetItem and then modify it again
        /// </summary>
        [Fact]
        public void ModifyItemPreviouslyModifiedAndGottenThroughGetItem()
        {
            ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
            ItemDictionary<ProjectItemInstance> table1 = new ItemDictionary<ProjectItemInstance>();
            Lookup lookup = LookupHelpers.CreateLookup(table1);

            // Add an item with m=m1 and n=n1 
            ProjectItemInstance item1 = new ProjectItemInstance(project, "i1", "a2", project.FullPath);
            item1.SetMetadata("m", "m1");
            lookup.PopulateWithItem(item1);

            Lookup.Scope enteredScope = lookup.EnterScope("x");

            // Make a modification to the item to be m=m2
            Lookup.MetadataModifications newMetadata = new Lookup.MetadataModifications(keepOnlySpecified: false);
            newMetadata.Add("m", "m2");
            ICollection<ProjectItemInstance> group = new List<ProjectItemInstance>();
            group.Add(item1);
            lookup.ModifyItems(item1.ItemType, group, newMetadata);

            // Get the item (under the covers, it cloned it in order to apply the modification)
            ICollection<ProjectItemInstance> group2 = lookup.GetItems(item1.ItemType);
            Assert.Single(group2);
            ProjectItemInstance item1b = group2.First();

            // Modify to m=m3
            Lookup.MetadataModifications newMetadata2 = new Lookup.MetadataModifications(keepOnlySpecified: false);
            newMetadata2.Add("m", "m3");
            ICollection<ProjectItemInstance> group3 = new List<ProjectItemInstance>();
            group3.Add(item1b);
            lookup.ModifyItems(item1b.ItemType, group3, newMetadata2);

            // Modifications are visible
            ICollection<ProjectItemInstance> group4 = lookup.GetItems(item1b.ItemType);
            Assert.Single(group4);
            Assert.Equal("m3", group4.First().GetMetadataValue("m"));

            // Leave scope
            enteredScope.LeaveScope();

            // Still visible
            ICollection<ProjectItemInstance> group5 = lookup.GetItems(item1b.ItemType);
            Assert.Single(group5);
            Assert.Equal("m3", group5.First().GetMetadataValue("m"));
        }


        /// <summary>
        /// After modification, should be able to GetItem and then modify it again
        /// </summary>
        [Fact]
        public void ModifyItemInProjectPreviouslyModifiedAndGottenThroughGetItem()
        {
            ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
            // Create some project state with an item with m=m1 and n=n1 
            ItemDictionary<ProjectItemInstance> table1 = new ItemDictionary<ProjectItemInstance>();
            ProjectItemInstance item1 = new ProjectItemInstance(project, "i1", "a2", project.FullPath);
            item1.SetMetadata("m", "m1");
            table1.Add(item1);

            Lookup lookup = LookupHelpers.CreateLookup(table1);

            Lookup.Scope enteredScope = lookup.EnterScope("x");

            // Make a modification to the item to be m=m2
            Lookup.MetadataModifications newMetadata = new Lookup.MetadataModifications(keepOnlySpecified: false);
            newMetadata.Add("m", "m2");
            List<ProjectItemInstance> group = new List<ProjectItemInstance>();
            group.Add(item1);
            lookup.ModifyItems(item1.ItemType, group, newMetadata);

            // Get the item (under the covers, it cloned it in order to apply the modification)
            ICollection<ProjectItemInstance> group2 = lookup.GetItems(item1.ItemType);
            Assert.Single(group2);
            ProjectItemInstance item1b = group2.First();

            // Modify to m=m3
            Lookup.MetadataModifications newMetadata2 = new Lookup.MetadataModifications(keepOnlySpecified: false);
            newMetadata2.Add("m", "m3");
            List<ProjectItemInstance> group3 = new List<ProjectItemInstance>();
            group3.Add(item1b);
            lookup.ModifyItems(item1b.ItemType, group3, newMetadata2);

            // Modifications are visible
            ICollection<ProjectItemInstance> group4 = lookup.GetItems(item1b.ItemType);
            Assert.Single(group4);
            Assert.Equal("m3", group4.First().GetMetadataValue("m"));

            // Leave scope
            enteredScope.LeaveScope();

            // Still visible
            ICollection<ProjectItemInstance> group5 = lookup.GetItems(item1b.ItemType);
            Assert.Single(group5);
            Assert.Equal("m3", group5.First().GetMetadataValue("m"));

            // And the one in the project is changed
            Assert.Equal("m3", item1.GetMetadataValue("m"));
        }

        /// <summary>
        /// After modification, should be able to GetItem and then remove it
        /// </summary>
        [Fact]
        public void RemoveItemPreviouslyModifiedAndGottenThroughGetItem()
        {
            ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
            ItemDictionary<ProjectItemInstance> table1 = new ItemDictionary<ProjectItemInstance>();
            Lookup lookup = LookupHelpers.CreateLookup(table1);

            // Add an item with m=m1 and n=n1 
            ProjectItemInstance item1 = new ProjectItemInstance(project, "i1", "a2", project.FullPath);
            item1.SetMetadata("m", "m1");
            lookup.PopulateWithItem(item1);

            lookup.EnterScope("x");

            // Make a modification to the item to be m=m2
            Lookup.MetadataModifications newMetadata = new Lookup.MetadataModifications(keepOnlySpecified: false);
            newMetadata.Add("m", "m2");
            List<ProjectItemInstance> group = new List<ProjectItemInstance>();
            group.Add(item1);
            lookup.ModifyItems(item1.ItemType, group, newMetadata);

            // Get the item (under the covers, it cloned it in order to apply the modification)
            ICollection<ProjectItemInstance> group2 = lookup.GetItems(item1.ItemType);
            Assert.Single(group2);
            ProjectItemInstance item1b = group2.First();

            // Remove the item
            lookup.RemoveItem(item1b);

            // There's now no items at all
            ICollection<ProjectItemInstance> group3 = lookup.GetItems(item1.ItemType);
            Assert.Empty(group3);
        }

        /// <summary>
        /// After modification, should be able to GetItem and then remove it
        /// </summary>
        [Fact]
        public void RemoveItemFromProjectPreviouslyModifiedAndGottenThroughGetItem()
        {
            ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
            // Create some project state with an item with m=m1 and n=n1 
            ItemDictionary<ProjectItemInstance> table1 = new ItemDictionary<ProjectItemInstance>();
            ProjectItemInstance item1 = new ProjectItemInstance(project, "i1", "a2", project.FullPath);
            item1.SetMetadata("m", "m1");
            table1.Add(item1);

            Lookup lookup = LookupHelpers.CreateLookup(table1);

            Lookup.Scope enteredScope = lookup.EnterScope("x");

            // Make a modification to the item to be m=m2
            Lookup.MetadataModifications newMetadata = new Lookup.MetadataModifications(keepOnlySpecified: false);
            newMetadata.Add("m", "m2");
            List<ProjectItemInstance> group = new List<ProjectItemInstance>();
            group.Add(item1);
            lookup.ModifyItems(item1.ItemType, group, newMetadata);

            // Get the item (under the covers, it cloned it in order to apply the modification)
            ICollection<ProjectItemInstance> group2 = lookup.GetItems(item1.ItemType);
            Assert.Single(group2);
            ProjectItemInstance item1b = group2.First();

            // Remove the item
            lookup.RemoveItem(item1b);

            // There's now no items at all
            ICollection<ProjectItemInstance> group3 = lookup.GetItems(item1.ItemType);
            Assert.Empty(group3);

            // Leave scope
            enteredScope.LeaveScope();

            // And now none left in the project either
            Assert.Empty(table1["i1"]);
        }

        /// <summary>
        /// If the property isn't modified, the initial property
        /// should be returned
        /// </summary>
        [Fact]
        public void UnmodifiedProperty()
        {
            PropertyDictionary<ProjectPropertyInstance> group = new PropertyDictionary<ProjectPropertyInstance>();
            ProjectPropertyInstance property = ProjectPropertyInstance.Create("p1", "v1");
            group.Set(property);
            Lookup lookup = LookupHelpers.CreateLookup(group);

            Assert.Equal(property, lookup.GetProperty("p1"));

            lookup.EnterScope("x");

            Assert.Equal(property, lookup.GetProperty("p1"));
        }

        /// <summary>
        /// If the property isn't found, should return null
        /// </summary>
        [Fact]
        public void NonexistentProperty()
        {
            PropertyDictionary<ProjectPropertyInstance> group = new PropertyDictionary<ProjectPropertyInstance>();
            Lookup lookup = LookupHelpers.CreateLookup(group);

            Assert.Null(lookup.GetProperty("p1"));

            lookup.EnterScope("x");

            Assert.Null(lookup.GetProperty("p1"));
        }

        /// <summary>
        /// If the property is modified, the updated value should be returned,
        /// both before and after leaving scope.
        /// </summary>
        [Fact]
        public void ModifiedProperty()
        {
            PropertyDictionary<ProjectPropertyInstance> group = new PropertyDictionary<ProjectPropertyInstance>();
            group.Set(ProjectPropertyInstance.Create("p1", "v1"));
            Lookup lookup = LookupHelpers.CreateLookup(group);
            // Enter scope so that property sets are allowed on it
            Lookup.Scope enteredScope = lookup.EnterScope("x");

            // Change the property value
            lookup.SetProperty(ProjectPropertyInstance.Create("p1", "v2"));

            // Lookup is updated, but not original item group
            Assert.Equal("v2", lookup.GetProperty("p1").EvaluatedValue);
            Assert.Equal("v1", group["p1"].EvaluatedValue);

            Lookup.Scope enteredScope2 = lookup.EnterScope("x");

            // Change the value again in the new scope 
            lookup.SetProperty(ProjectPropertyInstance.Create("p1", "v3"));

            // Lookup is updated, but not the original item group
            Assert.Equal("v3", lookup.GetProperty("p1").EvaluatedValue);
            Assert.Equal("v1", group["p1"].EvaluatedValue);

            Lookup.Scope enteredScope3 = lookup.EnterScope("x");

            // Change the value again in the new scope 
            lookup.SetProperty(ProjectPropertyInstance.Create("p1", "v4"));

            Assert.Equal("v4", lookup.GetProperty("p1").EvaluatedValue);

            enteredScope3.LeaveScope();

            Assert.Equal("v4", lookup.GetProperty("p1").EvaluatedValue);

            // Leave to the outer scope
            enteredScope2.LeaveScope();
            enteredScope.LeaveScope();

            // Now the lookup and original group are updated
            Assert.Equal("v4", lookup.GetProperty("p1").EvaluatedValue);
            Assert.Equal("v4", group["p1"].EvaluatedValue);
        }
    }

    internal class LookupHelpers
    {
        internal static Lookup CreateEmptyLookup()
        {
            Lookup lookup = new Lookup(new ItemDictionary<ProjectItemInstance>(), new PropertyDictionary<ProjectPropertyInstance>());
            return lookup;
        }

        internal static Lookup CreateLookup(ItemDictionary<ProjectItemInstance> items)
        {
            Lookup lookup = new Lookup(items, new PropertyDictionary<ProjectPropertyInstance>());
            return lookup;
        }

        internal static Lookup CreateLookup(PropertyDictionary<ProjectPropertyInstance> properties)
        {
            Lookup lookup = new Lookup(new ItemDictionary<ProjectItemInstance>(), properties);
            return lookup;
        }

        internal static Lookup CreateLookup(PropertyDictionary<ProjectPropertyInstance> properties, ItemDictionary<ProjectItemInstance> items)
        {
            Lookup lookup = new Lookup(items, properties);
            return lookup;
        }
    }
}
