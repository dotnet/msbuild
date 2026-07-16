// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public sealed class AssignLinkMetadata_Tests
    {
        private readonly string _defaultItemSpec = Path.Combine(Path.GetTempPath(), "SubFolder", "a.cs");

        /// <summary>
        /// AssignLinkMetadata should behave nicely when no items are set to it
        /// </summary>
        [MSBuildTestMethod]
        public void NoItems()
        {
            AssignLinkMetadata t = new AssignLinkMetadata
            {
                BuildEngine = new MockEngine()
            };
            bool success = t.Execute();

            Assert.IsTrue(success);
            Assert.IsEmpty(t.OutputItems);
        }

        /// <summary>
        /// AssignLinkMetadata should behave nicely when there is an item with an
        /// itemspec that contains invalid path characters.
        /// </summary>
        [MSBuildTestMethod]
        public void InvalidItemPath()
        {
            ITaskItem item = GetParentedTaskItem(_defaultItemSpec);
            item.ItemSpec = "|||";

            AssignLinkMetadata t = new AssignLinkMetadata
            {
                BuildEngine = new MockEngine(),
                Items = new ITaskItem[] { new TaskItem(item) }
            };
            bool success = t.Execute();

            Assert.IsTrue(success);
            Assert.IsEmpty(t.OutputItems);
        }

        /// <summary>
        /// Test basic function of the AssignLinkMetadata task
        /// </summary>
        [MSBuildTestMethod]
        public void Basic()
        {
            ITaskItem item = GetParentedTaskItem(_defaultItemSpec);

            AssignLinkMetadata t = new AssignLinkMetadata
            {
                BuildEngine = new MockEngine(),
                Items = new ITaskItem[] { new TaskItem(item) }
            };
            bool success = t.Execute();

            Assert.IsTrue(success);
            Assert.ContainsSingle(t.OutputItems);
            Assert.AreEqual(item.ItemSpec, t.OutputItems[0].ItemSpec);

            // Link metadata should have been added by the task, and OriginalItemSpec was added by the copy
            Assert.AreEqual(item.MetadataCount + 2, t.OutputItems[0].MetadataCount);
            Assert.AreEqual(Path.Combine("SubFolder", "a.cs"), t.OutputItems[0].GetMetadata("Link"));
        }

        /// <summary>
        /// AssignLinkMetadata should behave nicely when there is an item with an
        /// itemspec that contains invalid path characters, and still successfully
        /// output any items that aren't problematic.
        /// </summary>
        [MSBuildTestMethod]
        public void InvalidItemPathWithOtherValidItem()
        {
            ITaskItem item1 = GetParentedTaskItem(itemSpec: "|||");
            ITaskItem item2 = GetParentedTaskItem(_defaultItemSpec);

            AssignLinkMetadata t = new AssignLinkMetadata
            {
                BuildEngine = new MockEngine(),
                Items = new ITaskItem[] { new TaskItem(item1), new TaskItem(item2) }
            };
            bool success = t.Execute();

            Assert.IsTrue(success);
            Assert.ContainsSingle(t.OutputItems);
            Assert.AreEqual(item2.ItemSpec, t.OutputItems[0].ItemSpec);

            // Link metadata should have been added by the task, and OriginalItemSpec was added by the copy
            Assert.AreEqual(item2.MetadataCount + 2, t.OutputItems[0].MetadataCount);
            Assert.AreEqual(Path.Combine("SubFolder", "a.cs"), t.OutputItems[0].GetMetadata("Link"));
        }

        /// <summary>
        /// AssignLinkMetadata should not override if Link is already set
        /// </summary>
        [MSBuildTestMethod]
        public void DontOverrideLink()
        {
            ITaskItem item = GetParentedTaskItem(_defaultItemSpec, Path.Combine("SubFolder2", "SubSubFolder", "a.cs"));

            AssignLinkMetadata t = new AssignLinkMetadata
            {
                BuildEngine = new MockEngine(),
                Items = new ITaskItem[] { new TaskItem(item) }
            };
            bool success = t.Execute();

            Assert.IsTrue(success);
            Assert.IsEmpty(t.OutputItems);
        }

        /// <summary>
        /// AssignLinkMetadata should not set Link if the item is outside the
        /// defining project's cone
        /// </summary>
        [MSBuildTestMethod]
        public void OutsideDefiningProjectCone()
        {
            var item = GetParentedTaskItem(NativeMethodsShared.IsUnixLike
                ? Path.Combine("//subfolder/a.cs")
                : @"c:\subfolder\a.cs");

            AssignLinkMetadata t = new AssignLinkMetadata
            {
                BuildEngine = new MockEngine(),
                Items = new ITaskItem[] { new TaskItem(item) }
            };
            bool success = t.Execute();

            Assert.IsTrue(success);
            Assert.IsEmpty(t.OutputItems);
        }

        /// <summary>
        /// AssignLinkMetadata should not set Link if the item does not know its
        /// defining project
        /// </summary>
        [MSBuildTestMethod]
        public void NoDefiningProjectMetadata()
        {
            ITaskItem item = new TaskItem(Path.Combine("SubFolder", "a.cs"));

            AssignLinkMetadata t = new AssignLinkMetadata
            {
                BuildEngine = new MockEngine(),
                Items = new ITaskItem[] { item }
            };
            bool success = t.Execute();

            Assert.IsTrue(success);
            Assert.IsEmpty(t.OutputItems);
        }

        /// <summary>
        /// Helper function creating a task item that is associated with a parent project
        /// </summary>
        private ITaskItem GetParentedTaskItem(string itemSpec, string linkMetadata = null)
        {
            using var collection = new ProjectCollection();
            Project p = new Project(collection)
            {
                FullPath = Path.Combine(Path.GetTempPath(), "a.proj")
            };
            ProjectInstance pi = p.CreateProjectInstance();

            IDictionary<string, string> metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (linkMetadata != null)
            {
                metadata.Add("Link", linkMetadata);
            }

            ITaskItem item = pi.AddItem("Foo", itemSpec, metadata);
            return item;
        }
    }
}
