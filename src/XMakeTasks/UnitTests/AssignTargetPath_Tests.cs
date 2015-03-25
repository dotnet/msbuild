// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;

using NUnit.Framework;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    sealed public class AssignTargetPath_Tests
    {
        [Test]
        public void Regress314791()
        {
            AssignTargetPath t = new AssignTargetPath();
            t.BuildEngine = new MockEngine();
            t.Files = new ITaskItem[]
                          { new TaskItem(NativeMethodsShared.IsWindows ? @"c:\bin2\abc.efg" : "/bin2/abc.efg") };
            t.RootFolder = NativeMethodsShared.IsWindows ? @"c:\bin" : "/bin";

            bool success = t.Execute();

            Assert.IsTrue(success);

            Assert.AreEqual(1, t.AssignedFiles.Length);
            Assert.AreEqual(
                NativeMethodsShared.IsWindows ? @"c:\bin2\abc.efg" : "/bin2/abc.efg",
                t.AssignedFiles[0].ItemSpec);
            Assert.AreEqual(@"abc.efg", t.AssignedFiles[0].GetMetadata("TargetPath"));
        }

        [Test]
        public void AtConeRoot()
        {
            AssignTargetPath t = new AssignTargetPath();
            t.BuildEngine = new MockEngine();
            t.Files = new ITaskItem[]
                          { new TaskItem(NativeMethodsShared.IsWindows ? @"c:\f1\f2\file.txt" : "/f1/f2/file.txt") };
            t.RootFolder = NativeMethodsShared.IsWindows ? @"c:\f1\f2" : "/f1/f2";

            bool success = t.Execute();

            Assert.IsTrue(success);

            Assert.AreEqual(1, t.AssignedFiles.Length);
            Assert.AreEqual(@"file.txt", t.AssignedFiles[0].GetMetadata("TargetPath"));
        }

        [Test]
        public void OutOfCone()
        {
            AssignTargetPath t = new AssignTargetPath();
            t.BuildEngine = new MockEngine();
            t.Files = new ITaskItem[]
                          {
                              new TaskItem(
                                  NativeMethodsShared.IsWindows ? @"d:\f1\f2\f3\f4\file.txt" : "/f1/f2/f3/f4/file.txt")
                          };
            t.RootFolder = NativeMethodsShared.IsWindows ? @"c:\f1" : "/f1";

            bool success = t.Execute();

            Assert.IsTrue(success);

            Assert.AreEqual(1, t.AssignedFiles.Length);
            Assert.AreEqual("file.txt", t.AssignedFiles[0].GetMetadata("TargetPath"));
        }

        [Test]
        public void InConeButAbsolute()
        {
            AssignTargetPath t = new AssignTargetPath();
            t.BuildEngine = new MockEngine();
            t.Files = new ITaskItem[]
                          {
                              new TaskItem(
                                  NativeMethodsShared.IsWindows ? @"c:\f1\f2\f3\f4\file.txt" : "/f1/f2/f3/f4/file.txt")
                          };
            t.RootFolder = NativeMethodsShared.IsWindows ? @"c:\f1\f2" : "/f1/f2";

            bool success = t.Execute();

            Assert.IsTrue(success);

            Assert.AreEqual(1, t.AssignedFiles.Length);
            Assert.AreEqual(
                NativeMethodsShared.IsWindows ? @"f3\f4\file.txt" : "f3/f4/file.txt",
                t.AssignedFiles[0].GetMetadata("TargetPath"));
        }
    }
}



