// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public class FindInList_Tests
    {
        [TestMethod]
        public void FoundCaseInsensitive()
        {
            FindInList f = new FindInList();
            f.BuildEngine = new MockEngine();
            f.ItemSpecToFind = "a.cs";
            f.List = new ITaskItem[] { new TaskItem("A.CS"), new TaskItem("b.cs") };
            Assert.IsTrue(f.Execute());
            Assert.AreEqual("A.CS", f.ItemFound.ItemSpec);
        }

        [TestMethod]
        public void FoundCaseSensitive()
        {
            FindInList f = new FindInList();
            f.BuildEngine = new MockEngine();
            f.ItemSpecToFind = "a.cs";
            f.CaseSensitive = true;
            f.List = new ITaskItem[] { new TaskItem("A.CS"), new TaskItem("a.cs") };
            Assert.IsTrue(f.Execute());
            Assert.AreEqual("a.cs", f.ItemFound.ItemSpec);
        }

        [TestMethod]
        public void NotFoundCaseSensitive()
        {
            FindInList f = new FindInList();
            f.BuildEngine = new MockEngine();
            f.ItemSpecToFind = "a.cs";
            f.CaseSensitive = true;
            f.List = new ITaskItem[] { new TaskItem("A.CS"), new TaskItem("b.cs") };
            Assert.IsTrue(f.Execute());
            Assert.AreEqual(null, f.ItemFound);
        }

        [TestMethod]
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
            Assert.IsTrue(f.Execute());
            Assert.AreEqual("a.cs", f.ItemFound.ItemSpec);
            Assert.AreEqual(item1.GetMetadata("id"), f.ItemFound.GetMetadata("id"));
        }

        /// <summary>
        /// Given two items (distinguished with metadata) verify that the last one is picked.
        /// </summary>
        [TestMethod]
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
            Assert.IsTrue(f.Execute(), "Expect success");
            Assert.AreEqual("a.cs", f.ItemFound.ItemSpec);
            Assert.AreEqual(item2.GetMetadata("id"), f.ItemFound.GetMetadata("id"));
        }

        [TestMethod]
        public void ReturnsLastOneEmptyList()
        {
            FindInList f = new FindInList();
            f.BuildEngine = new MockEngine();
            f.ItemSpecToFind = "a.cs";
            f.FindLastMatch = true;
            f.List = new ITaskItem[] { };
            Assert.IsTrue(f.Execute());
            Assert.AreEqual(null, f.ItemFound);
        }

        [TestMethod]
        public void NotFound()
        {
            FindInList f = new FindInList();
            f.BuildEngine = new MockEngine();
            f.ItemSpecToFind = "a.cs";
            f.List = new ITaskItem[] { new TaskItem("foo\a.cs"), new TaskItem("b.cs") };
            Assert.IsTrue(f.Execute());
            Assert.AreEqual(null, f.ItemFound);
        }

        [TestMethod]
        public void MatchFileNameOnly()
        {
            FindInList f = new FindInList();
            f.BuildEngine = new MockEngine();
            f.ItemSpecToFind = "a.cs";
            f.MatchFileNameOnly = true;
            f.List = new ITaskItem[] { new TaskItem(@"c:\foo\a.cs"), new TaskItem("b.cs") };
            Assert.IsTrue(f.Execute());
            Assert.AreEqual(@"c:\foo\a.cs", f.ItemFound.ItemSpec);
        }

        [TestMethod]
        public void MatchFileNameOnlyWithAnInvalidPath()
        {
            FindInList f = new FindInList();
            MockEngine e = new MockEngine();
            f.BuildEngine = e;
            f.ItemSpecToFind = "a.cs";
            f.MatchFileNameOnly = true;
            f.List = new ITaskItem[] { new TaskItem(@"!@#$@$%|"), new TaskItem(@"foo\a.cs"), new TaskItem("b.cs") };
            Assert.IsTrue(f.Execute());
            Console.WriteLine(e.Log);
            // Should ignore the invalid paths
            Assert.AreEqual(@"foo\a.cs", f.ItemFound.ItemSpec);
        }
    }
}
