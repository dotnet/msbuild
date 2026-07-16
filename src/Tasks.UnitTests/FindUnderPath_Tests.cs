// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.UnitTests;
using Microsoft.Build.Utilities;



#nullable disable

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public sealed class FindUnderPath_Tests
    {
        [MSBuildTestMethod]
        public void BasicFilter()
        {
            FindUnderPath t = new FindUnderPath();
            t.TaskEnvironment = TaskEnvironmentHelper.CreateForTest();
            t.BuildEngine = new MockEngine();

            t.Path = new TaskItem(@"C:\MyProject");
            t.Files = new ITaskItem[] { new TaskItem(@"C:\MyProject\File1.txt"), new TaskItem(@"C:\SomeoneElsesProject\File2.txt") };

            bool success = t.Execute();

            Assert.IsTrue(success);
            Assert.ContainsSingle(t.InPath);
            Assert.ContainsSingle(t.OutOfPath);
            Assert.AreEqual(FileUtilities.FixFilePath(@"C:\MyProject\File1.txt"), t.InPath[0].ItemSpec);
            Assert.AreEqual(FileUtilities.FixFilePath(@"C:\SomeoneElsesProject\File2.txt"), t.OutOfPath[0].ItemSpec);
        }

        /// <summary>
        /// Tests that invalid file path characters cause the task to fail.
        /// This only applies when Wave18_5 is disabled, as the new behavior doesn't throw on invalid path characters.
        /// </summary>
        [WindowsFullFrameworkOnlyFact(additionalMessage: ".NET Core 2.1+ no longer validates paths: https://github.com/dotnet/corefx/issues/27779#issuecomment-371253486. On Unix there is no invalid file name characters.")]
        public void InvalidFile()
        {
            using TestEnvironment env = TestEnvironment.Create();

            // TODO: Remove test when Wave18_5 rotates out - new behavior doesn't throw on invalid path characters
            ChangeWaves.ResetStateForTests();
            env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", ChangeWaves.Wave18_5.ToString());

            FindUnderPath t = new FindUnderPath();
            t.TaskEnvironment = TaskEnvironmentHelper.CreateForTest();
            t.BuildEngine = new MockEngine();

            t.Path = new TaskItem(@"C:\MyProject");
            t.Files = new ITaskItem[] { new TaskItem(@":::") };

            bool success = t.Execute();

            Assert.IsFalse(success);

            ChangeWaves.ResetStateForTests();

            // Don't crash
        }

        /// <summary>
        /// Tests that invalid path characters cause the task to fail.
        /// This only applies when Wave18_5 is disabled, as the new behavior doesn't throw on invalid path characters.
        /// </summary>
        [WindowsFullFrameworkOnlyFact(additionalMessage: ".NET Core 2.1+ no longer validates paths: https://github.com/dotnet/corefx/issues/27779#issuecomment-371253486. On Unix there is no invalid file name characters.")]
        public void InvalidPath()
        {
            using TestEnvironment env = TestEnvironment.Create();

            // TODO: Remove test when Wave18_5 rotates out - new behavior doesn't throw on invalid path characters
            ChangeWaves.ResetStateForTests();
            env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", ChangeWaves.Wave18_5.ToString());

            FindUnderPath t = new FindUnderPath();
            t.TaskEnvironment = TaskEnvironmentHelper.CreateForTest();
            t.BuildEngine = new MockEngine();

            t.Path = new TaskItem(@"||::||");
            t.Files = new ITaskItem[] { new TaskItem(@"foo") };

            bool success = t.Execute();

            Assert.IsFalse(success);

            ChangeWaves.ResetStateForTests();

            // Don't crash
        }

        // Create a temporary file and run the task on it
        private static void RunTask(FindUnderPath t, out FileInfo testFile, out bool success)
        {
            string fileName = ObjectModelHelpers.CreateFileInTempProjectDirectory("file%3b.temp", "foo");
            testFile = new FileInfo(fileName);

            t.Path = new TaskItem(ObjectModelHelpers.TempProjectDir);
            t.Files = new ITaskItem[] { new TaskItem(EscapingUtilities.Escape(testFile.Name)),
                new TaskItem(NativeMethodsShared.IsWindows ? @"C:\SomeoneElsesProject\File2.txt" : "/SomeoneElsesProject/File2.txt") };

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

        [MSBuildTestMethod]
        [TestCategory("netcore-osx-failing")]
        [TestCategory("netcore-linux-failing")]
        public void VerifyFullPath()
        {
            FindUnderPath t = new FindUnderPath();
            t.TaskEnvironment = TaskEnvironmentHelper.CreateForTest();
            t.BuildEngine = new MockEngine();

            t.UpdateToAbsolutePaths = true;

            FileInfo testFile;
            bool success;
            RunTask(t, out testFile, out success);

            Assert.IsTrue(success);
            Assert.ContainsSingle(t.InPath);
            Assert.ContainsSingle(t.OutOfPath);
            Assert.AreEqual(testFile.FullName, t.InPath[0].ItemSpec);
            Assert.AreEqual(NativeMethodsShared.IsWindows ? @"C:\SomeoneElsesProject\File2.txt" : "/SomeoneElsesProject/File2.txt",
                t.OutOfPath[0].ItemSpec);
        }

        [MSBuildTestMethod]
        [TestCategory("netcore-osx-failing")]
        [TestCategory("netcore-linux-failing")]
        public void VerifyFullPathNegative()
        {
            FindUnderPath t = new FindUnderPath();
            t.TaskEnvironment = TaskEnvironmentHelper.CreateForTest();
            t.BuildEngine = new MockEngine();

            t.UpdateToAbsolutePaths = false;

            FileInfo testFile;
            bool success;
            RunTask(t, out testFile, out success);

            Assert.IsTrue(success);
            Assert.ContainsSingle(t.InPath);
            Assert.ContainsSingle(t.OutOfPath);
            Assert.AreEqual(testFile.Name, t.InPath[0].ItemSpec);
            Assert.AreEqual(NativeMethodsShared.IsWindows ? @"C:\SomeoneElsesProject\File2.txt" : "/SomeoneElsesProject/File2.txt",
                t.OutOfPath[0].ItemSpec);
        }
    }
}
