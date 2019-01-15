// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Xunit;



namespace Microsoft.Build.UnitTests
{
    sealed public class FindUnderPath_Tests
    {
        [Fact]
        public void BasicFilter()
        {
            FindUnderPath t = new FindUnderPath();
            t.BuildEngine = new MockEngine();

            t.Path = new TaskItem(@"C:\MyProject");
            t.Files = new ITaskItem[] { new TaskItem(@"C:\MyProject\File1.txt"), new TaskItem(@"C:\SomeoneElsesProject\File2.txt") };

            bool success = t.Execute();

            Assert.True(success);
            Assert.Single(t.InPath);
            Assert.Single(t.OutOfPath);
            Assert.Equal(FileUtilities.FixFilePath(@"C:\MyProject\File1.txt"), t.InPath[0].ItemSpec);
            Assert.Equal(FileUtilities.FixFilePath(@"C:\SomeoneElsesProject\File2.txt"), t.OutOfPath[0].ItemSpec);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)] // On Unix there no invalid file name characters
        [SkipOnTargetFramework(TargetFrameworkMonikers.Netcoreapp, ".NET Core 2.1+ no longer validates paths: https://github.com/dotnet/corefx/issues/27779#issuecomment-371253486")]
        public void InvalidFile()
        {
            FindUnderPath t = new FindUnderPath();
            t.BuildEngine = new MockEngine();

            t.Path = new TaskItem(@"C:\MyProject");
            t.Files = new ITaskItem[] { new TaskItem(@":::") };

            bool success = t.Execute();

            Assert.False(success);

            // Don't crash
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)] // On Unix there no invalid file name characters
        [SkipOnTargetFramework(TargetFrameworkMonikers.Netcoreapp, ".NET Core 2.1+ no longer validates paths: https://github.com/dotnet/corefx/issues/27779#issuecomment-371253486")]
        public void InvalidPath()
        {
            FindUnderPath t = new FindUnderPath();
            t.BuildEngine = new MockEngine();

            t.Path = new TaskItem(@"||::||");
            t.Files = new ITaskItem[] { new TaskItem(@"foo") };

            bool success = t.Execute();

            Assert.False(success);

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

        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        [Trait("Category", "mono-osx-failing")]
        public void VerifyFullPath()
        {
            FindUnderPath t = new FindUnderPath();
            t.BuildEngine = new MockEngine();

            t.UpdateToAbsolutePaths = true;

            FileInfo testFile;
            bool success;
            RunTask(t, out testFile, out success);

            Assert.True(success);
            Assert.Single(t.InPath);
            Assert.Single(t.OutOfPath);
            Assert.Equal(testFile.FullName, t.InPath[0].ItemSpec);
            Assert.Equal(NativeMethodsShared.IsWindows ? @"C:\SomeoneElsesProject\File2.txt" : "/SomeoneElsesProject/File2.txt",
                t.OutOfPath[0].ItemSpec);
        }

        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        [Trait("Category", "mono-osx-failing")]
        public void VerifyFullPathNegative()
        {
            FindUnderPath t = new FindUnderPath();
            t.BuildEngine = new MockEngine();

            t.UpdateToAbsolutePaths = false;

            FileInfo testFile;
            bool success;
            RunTask(t, out testFile, out success);

            Assert.True(success);
            Assert.Single(t.InPath);
            Assert.Single(t.OutOfPath);
            Assert.Equal(testFile.Name, t.InPath[0].ItemSpec);
            Assert.Equal(NativeMethodsShared.IsWindows ? @"C:\SomeoneElsesProject\File2.txt" : "/SomeoneElsesProject/File2.txt",
                t.OutOfPath[0].ItemSpec);
        }
    }
}



