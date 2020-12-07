// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class FindInList_Tests
    {
        [Fact]
        public void FoundCaseInsensitive()
        {
            FindInList f = new FindInList();
            f.BuildEngine = new MockEngine();
            f.ItemSpecToFind = "a.cs";
            f.List = new ITaskItem[] { new TaskItem("A.CS"), new TaskItem("b.cs") };
            Assert.True(f.Execute());
            Assert.Equal("A.CS", f.ItemFound.ItemSpec);
        }

        [Fact]
        public void FoundCaseSensitive()
        {
            FindInList f = new FindInList();
            f.BuildEngine = new MockEngine();
            f.ItemSpecToFind = "a.cs";
            f.CaseSensitive = true;
            f.List = new ITaskItem[] { new TaskItem("A.CS"), new TaskItem("a.cs") };
            Assert.True(f.Execute());
            Assert.Equal("a.cs", f.ItemFound.ItemSpec);
        }

        [Fact]
        public void NotFoundCaseSensitive()
        {
            FindInList f = new FindInList();
            f.BuildEngine = new MockEngine();
            f.ItemSpecToFind = "a.cs";
            f.CaseSensitive = true;
            f.List = new ITaskItem[] { new TaskItem("A.CS"), new TaskItem("b.cs") };
            Assert.True(f.Execute());
            Assert.Null(f.ItemFound);
        }

        [Fact]
        public void ReturnsFirstOne()
        {
            FindInList f = new FindInList();
            f.BuildEngine = new MockEngine();
            f.ItemSpecToFind = "a.cs";
            ITaskItem item1 = new TaskItem("a.cs");
            item1.SetMetadata("id", "1");
            ITaskItem item2 = new TaskItem("a.cs");
            item2.SetMetadata("id", "2");
            f.List = new ITaskItem[] { item1, item2 };
            Assert.True(f.Execute());
            Assert.Equal("a.cs", f.ItemFound.ItemSpec);
            Assert.Equal(item1.GetMetadata("id"), f.ItemFound.GetMetadata("id"));
        }

        /// <summary>
        /// Given two items (distinguished with metadata) verify that the last one is picked.
        /// </summary>
        [Fact]
        public void ReturnsLastOne()
        {
            FindInList f = new FindInList();
            f.BuildEngine = new MockEngine();
            f.ItemSpecToFind = "a.cs";
            f.FindLastMatch = true;
            ITaskItem item1 = new TaskItem("a.cs");
            item1.SetMetadata("id", "1");
            ITaskItem item2 = new TaskItem("a.cs");
            item2.SetMetadata("id", "2");
            f.List = new ITaskItem[] { item1, item2 };
            Assert.True(f.Execute()); // "Expect success"
            Assert.Equal("a.cs", f.ItemFound.ItemSpec);
            Assert.Equal(item2.GetMetadata("id"), f.ItemFound.GetMetadata("id"));
        }

        [Fact]
        public void ReturnsLastOneEmptyList()
        {
            FindInList f = new FindInList();
            f.BuildEngine = new MockEngine();
            f.ItemSpecToFind = "a.cs";
            f.FindLastMatch = true;
            f.List = new ITaskItem[] { };
            Assert.True(f.Execute());
            Assert.Null(f.ItemFound);
        }

        [Fact]
        public void NotFound()
        {
            FindInList f = new FindInList();
            f.BuildEngine = new MockEngine();
            f.ItemSpecToFind = "a.cs";
            f.List = new ITaskItem[] { new TaskItem("foo\a.cs"), new TaskItem("b.cs") };
            Assert.True(f.Execute());
            Assert.Null(f.ItemFound);
        }

        [Fact]
        public void MatchFileNameOnly()
        {
            FindInList f = new FindInList();
            f.BuildEngine = new MockEngine();
            f.ItemSpecToFind = "a.cs";
            f.MatchFileNameOnly = true;
            f.List = new ITaskItem[] { new TaskItem(@"c:\foo\a.cs"), new TaskItem("b.cs") };
            Assert.True(f.Execute());
            Assert.Equal(FileUtilities.FixFilePath(@"c:\foo\a.cs"), f.ItemFound.ItemSpec);
        }

        [Fact]
        public void MatchFileNameOnlyWithAnInvalidPath()
        {
            FindInList f = new FindInList();
            MockEngine e = new MockEngine();
            f.BuildEngine = e;
            f.ItemSpecToFind = "a.cs";
            f.MatchFileNameOnly = true;
            f.List = new ITaskItem[] { new TaskItem(@"!@#$@$%|"), new TaskItem(@"foo\a.cs"), new TaskItem("b.cs") };
            Assert.True(f.Execute());
            Console.WriteLine(e.Log);
            // Should ignore the invalid paths
            Assert.Equal(FileUtilities.FixFilePath(@"foo\a.cs"), f.ItemFound.ItemSpec);
        }
    }
}
