// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Microsoft.Build.Evaluation;
using System.Collections.Generic;
using Microsoft.Build.Execution;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    sealed public class AssignLinkMetadata_Tests
    {
        /// <summary>
        /// AssignLinkMetadata should behave nicely when no items are set to it
        /// </summary>
        [Fact]
        public void NoItems()
        {
            AssignLinkMetadata t = new AssignLinkMetadata();
            t.BuildEngine = new MockEngine();
            bool success = t.Execute();

            Assert.True(success);
            Assert.Equal(0, t.OutputItems.Length);
        }

        /// <summary>
        /// AssignLinkMetadata should behave nicely when there is an item with an 
        /// itemspec that contains invalid path characters.
        /// </summary>
        [Fact]
        public void InvalidItemPath()
        {
            ITaskItem item = GetParentedTaskItem();
            item.ItemSpec = "|||";

            AssignLinkMetadata t = new AssignLinkMetadata();
            t.BuildEngine = new MockEngine();
            t.Items = new ITaskItem[] { new TaskItem(item) };
            bool success = t.Execute();

            Assert.True(success);
            Assert.Equal(0, t.OutputItems.Length);
        }

        /// <summary>
        /// Test basic function of the AssignLinkMetadata task
        /// </summary>
        [Fact]
        public void Basic()
        {
            ITaskItem item = GetParentedTaskItem();

            AssignLinkMetadata t = new AssignLinkMetadata();
            t.BuildEngine = new MockEngine();
            t.Items = new ITaskItem[] { new TaskItem(item) };
            bool success = t.Execute();

            Assert.True(success);
            Assert.Equal(1, t.OutputItems.Length);
            Assert.Equal(item.ItemSpec, t.OutputItems[0].ItemSpec);

            // Link metadata should have been added by the task, and OriginalItemSpec was added by the copy 
            Assert.Equal(item.MetadataCount + 2, t.OutputItems[0].MetadataCount);
            Assert.Equal(@"SubFolder\a.cs", t.OutputItems[0].GetMetadata("Link"));
        }

        /// <summary>
        /// AssignLinkMetadata should behave nicely when there is an item with an 
        /// itemspec that contains invalid path characters, and still successfully 
        /// output any items that aren't problematic.
        /// </summary>
        [Fact]
        public void InvalidItemPathWithOtherValidItem()
        {
            ITaskItem item1 = GetParentedTaskItem(itemSpec: "|||");
            ITaskItem item2 = GetParentedTaskItem();

            AssignLinkMetadata t = new AssignLinkMetadata();
            t.BuildEngine = new MockEngine();
            t.Items = new ITaskItem[] { new TaskItem(item1), new TaskItem(item2) };
            bool success = t.Execute();

            Assert.True(success);
            Assert.Equal(1, t.OutputItems.Length);
            Assert.Equal(item2.ItemSpec, t.OutputItems[0].ItemSpec);

            // Link metadata should have been added by the task, and OriginalItemSpec was added by the copy 
            Assert.Equal(item2.MetadataCount + 2, t.OutputItems[0].MetadataCount);
            Assert.Equal(@"SubFolder\a.cs", t.OutputItems[0].GetMetadata("Link"));
        }

        /// <summary>
        /// AssignLinkMetadata should not override if Link is already set
        /// </summary>
        [Fact]
        public void DontOverrideLink()
        {
            ITaskItem item = GetParentedTaskItem(linkMetadata: @"SubFolder2\SubSubFolder\a.cs");

            AssignLinkMetadata t = new AssignLinkMetadata();
            t.BuildEngine = new MockEngine();
            t.Items = new ITaskItem[] { new TaskItem(item) };
            bool success = t.Execute();

            Assert.True(success);
            Assert.Equal(0, t.OutputItems.Length);
        }

        /// <summary>
        /// AssignLinkMetadata should not set Link if the item is outside the 
        /// defining project's cone
        /// </summary>
        [Fact]
        public void OutsideDefiningProjectCone()
        {
            ITaskItem item = GetParentedTaskItem(itemSpec: @"c:\subfolder\a.cs");

            AssignLinkMetadata t = new AssignLinkMetadata();
            t.BuildEngine = new MockEngine();
            t.Items = new ITaskItem[] { new TaskItem(item) };
            bool success = t.Execute();

            Assert.True(success);
            Assert.Equal(0, t.OutputItems.Length);
        }

        /// <summary>
        /// AssignLinkMetadata should not set Link if the item does not know its
        /// defining project
        /// </summary>
        [Fact]
        public void NoDefiningProjectMetadata()
        {
            ITaskItem item = new TaskItem(@"SubFolder\a.cs");

            AssignLinkMetadata t = new AssignLinkMetadata();
            t.BuildEngine = new MockEngine();
            t.Items = new ITaskItem[] { item };
            bool success = t.Execute();

            Assert.True(success);
            Assert.Equal(0, t.OutputItems.Length);
        }

        /// <summary>
        /// Helper function creating a task item that is associated with a parent project
        /// </summary>
        private ITaskItem GetParentedTaskItem(string linkMetadata = null)
        {
            return GetParentedTaskItem(Path.Combine(Path.GetTempPath(), "SubFolder", "a.cs"), linkMetadata);
        }

        /// <summary>
        /// Helper function creating a task item that is associated with a parent project
        /// </summary>
        private ITaskItem GetParentedTaskItem(string itemSpec, string linkMetadata = null)
        {
            Project p = new Project(new ProjectCollection());
            p.FullPath = Path.Combine(Path.GetTempPath(), "a.proj");
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



