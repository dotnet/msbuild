// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    sealed public class FindUnderPath_Tests
    {
        [TestMethod]
        public void BasicFilter()
        {
            FindUnderPath t = new FindUnderPath();
            t.BuildEngine = new MockEngine();

            t.Path = new TaskItem(@"C:\MyProject");
            t.Files = new ITaskItem[] { new TaskItem(@"C:\MyProject\File1.txt"), new TaskItem(@"C:\SomeoneElsesProject\File2.txt") };

            bool success = t.Execute();

            Assert.IsTrue(success);
            Assert.AreEqual(1, t.InPath.Length);
            Assert.AreEqual(1, t.OutOfPath.Length);
            Assert.AreEqual(@"C:\MyProject\File1.txt", t.InPath[0].ItemSpec);
            Assert.AreEqual(@"C:\SomeoneElsesProject\File2.txt", t.OutOfPath[0].ItemSpec);
        }

        [TestMethod]
        public void InvalidFile()
        {
            FindUnderPath t = new FindUnderPath();
            t.BuildEngine = new MockEngine();

            t.Path = new TaskItem(@"C:\MyProject");
            t.Files = new ITaskItem[] { new TaskItem(@":::") };

            bool success = t.Execute();

            Assert.IsTrue(!success);

            // Don't crash
        }

        [TestMethod]
        public void InvalidPath()
        {
            FindUnderPath t = new FindUnderPath();
            t.BuildEngine = new MockEngine();

            t.Path = new TaskItem(@"||::||");
            t.Files = new ITaskItem[] { new TaskItem(@"foo") };

            bool success = t.Execute();

            Assert.IsTrue(!success);

            // Don't crash
        }

        // Create a temporary file and run the task on it
        private static void RunTask(FindUnderPath t, out FileInfo testFile, out bool success)
        {
            string fileName = ObjectModelHelpers.CreateFileInTempProjectDirectory("file%3b.temp", "foo");
            testFile = new FileInfo(fileName);

            t.Path = new TaskItem(ObjectModelHelpers.TempProjectDir);
            t.Files = new ITaskItem[] { new TaskItem(EscapingUtilities.Escape(testFile.Name)), new TaskItem(@"C:\SomeoneElsesProject\File2.txt") };

            success = false;
            string currentDirectory = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(ObjectModelHelpers.TempProjectDir);
                success = t.Execute();
            }
            finally
            {
                Directory.SetCurrentDirectory(currentDirectory);
            }
        }

        [TestMethod]
        public void VerifyFullPath()
        {
            FindUnderPath t = new FindUnderPath();
            t.BuildEngine = new MockEngine();

            t.UpdateToAbsolutePaths = true;

            FileInfo testFile;
            bool success;
            RunTask(t, out testFile, out success);

            Assert.IsTrue(success);
            Assert.AreEqual(1, t.InPath.Length);
            Assert.AreEqual(1, t.OutOfPath.Length);
            Assert.AreEqual(testFile.FullName, t.InPath[0].ItemSpec);
            Assert.AreEqual(@"C:\SomeoneElsesProject\File2.txt", t.OutOfPath[0].ItemSpec);
        }

        [TestMethod]
        public void VerifyFullPathNegative()
        {
            FindUnderPath t = new FindUnderPath();
            t.BuildEngine = new MockEngine();

            t.UpdateToAbsolutePaths = false;

            FileInfo testFile;
            bool success;
            RunTask(t, out testFile, out success);

            Assert.IsTrue(success);
            Assert.AreEqual(1, t.InPath.Length);
            Assert.AreEqual(1, t.OutOfPath.Length);
            Assert.AreEqual(testFile.Name, t.InPath[0].ItemSpec);
            Assert.AreEqual(@"C:\SomeoneElsesProject\File2.txt", t.OutOfPath[0].ItemSpec);
        }
    }
}



