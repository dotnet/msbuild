// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Reflection;
using NUnit.Framework;
using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;


namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class GroupingCollectionTest
    {
        private BuildPropertyGroup pg1;
        private BuildPropertyGroup pg2;
        private BuildPropertyGroup pg3;

        private Choose choose1;
        private Choose choose2;
        private Choose choose3;

        private BuildItemGroup ig1;
        private BuildItemGroup ig2;
        private BuildItemGroup ig3;

        private void SetupMembers()
        {
            pg1 = new BuildPropertyGroup();
            pg1.SetProperty("foo", "bar");
            pg1.SetProperty("abc", "true");
            pg1.SetProperty("Unit", "inches");

            pg2 = new BuildPropertyGroup();
            pg2.SetProperty("foo", "bar");
            pg2.SetProperty("abc", "true");

            pg3 = new BuildPropertyGroup();

            // These Choose objects are only suitable for
            // holding a place in the GroupingCollection.
            choose1 = new Choose();
            choose2 = new Choose();
            choose3 = new Choose();

            ig1 = new BuildItemGroup();
            ig1.AddNewItem("x", "x1");
            ig1.AddNewItem("x", "x2");
            ig1.AddNewItem("y", "y1");
            ig1.AddNewItem("y", "y2");
            ig1.AddNewItem("y", "y3");
            ig1.AddNewItem("y", "y4");

            ig2 = new BuildItemGroup();
            ig2.AddNewItem("jacksonfive", "germaine");
            ig2.AddNewItem("jacksonfive", "tito");
            ig2.AddNewItem("jacksonfive", "michael");
            ig2.AddNewItem("jacksonfive", "latoya");
            ig2.AddNewItem("jacksonfive", "janet");

            ig3 = new BuildItemGroup();
        }

        private void AssertNPropertyGroupsInCollection(GroupingCollection group, int n)
        {
            int count;
            count = 0;
            foreach (IItemPropertyGrouping pg in new GroupEnumeratorHelper(group, GroupEnumeratorHelper.ListType.PropertyGroupsAll))
            {
                count++;
            }
            Assertion.AssertEquals(n, count);
            // PropertyGroupCount uses a different mechanism to obtain the total count, so verify it as well
            Assertion.AssertEquals(n, group.PropertyGroupCount);
        }

        private void AssertNItemGroupsInCollection(GroupingCollection group, int n)
        {
            int count;
            count = 0;
            foreach (IItemPropertyGrouping pg in new GroupEnumeratorHelper(group, GroupEnumeratorHelper.ListType.ItemGroupsAll))
            {
                count++;
            }
            Assertion.AssertEquals(n, count);
            // ItemGroupCount uses a different mechanism to obtain the total count, so verify it as well
            Assertion.AssertEquals(n, group.ItemGroupCount);
        }

        private void AssertNPropertyGroupsAndChoosesInCollection(GroupingCollection group, int n)
        {
            int count;
            count = 0;
            foreach (IItemPropertyGrouping pg in new GroupEnumeratorHelper(group, GroupEnumeratorHelper.ListType.PropertyGroupsTopLevelAndChoose))
            {
                count++;
            }
            Assertion.AssertEquals(n, count);
        }

        private void AssertNItemGroupsAndChoosesInCollection(GroupingCollection group, int n)
        {
            int count;
            count = 0;
            foreach (IItemPropertyGrouping pg in new GroupEnumeratorHelper(group, GroupEnumeratorHelper.ListType.ItemGroupsTopLevelAndChoose))
            {
                count++;
            }
            Assertion.AssertEquals(n, count);
        }

        /// <summary>
        /// </summary>
        /// <owner>DavidLe</owner>
        [Test]
        public void EnumerationTest()
        {
            SetupMembers();
            GroupingCollection group = new GroupingCollection(null);
            group.InsertAtEnd(this.pg1);
            group.InsertAtEnd(this.ig1);
            group.InsertAtEnd(this.pg2);
            group.InsertAtEnd(this.ig2);
            group.InsertAtEnd(this.ig3);

            AssertNPropertyGroupsInCollection(group, 2);
            Assertion.Assert(group.PropertyGroupCount == 2);
            AssertNItemGroupsInCollection(group, 3);
            Assertion.Assert(group.ItemGroupCount == 3);

            group.InsertAtEnd(this.choose1);
            group.InsertAtEnd(this.choose2);

            AssertNPropertyGroupsInCollection(group, 2);
            Assertion.Assert(group.PropertyGroupCount == 2);
            AssertNItemGroupsInCollection(group, 3);
            Assertion.Assert(group.ItemGroupCount == 3);

            AssertNPropertyGroupsAndChoosesInCollection(group, 4);
            AssertNItemGroupsAndChoosesInCollection(group, 5);
        }

        /// <summary>
        /// </summary>
        /// <owner>DavidLe</owner>
        [Test]
        public void InsertionTest()
        {
            SetupMembers();
            GroupingCollection group = new GroupingCollection(null);
            group.InsertAtEnd(this.pg1);
            group.InsertAtEnd(this.ig1);
            group.InsertAtBeginning(this.pg2);
            group.InsertAtEnd(this.ig2);
            group.InsertAfter(this.ig3, this.ig2);
            group.InsertAfter(this.pg3, this.pg2);

            AssertNPropertyGroupsInCollection(group, 3);
            Assertion.Assert(group.PropertyGroupCount == 3);
            AssertNItemGroupsInCollection(group, 3);
            Assertion.Assert(group.ItemGroupCount == 3);

            group.InsertAtEnd(this.choose1);
            group.InsertAtEnd(this.choose2);
        }

        /// <summary>
        /// </summary>
        /// <owner>DavidLe</owner>
        [Test]
        public void RemoveTest()
        {
            SetupMembers();
            GroupingCollection group = new GroupingCollection(null);
            group.InsertAtEnd(this.pg1);
            group.InsertAtEnd(this.ig1);
            group.InsertAtBeginning(this.pg2);
            group.InsertAtEnd(this.ig2);
            group.InsertAfter(this.ig3, this.ig2);
            group.InsertAfter(this.pg3, this.pg2);

            AssertNPropertyGroupsInCollection(group, 3);
            AssertNItemGroupsInCollection(group, 3);

            group.RemovePropertyGroup(this.pg3);
            AssertNPropertyGroupsInCollection(group, 2);
            AssertNItemGroupsInCollection(group, 3);

            group.RemovePropertyGroup(this.pg2);
            AssertNPropertyGroupsInCollection(group, 1);
            AssertNItemGroupsInCollection(group, 3);

            group.RemoveItemGroup(this.ig2);
            AssertNPropertyGroupsInCollection(group, 1);
            AssertNItemGroupsInCollection(group, 2);
        }

        /// <summary>
        /// Make sure linked property group and item group counting works correctly. 
        /// Parent grouping collections depend on child grouping collections to update the count for nested groups.
        /// </summary>
        /// <owner>LukaszG</owner>
        [Test]
        public void LinkedCount()
        {
            SetupMembers();
            GroupingCollection masterGroup = new GroupingCollection(null);
            GroupingCollection childGroup1 = new GroupingCollection(masterGroup);
            GroupingCollection childGroup2 = new GroupingCollection(masterGroup);
            GroupingCollection nestedGroup = new GroupingCollection(childGroup1);

            nestedGroup.InsertAtEnd(this.ig1);
            nestedGroup.InsertAfter(this.ig2, this.ig1);
            nestedGroup.InsertAtBeginning(this.pg1);

            childGroup1.InsertAtEnd(this.ig1);
            childGroup1.InsertAtBeginning(this.pg1);

            childGroup2.InsertAtBeginning(this.pg1);

            masterGroup.InsertAtEnd(this.ig1);
            masterGroup.InsertAfter(this.ig2, this.ig1);
            masterGroup.InsertAtEnd(this.pg2);

            Assertion.AssertEquals(nestedGroup.ItemGroupCount, 2);
            Assertion.AssertEquals(nestedGroup.PropertyGroupCount, 1);

            Assertion.AssertEquals(childGroup1.ItemGroupCount, 1 + 2);
            Assertion.AssertEquals(childGroup1.PropertyGroupCount, 1 + 1);

            Assertion.AssertEquals(childGroup2.ItemGroupCount, 0);
            Assertion.AssertEquals(childGroup2.PropertyGroupCount, 1);

            Assertion.AssertEquals(masterGroup.ItemGroupCount, 2 + 0 + 1 + 2);
            Assertion.AssertEquals(masterGroup.PropertyGroupCount, 1 + 1 + 1 + 1);

            nestedGroup.Clear();
            nestedGroup.InsertAtEnd(this.ig2);
            nestedGroup.InsertAfter(this.ig3, this.ig2);
            
            childGroup1.RemovePropertyGroup(this.pg1);
            childGroup1.RemoveItemGroup(this.ig1);
            childGroup1.InsertAtEnd(this.ig3);

            childGroup2.RemovePropertyGroup(this.pg1);
            
            masterGroup.RemoveItemGroup(this.ig2);

            Assertion.AssertEquals(nestedGroup.ItemGroupCount, 2);
            Assertion.AssertEquals(nestedGroup.PropertyGroupCount, 0);

            Assertion.AssertEquals(childGroup1.ItemGroupCount, 1 + 2);
            Assertion.AssertEquals(childGroup1.PropertyGroupCount, 0 + 0);

            Assertion.AssertEquals(childGroup2.ItemGroupCount, 0);
            Assertion.AssertEquals(childGroup2.PropertyGroupCount, 0);

            Assertion.AssertEquals(masterGroup.ItemGroupCount, 1 + 0 + 1 + 2);
            Assertion.AssertEquals(masterGroup.PropertyGroupCount, 1 + 0 + 0 + 0);
        }
    }
}
