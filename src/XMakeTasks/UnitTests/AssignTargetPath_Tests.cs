// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    sealed public class AssignTargetPath_Tests
    {
        [TestMethod]
        public void Regress314791()
        {
            AssignTargetPath t = new AssignTargetPath();
            t.BuildEngine = new MockEngine();
            t.Files = new ITaskItem[] { new TaskItem(@"c:\bin2\abc.efg") };
            t.RootFolder = @"c:\bin";

            bool success = t.Execute();

            Assert.IsTrue(success);

            Assert.AreEqual(1, t.AssignedFiles.Length);
            Assert.AreEqual(@"c:\bin2\abc.efg", t.AssignedFiles[0].ItemSpec);
            Assert.AreEqual(@"abc.efg", t.AssignedFiles[0].GetMetadata("TargetPath"));
        }

        [TestMethod]
        public void AtConeRoot()
        {
            AssignTargetPath t = new AssignTargetPath();
            t.BuildEngine = new MockEngine();
            t.Files = new ITaskItem[] { new TaskItem(@"c:\f1\f2\file.txt") };
            t.RootFolder = @"c:\f1\f2";

            bool success = t.Execute();

            Assert.IsTrue(success);

            Assert.AreEqual(1, t.AssignedFiles.Length);
            Assert.AreEqual(@"file.txt", t.AssignedFiles[0].GetMetadata("TargetPath"));
        }

        [TestMethod]
        public void OutOfCone()
        {
            AssignTargetPath t = new AssignTargetPath();
            t.BuildEngine = new MockEngine();
            t.Files = new ITaskItem[] { new TaskItem(@"d:\f1\f2\f3\f4\file.txt") };
            t.RootFolder = @"c:\f1";

            bool success = t.Execute();

            Assert.IsTrue(success);

            Assert.AreEqual(1, t.AssignedFiles.Length);
            Assert.AreEqual("file.txt", t.AssignedFiles[0].GetMetadata("TargetPath"));
        }

        [TestMethod]
        public void InConeButAbsolute()
        {
            AssignTargetPath t = new AssignTargetPath();
            t.BuildEngine = new MockEngine();
            t.Files = new ITaskItem[] { new TaskItem(@"c:\f1\f2\f3\f4\file.txt") };
            t.RootFolder = @"c:\f1\f2";

            bool success = t.Execute();

            Assert.IsTrue(success);

            Assert.AreEqual(1, t.AssignedFiles.Length);
            Assert.AreEqual(@"f3\f4\file.txt", t.AssignedFiles[0].GetMetadata("TargetPath"));
        }
    }
}



