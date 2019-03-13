// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using Microsoft.CodeAnalysis.BuildTasks;
using Xunit;
using BackEndNativeMethods = Microsoft.Build.BackEnd.NativeMethods;

// PLEASE NOTE: This is a UNICODE file as it contains UNICODE characters!
#if FEATURE_FILE_TRACKER

namespace Microsoft.Build.UnitTests.FileTracking
{
    public sealed class FileTrackerTests : IDisposable
    {
        private static string s_defaultFileTrackerPathUnquoted;
        private static string s_defaultFileTrackerPath;
        private static string s_defaultTrackerPath;

        private static string s_oldPath;

        private static string s_cmd32Path;
        private static string s_cmd64Path;

        public FileTrackerTests()
        {
            if (NativeMethodsShared.IsUnixLike)
            {
                return; // "FileTracker is not supported under Unix"
            }

            s_defaultFileTrackerPathUnquoted = FileTracker.GetFileTrackerPath(ExecutableType.SameAsCurrentProcess);
            s_defaultFileTrackerPath = "\"" + s_defaultFileTrackerPathUnquoted + "\"";
            s_defaultTrackerPath = FileTracker.GetTrackerPath(ExecutableType.SameAsCurrentProcess);

            s_cmd32Path = (IntPtr.Size == sizeof(int))
                ? Environment.ExpandEnvironmentVariables(@"%windir%\System32\cmd.exe")
                : Environment.ExpandEnvironmentVariables(@"%windir%\syswow64\cmd.exe");

            s_cmd64Path = (IntPtr.Size == sizeof(int))
                ? Environment.ExpandEnvironmentVariables(@"%windir%\sysnative\cmd.exe")
                : Environment.ExpandEnvironmentVariables(@"%windir%\System32\cmd.exe");

            // blank out the path so that we know we're not inadvertently depending on it.
            s_oldPath = Environment.GetEnvironmentVariable("PATH");

            if (Environment.OSVersion.Platform == PlatformID.MacOSX || Environment.OSVersion.Platform == PlatformID.Unix)
            {
                Environment.SetEnvironmentVariable("PATH", "/sbin:/bin");
            }
            else
            {
                Environment.SetEnvironmentVariable(
                    "PATH",
                    Environment.ExpandEnvironmentVariables("%windir%\\system32;%windir%"));
            }

            // Call StopTrackingAndCleanup here, just in case one of the unit tests failed before it called it
            // In real code StopTrackingAndCleanup(); would always be in a finally {} block.
            FileTracker.StopTrackingAndCleanup();
            FileTrackerTestHelper.CleanTlogs();
            FileTracker.SetThreadCount(1);
        }

        public void Dispose()
        {
            // Reset PATH to its original value. 
            if (s_oldPath != null)
            {
                Environment.SetEnvironmentVariable("PATH", s_oldPath);
                s_oldPath = null;
            }

            FileTrackerTestHelper.CleanTlogs();
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void FileTrackerHelp()
        {
            Console.WriteLine("Test: FileTracker");
            int exit = FileTrackerTestHelper.RunCommand(s_defaultTrackerPath, "");

            Assert.Equal(1, exit);
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void FileTrackerBadArg()
        {
            Console.WriteLine("Test: FileTrackerBadArg");

            int exit = FileTrackerTestHelper.RunCommandWithLog(s_defaultTrackerPath, "/q", out string log);

            Assert.Equal(1, exit);
            Assert.Contains("TRK0000", log); // bad arg
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void FileTrackerNoUIDll()
        {
            Console.WriteLine("Test: FileTrackerNoUIDll");
            string testDirectory = Path.Combine(Directory.GetCurrentDirectory(), "FileTrackerNoUIDll");
            string testTrackerPath = Path.Combine(testDirectory, Path.GetFileName(s_defaultTrackerPath));

            try
            {
                if (Directory.Exists(testDirectory))
                {
                    ObjectModelHelpers.DeleteDirectory(testDirectory);
                    Directory.Delete(testDirectory, true);
                }

                // create an empty directory and copy Tracker.exe -- BUT NOT TrackerUI.dll -- to 
                // that directory. 
                Directory.CreateDirectory(testDirectory);
                File.Copy(s_defaultTrackerPath, testTrackerPath);

                int exit = FileTrackerTestHelper.RunCommandWithLog(testTrackerPath, "/?", out string log);

                Assert.Equal(9, exit);
                // It's OK to look for the English message since that's all we're capable of printing when we can't find
                // our resource dll. 
                Assert.Contains("FileTracker : ERROR : Could not load UI satellite dll 'TrackerUI.dll'", log);
            }
            finally
            {
                // Doesn't delete the directory itself, but deletes its contents.  If you try to delete the directory, 
                // even after calling this method, it sometimes throws IO exceptions due to not recognizing that the 
                // contents have been deleted yet. 
                ObjectModelHelpers.DeleteDirectory(testDirectory);
            }
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void FileTrackerNonexistentRspFile()
        {
            Console.WriteLine("Test: FileTrackerNonexistentRspFile");

            File.Delete("findstr.read.1.tlog");
            FileTrackerTestHelper.WriteAll("test.in", "foo");

            int exit = FileTrackerTestHelper.RunCommandWithLog(s_defaultTrackerPath, "/d " + s_defaultFileTrackerPath + " @abc.rsp /c findstr /ip foo test.in", out string log);
            Console.WriteLine("");

            // missing rsp file is a non-fatal error
            Assert.Equal(0, exit);
            FileTrackerTestHelper.AssertFoundStringInTLog(Path.GetFullPath("test.in").ToUpperInvariant(), "findstr.read.1.tlog");

            // but it should still be reported
            Assert.Contains("Tracker.exe:", log);
            Assert.Contains("abc.rsp", log);
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void FileTrackerWithDll()
        {
            Console.WriteLine("Test: FileTrackerWithDll");

            int exit = FileTrackerTestHelper.RunCommand(s_defaultTrackerPath, "/d " + s_defaultFileTrackerPath);

            Assert.Equal(1, exit);
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void FileTrackerReadOnlyTlog()
        {
            Console.WriteLine("Test: FileTrackerTlogWriteFailure");
            string tlog = "findstr.read.1.tlog";
            string trackerCommand = "/d " + s_defaultFileTrackerPath + " /c findstr /ip foo test.in";

            File.Delete(tlog);
            FileTrackerTestHelper.WriteAll("test.in", "foo");

            try
            {
                int exit = FileTrackerTestHelper.RunCommand(s_defaultTrackerPath, trackerCommand);
                Console.WriteLine("");
                Assert.Equal(0, exit);
                FileTrackerTestHelper.AssertFoundStringInTLog(Path.GetFullPath("test.in").ToUpperInvariant(), tlog);

                File.SetAttributes(tlog, FileAttributes.ReadOnly);

                exit = FileTrackerTestHelper.RunCommandWithLog(s_defaultTrackerPath, trackerCommand, out string log);
                Console.WriteLine("");
                Assert.Equal(0, exit);
                Assert.Contains("FTK1011", log); // could not create new log:  the file exists.
            }
            finally
            {
                File.SetAttributes(tlog, FileAttributes.Normal);
                File.Delete(tlog);
            }
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void FileTrackerFindStrIn()
        {
            Console.WriteLine("Test: FileTrackerFindStrIn");

            File.Delete("findstr.read.1.tlog");
            FileTrackerTestHelper.WriteAll("test.in", "foo");

            int exit = FileTrackerTestHelper.RunCommand(s_defaultTrackerPath, "/d " + s_defaultFileTrackerPath + " /c findstr /ip foo test.in");
            Console.WriteLine("");
            Assert.Equal(0, exit);
            FileTrackerTestHelper.AssertFoundStringInTLog(Path.GetFullPath("test.in").ToUpperInvariant(), "findstr.read.1.tlog");
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void FileTrackerFindStrInOperations()
        {
            Console.WriteLine("Test: FileTrackerFindStrInOperations");

            File.Delete("findstr.read.1.tlog");
            FileTrackerTestHelper.WriteAll("test.in", "foo");

            int exit = FileTrackerTestHelper.RunCommand(s_defaultTrackerPath, "/d " + s_defaultFileTrackerPath + " /o /c findstr /ip foo test.in");
            Console.WriteLine("");
            Assert.Equal(0, exit);

            // On some OS's it calls CreateFileA as well, on Windows7 it doesn't, but it calls CreateFileW on defaultsort.nls..
            bool foundW = FileTrackerTestHelper.FindStringInTlog("CreateFileW, Desired Access=0x80000000, Creation Disposition=0x3:" + Path.GetFullPath("test.in").ToUpperInvariant(), "findstr.read.1.tlog");
            bool foundA = FileTrackerTestHelper.FindStringInTlog("CreateFileA, Desired Access=0x80000000, Creation Disposition=0x3:" + Path.GetFullPath("test.in").ToUpperInvariant(), "findstr.read.1.tlog");
            Assert.True(foundW || foundA);
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void FileTrackerFindStrInOperationsExtended()
        {
            Console.WriteLine("Test: FileTrackerFindStrInOperationsExtended");

            File.Delete("findstr.read.1.tlog");
            FileTrackerTestHelper.WriteAll("test.in", "foo");

            int exit = FileTrackerTestHelper.RunCommand(s_defaultTrackerPath, "/d " + s_defaultFileTrackerPath + " /o /e /c findstr /ip foo test.in");
            Console.WriteLine("");
            Assert.Equal(0, exit);

            // On some OS's it calls GetFileAttributesW as well, on Windows 2k8 R2 it doesn't
            bool foundGetFileAttributesW = FileTrackerTestHelper.FindStringInTlog("GetFileAttributesW:" + Path.GetFullPath("test.in").ToUpperInvariant(), "findstr.read.1.tlog");
            bool foundGetFileAttributesA = FileTrackerTestHelper.FindStringInTlog("GetFileAttributesA:" + Path.GetFullPath("test.in").ToUpperInvariant(), "findstr.read.1.tlog");
            Assert.True(foundGetFileAttributesW || foundGetFileAttributesA);

            // On some OS's it calls CreateFileA as well, on Windows7 it doesn't, but it calls CreateFileW on defaultsort.nls..
            bool foundCreateFileW = FileTrackerTestHelper.FindStringInTlog("CreateFileW, Desired Access=0x80000000, Creation Disposition=0x3:" + Path.GetFullPath("test.in").ToUpperInvariant(), "findstr.read.1.tlog");
            bool foundCreateFileA = FileTrackerTestHelper.FindStringInTlog("CreateFileA, Desired Access=0x80000000, Creation Disposition=0x3:" + Path.GetFullPath("test.in").ToUpperInvariant(), "findstr.read.1.tlog");
            Assert.True(foundCreateFileW || foundCreateFileA);
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void FileTrackerFindStrInOperationsExtended_AttributesOnly()
        {
            Console.WriteLine("Test: FileTrackerFindStrInOperationsExtended_AttributesOnly");

            File.Delete("findstr.read.1.tlog");
            FileTrackerTestHelper.WriteAll("test.in", "foo");

            int exit = FileTrackerTestHelper.RunCommand(s_defaultTrackerPath, "/d " + s_defaultFileTrackerPath + " /o /a /c findstr /ip foo test.in");
            Console.WriteLine("");
            Assert.Equal(0, exit);
            // On some OS's it calls GetFileAttributesW as well, on Windows 2k8 R2 it doesn't
            bool foundGetFileAttributesW = FileTrackerTestHelper.FindStringInTlog("GetFileAttributesW:" + Path.GetFullPath("test.in").ToUpperInvariant(), "findstr.read.1.tlog");
            bool foundGetFileAttributesA = FileTrackerTestHelper.FindStringInTlog("GetFileAttributesA:" + Path.GetFullPath("test.in").ToUpperInvariant(), "findstr.read.1.tlog");
            Assert.True(foundGetFileAttributesW || foundGetFileAttributesA);

            // On some OS's it calls CreateFileA as well, on Windows7 it doesn't, but it calls CreateFileW on defaultsort.nls..
            bool foundCreateFileW = FileTrackerTestHelper.FindStringInTlog("CreateFileW, Desired Access=0x80000000, Creation Disposition=0x3:" + Path.GetFullPath("test.in").ToUpperInvariant(), "findstr.read.1.tlog");
            bool foundCreateFileA = FileTrackerTestHelper.FindStringInTlog("CreateFileA, Desired Access=0x80000000, Creation Disposition=0x3:" + Path.GetFullPath("test.in").ToUpperInvariant(), "findstr.read.1.tlog");
            Assert.True(foundCreateFileW || foundCreateFileA);
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void FileTrackerExtendedDirectoryTracking()
        {
            Console.WriteLine("Test: FileTrackerExtendedDirectoryTracking");

            File.Delete("directoryattributes.read.1.tlog");
            File.Delete("directoryattributes.write.1.tlog");

            string codeFile = null;
            string outputFile = Path.Combine(Path.GetTempPath(), "directoryattributes.exe");
            string codeContent = @"
using System.IO;
using System.Runtime.InteropServices;

namespace ConsoleApplication4
{
    class Program
    {
        static void Main(string[] args)
        {
            File.GetAttributes(Directory.GetCurrentDirectory());
            GetFileAttributes(Directory.GetCurrentDirectory()); 
        }

        [DllImport(""Kernel32.dll"", SetLastError = true, CharSet = CharSet.Unicode)]
        private extern static uint GetFileAttributes(string FileName); 
    }
}";

            File.Delete(outputFile);

            try
            {
                codeFile = FileUtilities.GetTemporaryFile();
                File.WriteAllText(codeFile, codeContent);
                Csc csc = new Csc();
                csc.BuildEngine = new MockEngine();
                csc.Sources = new ITaskItem[] { new TaskItem(codeFile) };
                csc.OutputAssembly = new TaskItem(outputFile);
                csc.Execute();

                string trackerPath = FileTracker.GetTrackerPath(ExecutableType.ManagedIL);
                string fileTrackerPath = FileTracker.GetFileTrackerPath(ExecutableType.ManagedIL);
                string commandArgs = "/d \"" + fileTrackerPath + "\" /o /u /e /c \"" + outputFile + "\"";

                int exit = FileTrackerTestHelper.RunCommand(trackerPath, commandArgs);
                Console.WriteLine("");
                Assert.Equal(0, exit);

                // Should track directories when '/e' is passed
                FileTrackerTestHelper.AssertFoundStringInTLog("GetFileAttributesExW:" + FileUtilities.EnsureTrailingSlash(Directory.GetCurrentDirectory()).ToUpperInvariant(), "directoryattributes.read.1.tlog");
                FileTrackerTestHelper.AssertFoundStringInTLog("GetFileAttributesW:" + FileUtilities.EnsureTrailingSlash(Directory.GetCurrentDirectory()).ToUpperInvariant(), "directoryattributes.read.1.tlog");

                File.Delete("directoryattributes.read.1.tlog");
                File.Delete("directoryattributes.write.1.tlog");

                commandArgs = "/d \"" + fileTrackerPath + "\" /o /u /a /c \"" + outputFile + "\"";

                exit = FileTrackerTestHelper.RunCommand(trackerPath, commandArgs);
                Console.WriteLine("");
                Assert.Equal(0, exit);

                // With '/a', should *not* track GetFileAttributes on directories, even though we do so on files. 
                FileTrackerTestHelper.AssertDidntFindStringInTLog("GetFileAttributesExW:" + FileUtilities.EnsureTrailingSlash(Directory.GetCurrentDirectory()).ToUpperInvariant(), "directoryattributes.read.1.tlog");
                FileTrackerTestHelper.AssertDidntFindStringInTLog("GetFileAttributesW:" + FileUtilities.EnsureTrailingSlash(Directory.GetCurrentDirectory()).ToUpperInvariant(), "directoryattributes.read.1.tlog");

                File.Delete("directoryattributes.read.1.tlog");
                File.Delete("directoryattributes.write.1.tlog");

                commandArgs = "/d \"" + fileTrackerPath + "\" /o /u /c \"" + outputFile + "\"";

                exit = FileTrackerTestHelper.RunCommand(trackerPath, commandArgs);
                Console.WriteLine("");
                Assert.Equal(0, exit);

                // With neither '/a' nor '/e', should not do any directory tracking whatsoever
                FileTrackerTestHelper.AssertDidntFindStringInTLog("GetFileAttributesExW:" + FileUtilities.EnsureTrailingSlash(Directory.GetCurrentDirectory()).ToUpperInvariant(), "directoryattributes.read.1.tlog");
                FileTrackerTestHelper.AssertDidntFindStringInTLog("GetFileAttributesW:" + FileUtilities.EnsureTrailingSlash(Directory.GetCurrentDirectory()).ToUpperInvariant(), "directoryattributes.read.1.tlog");

                File.Delete("directoryattributes.read.1.tlog");
                File.Delete("directoryattributes.write.1.tlog");

                commandArgs = "/d \"" + fileTrackerPath + "\" /u /e /c \"" + outputFile + "\"";

                exit = FileTrackerTestHelper.RunCommand(trackerPath, commandArgs);
                Console.WriteLine("");
                Assert.Equal(0, exit);

                // Should track directories when '/e' is passed
                FileTrackerTestHelper.AssertFoundStringInTLog(FileUtilities.EnsureTrailingSlash(Directory.GetCurrentDirectory()).ToUpperInvariant(), "directoryattributes.read.1.tlog");

                File.Delete("directoryattributes.read.1.tlog");
                File.Delete("directoryattributes.write.1.tlog");

                commandArgs = "/d \"" + fileTrackerPath + "\" /u /a /c \"" + outputFile + "\"";

                exit = FileTrackerTestHelper.RunCommand(trackerPath, commandArgs);
                Console.WriteLine("");
                Assert.Equal(0, exit);

                // With '/a', should *not* track GetFileAttributes on directories, even though we do so on files. 
                FileTrackerTestHelper.AssertDidntFindStringInTLog(FileUtilities.EnsureTrailingSlash(Directory.GetCurrentDirectory()).ToUpperInvariant(), "directoryattributes.read.1.tlog");

                File.Delete("directoryattributes.read.1.tlog");
                File.Delete("directoryattributes.write.1.tlog");

                commandArgs = "/d \"" + fileTrackerPath + "\" /u /c \"" + outputFile + "\"";

                exit = FileTrackerTestHelper.RunCommand(trackerPath, commandArgs);
                Console.WriteLine("");
                Assert.Equal(0, exit);

                // With neither '/a' nor '/e', should not do any directory tracking whatsoever
                FileTrackerTestHelper.AssertDidntFindStringInTLog(FileUtilities.EnsureTrailingSlash(Directory.GetCurrentDirectory()).ToUpperInvariant(), "directoryattributes.read.1.tlog");
            }
            finally
            {
                File.Delete(codeFile);
                File.Delete(outputFile);
            }
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void FileTrackerFindStrInIncludeDuplicates()
        {
            Console.WriteLine("Test: FileTrackerFindStrInIncludeDuplicates");

            File.Delete("findstr.read.1.tlog");
            FileTrackerTestHelper.WriteAll("test.in", "foo");

            string codeFile = null;
            string outputFile = Path.Combine(Path.GetTempPath(), "readtwice.exe");
            File.Delete(outputFile);

            try
            {
                string inputPath = Path.GetFullPath("test.in");
                codeFile = FileUtilities.GetTemporaryFile();
                string codeContent = @"using System.IO; class X { static void Main() { File.ReadAllText(@""" + inputPath + @"""); File.ReadAllText(@""" + inputPath + @"""); }}";
                File.WriteAllText(codeFile, codeContent);
                Csc csc = new Csc();
                csc.BuildEngine = new MockEngine();
                csc.Sources = new[] { new TaskItem(codeFile) };
                csc.OutputAssembly = new TaskItem(outputFile);
                csc.Execute();

                string trackerPath = FileTracker.GetTrackerPath(ExecutableType.ManagedIL);
                string fileTrackerPath = FileTracker.GetFileTrackerPath(ExecutableType.ManagedIL);
                string commandArgs = "/d \"" + fileTrackerPath + "\" /u /c \"" + outputFile + "\"";

                int exit = FileTrackerTestHelper.RunCommand(trackerPath, commandArgs);
                Console.WriteLine("");
                Assert.Equal(0, exit);
            }
            finally
            {
                File.Delete(codeFile);
                File.Delete(outputFile);
            }

            FileTrackerTestHelper.AssertFoundStringInTLog(Path.GetFullPath("test.in").ToUpperInvariant(), "readtwice.read.1.tlog", 2);
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void FileTrackerDoNotRecordWriteAsRead()
        {
            Console.WriteLine("Test: FileTrackerDoNotRecordWriteAsRead");

            File.Delete("writenoread.read.1.tlog");
            File.Delete("writenoread.write.1.tlog");

            string testDirectory = Path.Combine(Directory.GetCurrentDirectory(), "FileTrackerDoNotRecordWriteAsRead");

            if (Directory.Exists(testDirectory))
            {
                ObjectModelHelpers.DeleteDirectory(testDirectory);
                Directory.Delete(testDirectory, true /* recursive delete */);
            }

            Directory.CreateDirectory(testDirectory);
            string writeFile;
            string outputFile = Path.Combine(testDirectory, "writenoread.exe");

            try
            {
                writeFile = Path.Combine(testDirectory, "test.out");
                string codeFile = Path.Combine(testDirectory, "code.cs");
                string codeContent = @"
using System.IO; 
using System.Runtime.InteropServices;
class X 
{ 
    static void Main() 
    { 
        FileStream f = File.Open(@""" + writeFile + @""", FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite);
        f.WriteByte(8);
        f.Close();
    }
}";

                File.WriteAllText(codeFile, codeContent);
                Csc csc = new Csc();
                csc.BuildEngine = new MockEngine();
                csc.Sources = new[] { new TaskItem(codeFile) };
                csc.OutputAssembly = new TaskItem(outputFile);
                bool success = csc.Execute();

                Assert.True(success);

                string trackerPath = FileTracker.GetTrackerPath(ExecutableType.ManagedIL);
                string fileTrackerPath = FileTracker.GetFileTrackerPath(ExecutableType.ManagedIL);
                string commandArgs = "/d \"" + fileTrackerPath + "\" /o /c \"" + outputFile + "\"";

                int exit = FileTrackerTestHelper.RunCommand(trackerPath, commandArgs);
                Console.WriteLine("");
                Assert.Equal(0, exit);
            }
            finally
            {
                // Doesn't delete the directory itself, but deletes its contents.  If you try to delete the directory, 
                // even after calling this method, it sometimes throws IO exceptions due to not recognizing that the 
                // contents have been deleted yet. 
                ObjectModelHelpers.DeleteDirectory(testDirectory);
            }

            FileTrackerTestHelper.AssertDidntFindStringInTLog("CreateFileW, Desired Access=0xc0000000, Creation Disposition=0x1:" + writeFile.ToUpperInvariant(), "writenoread.read.1.tlog");
            FileTrackerTestHelper.AssertFoundStringInTLog("CreateFileW, Desired Access=0xc0000000, Creation Disposition=0x1:" + writeFile.ToUpperInvariant(), "writenoread.write.1.tlog");
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void FileTrackerFindStrInCommandLine()
        {
            Console.WriteLine("Test: FileTrackerFindStrInCommandLine");

            File.Delete("findstr.read.1.tlog");
            FileTrackerTestHelper.WriteAll("test.in", "foo");

            int exit = FileTrackerTestHelper.RunCommand(s_defaultTrackerPath, "/d " + s_defaultFileTrackerPath + " /t /c findstr /ip foo test.in");
            string line = FileTrackerTestHelper.ReadLineFromFile("findstr.command.1.tlog", 1);
            Console.WriteLine("");
            Assert.Equal(0, exit);
            Assert.Equal("findstr /ip foo test.in", line);
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void FileTrackerFindStrInArgumentSpaces()
        {
            Console.WriteLine("Test: FileTrackerFindStrIn");

            File.Delete("findstr.read.1.tlog");
            FileTrackerTestHelper.WriteAll("test file.in", "foo");

            int exit = FileTrackerTestHelper.RunCommand(s_defaultTrackerPath, "/d " + s_defaultFileTrackerPath + " /c findstr /ip foo \"test file.in\"");
            Console.WriteLine("");
            Assert.Equal(0, exit);
            FileTrackerTestHelper.AssertFoundStringInTLog(Path.GetFullPath("test file.in").ToUpperInvariant(), "findstr.read.1.tlog");
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void FileTrackerFindUnicode()
        {
            Console.WriteLine("Test: FileTrackerFindUnicode");

            File.Delete("find.read.1.tlog");
            FileTrackerTestHelper.WriteAll("t\u1EBCst.in", "foo");

            // FINDSTR.EXE doesn't support unicode, so we'll use FIND.EXE which does
            int exit = FileTrackerTestHelper.RunCommandNoStdOut(s_defaultTrackerPath, "/d " + s_defaultFileTrackerPath + " /i . /c find /I \"\\\"foo\"\\\" t\u1EBCst.in");
            Console.WriteLine("");
            Assert.Equal(0, exit);
            FileTrackerTestHelper.AssertFoundStringInTLog(Path.GetFullPath("t\u1EBCst.in").ToUpperInvariant(), "find.read.1.tlog");
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void FileTrackerStartProcessFindStrIn()
        {
            Console.WriteLine("Test: FileTrackerStartProcessFindStrIn");

            File.Delete("findstr.read.1.tlog");
            FileTrackerTestHelper.WriteAll("test.in", "foo");

            Process p = FileTracker.StartProcess("findstr", "/ip foo test.in", ExecutableType.Native32Bit);
            p.WaitForExit();
            int exit = p.ExitCode;
            Console.WriteLine("");
            Assert.Equal(0, exit);
            FileTrackerTestHelper.AssertFoundStringInTLog(Path.GetFullPath("test.in").ToUpperInvariant(), "findstr.read.1.tlog");
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void FileTrackerResponseFile()
        {
            Console.WriteLine("Test: FileTrackerResponseFile");

            File.Delete("tracker.rsp");
            FileTrackerTestHelper.WriteAll("tracker.rsp", "/d " + s_defaultFileTrackerPath + " /r jibbit");

            File.Delete("findstr.read.1.tlog");
            FileTrackerTestHelper.WriteAll("test.in", "foo");

            int exit = FileTrackerTestHelper.RunCommand(s_defaultTrackerPath, "@tracker.rsp /c findstr /ip foo test.in");

            Console.WriteLine("");
            Assert.Equal(0, exit);
            Assert.Equal("^JIBBIT",
                                   FileTrackerTestHelper.ReadLineFromFile("findstr.read.1.tlog", 1).ToUpperInvariant());
            FileTrackerTestHelper.AssertFoundStringInTLog(Path.GetFullPath("test.in").ToUpperInvariant(), "findstr.read.1.tlog");
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void FileTrackerFindStrInRootFiles()
        {
            Console.WriteLine("Test: FileTrackerFindStrInRootFiles");

            File.Delete("findstr.read.1.tlog");
            FileTrackerTestHelper.WriteAll("test.in", "foo");

            int exit = FileTrackerTestHelper.RunCommand(s_defaultTrackerPath, "/d " + s_defaultFileTrackerPath + " /r jibbit /c findstr /ip foo test.in");

            Console.WriteLine("");
            Assert.Equal(0, exit);
            Assert.Equal("^JIBBIT",
                                   FileTrackerTestHelper.ReadLineFromFile("findstr.read.1.tlog", 1).ToUpperInvariant());
            FileTrackerTestHelper.AssertFoundStringInTLog(Path.GetFullPath("test.in").ToUpperInvariant(), "findstr.read.1.tlog");
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void FileTrackerFindStrInRootFilesCommand()
        {
            Console.WriteLine("Test: FileTrackerFindStrInRootFilesCommand");

            File.Delete("findstr.read.1.tlog");
            File.Delete("findstr.command.1.tlog");
            FileTrackerTestHelper.WriteAll("test.in", "foo");

            int exit = FileTrackerTestHelper.RunCommand(s_defaultTrackerPath, "/t /d " + s_defaultFileTrackerPath + " /r jibbit /c findstr /ip foo test.in");

            Console.WriteLine("");
            Assert.Equal(0, exit);
            Assert.Equal("^JIBBIT",
                                   FileTrackerTestHelper.ReadLineFromFile("findstr.read.1.tlog", 1).ToUpperInvariant());
            FileTrackerTestHelper.AssertFoundStringInTLog(Path.GetFullPath("test.in").ToUpperInvariant(), "findstr.read.1.tlog");
            Assert.Equal("findstr /ip foo test.in",
                                   FileTrackerTestHelper.ReadLineFromFile("findstr.command.1.tlog", 2));
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void FileTrackerFindStrInRootFilesSpaces()
        {
            Console.WriteLine("Test: FileTrackerFindStrInRootFilesSpaces");

            File.Delete("findstr.read.1.tlog");
            FileTrackerTestHelper.WriteAll("test.in", "foo");

            int exit = FileTrackerTestHelper.RunCommand(s_defaultTrackerPath, "/d " + s_defaultFileTrackerPath + " /r \"jibbit goo\" /c findstr /ip foo test.in");

            Console.WriteLine("");
            Assert.Equal(0, exit);
            Assert.Equal("^JIBBIT GOO",
                                   FileTrackerTestHelper.ReadLineFromFile("findstr.read.1.tlog", 1).ToUpperInvariant());
            FileTrackerTestHelper.AssertFoundStringInTLog(Path.GetFullPath("test.in").ToUpperInvariant(), "findstr.read.1.tlog");
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void FileTrackerHelperCommandLine()
        {
            Console.WriteLine("Test: FileTrackerHelperCommandLine");

            File.Delete("findstr.read.1.tlog");
            FileTrackerTestHelper.WriteAll("test.in", "foo");

            int exit = FileTrackerTestHelper.RunCommand(
                s_defaultTrackerPath,
                FileTracker.TrackerArguments(
                    "findstr",
                    "/ip foo test.in",
                    "" + s_defaultFileTrackerPathUnquoted,
                    ".",
                    "jibbit goo"));

            Console.WriteLine("");
            Assert.Equal(0, exit);
            Assert.Equal("^JIBBIT GOO",
                                   FileTrackerTestHelper.ReadLineFromFile("findstr.read.1.tlog", 1).ToUpperInvariant());
            FileTrackerTestHelper.AssertFoundStringInTLog(Path.GetFullPath("test.in").ToUpperInvariant(), "findstr.read.1.tlog");
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void FileTrackerSortOut()
        {
            Console.WriteLine("Test: FileTrackerSortOut");

            File.Delete("sort.read.1.tlog");
            File.Delete("sort.write.1.tlog");
            File.WriteAllLines("test.in", new[] {
                                                            "bfoo",
                                                            "afoo"
                                                       });

            int exit = FileTrackerTestHelper.RunCommand(s_defaultTrackerPath, "/d " + s_defaultFileTrackerPath + " /c sort test.in /O test.out");

            Assert.Equal(0, exit);

            FileTrackerTestHelper.AssertFoundStringInTLog(Path.GetFullPath("test.in").ToUpperInvariant(), "sort.read.1.tlog");
            FileTrackerTestHelper.AssertFoundStringInTLog(Path.GetFullPath("test.out").ToUpperInvariant(), "sort.write.1.tlog");

            Assert.Equal("AFOO",
                                   FileTrackerTestHelper.ReadLineFromFile("test.out", 0).ToUpperInvariant());

            Assert.Equal("BFOO",
                                   FileTrackerTestHelper.ReadLineFromFile("test.out", 1).ToUpperInvariant());
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void FileTrackerSortOutIntermediate()
        {
            Console.WriteLine("Test: FileTrackerSortOutIntermediate");

            Directory.CreateDirectory("outdir");
            File.Delete("outdir\\sort.read.1.tlog");
            File.Delete("outdir\\sort.write.1.tlog");
            File.WriteAllLines("test.in", new[] {
                                                            "bfoo",
                                                            "afoo"
                                                       });

            int exit = FileTrackerTestHelper.RunCommand(s_defaultTrackerPath, "/d " + s_defaultFileTrackerPath + " /i outdir /c sort test.in /O test.out");

            Assert.Equal(0, exit);

            FileTrackerTestHelper.AssertFoundStringInTLog(Path.GetFullPath("test.in").ToUpperInvariant(), "outdir\\sort.read.1.tlog");
            FileTrackerTestHelper.AssertFoundStringInTLog(Path.GetFullPath("test.out").ToUpperInvariant(), "outdir\\sort.write.1.tlog");

            Assert.Equal("AFOO",
                                   FileTrackerTestHelper.ReadLineFromFile("test.out", 0).ToUpperInvariant());

            Assert.Equal("BFOO",
                                   FileTrackerTestHelper.ReadLineFromFile("test.out", 1).ToUpperInvariant());
        }


        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void FileTrackerIntermediateDirMissing()
        {
            Console.WriteLine("Test: FileTrackerIntermediateDirMissing");

            // Make sure it really is missing
            if (Directory.Exists("outdir"))
            {
                Directory.Delete("outdir", true);
            }

            File.WriteAllLines("test.in", new[] {
                                                            "bfoo",
                                                            "afoo"
                                                       });

            int exit = FileTrackerTestHelper.RunCommand(s_defaultTrackerPath, "/d " + s_defaultFileTrackerPath + " /i outdir /c sort test.in /O test.out");

            Assert.Equal(0, exit);

            FileTrackerTestHelper.AssertFoundStringInTLog(Path.GetFullPath("test.in").ToUpperInvariant(), "outdir\\sort.read.1.tlog");
            FileTrackerTestHelper.AssertFoundStringInTLog(Path.GetFullPath("test.out").ToUpperInvariant(), "outdir\\sort.write.1.tlog");

            Assert.Equal("AFOO",
                                   FileTrackerTestHelper.ReadLineFromFile("test.out", 0).ToUpperInvariant());

            Assert.Equal("BFOO",
                                   FileTrackerTestHelper.ReadLineFromFile("test.out", 1).ToUpperInvariant());
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void FileTrackerFindStrInChain()
        {
            Console.WriteLine("Test: FileTrackerFindStrInChain");

            File.Delete("cmd-findstr.read.1.tlog");
            FileTrackerTestHelper.WriteAll("test.in", "foo");

            int exit = FileTrackerTestHelper.RunCommand(s_defaultTrackerPath, "/d " + s_defaultFileTrackerPath + " /c cmd /c findstr /ip foo test.in");
            Console.WriteLine("");
            Assert.Equal(0, exit);
            FileTrackerTestHelper.AssertFoundStringInTLog(Path.GetFullPath("test.in").ToUpperInvariant(), "cmd-findstr.read.1.tlog");
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void FileTrackerFindStrInChainRepeatCommand()
        {
            Console.WriteLine("Test: FileTrackerFindStrInChainRepeatCommand");

            string[] tlogFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "cmd*-findstr.*.1.tlog", SearchOption.TopDirectoryOnly);
            foreach (string tlogFile in tlogFiles)
            {
                File.Delete(tlogFile);
            }
            FileTrackerTestHelper.WriteAll("test.in", "foo");

            int exit = FileTrackerTestHelper.RunCommand(s_defaultTrackerPath, "/d " + s_defaultFileTrackerPath + " /c cmd /c cmd /c findstr /ip foo test.in");
            tlogFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "cmd*-findstr.read.1.tlog", SearchOption.TopDirectoryOnly);
            Console.WriteLine("");
            Assert.Equal(0, exit);
            FileTrackerTestHelper.AssertFoundStringInTLog(Path.GetFullPath("test.in").ToUpperInvariant(), tlogFiles[0]);
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void FileTrackerFindStrInX64X86ChainRepeatCommand()
        {
            Console.WriteLine("Test: FileTrackerFindStrInX64X86ChainRepeatCommand");

            if (!Environment.Is64BitOperatingSystem)
            {
                Console.WriteLine("FileTrackerFindStrInX64X86ChainRepeatCommand runs both 32-and 64-bit programs so it requires 64-bit Windows.");
                Assert.True(true);
                return;
            }

            string[] tlogFiles = Directory.GetFiles(Environment.CurrentDirectory, "cmd*-findstr.*.1.tlog", SearchOption.TopDirectoryOnly);
            foreach (string tlogFile in tlogFiles)
            {
                File.Delete(tlogFile);
            }
            FileTrackerTestHelper.WriteAll("test.in", "foo");

            int exit = FileTrackerTestHelper.RunCommand(s_defaultTrackerPath, "/d " + s_defaultFileTrackerPath + " /c " + s_cmd64Path + " /c " + s_cmd32Path + " /c findstr /ip foo test.in");
            tlogFiles = Directory.GetFiles(Environment.CurrentDirectory, "cmd*-findstr.read.1.tlog", SearchOption.TopDirectoryOnly);
            Console.WriteLine("");
            Assert.Equal(0, exit);
            FileTrackerTestHelper.AssertFoundStringInTLog(Path.GetFullPath("test.in").ToUpperInvariant(), tlogFiles[0]);
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void FileTrackerFindStrInX86X64ChainRepeatCommand()
        {
            Console.WriteLine("Test: FileTrackerFindStrInX86X64ChainRepeatCommand");

            if (!Environment.Is64BitOperatingSystem)
            {
                Console.WriteLine("FileTrackerFindStrInX86X64ChainRepeatCommand runs both 32-and 64-bit programs so it requires 64-bit Windows.");
                Assert.True(true);
                return;
            }

            string[] tlogFiles = Directory.GetFiles(Environment.CurrentDirectory, "cmd*-findstr.*.1.tlog", SearchOption.TopDirectoryOnly);
            foreach (string tlogFile in tlogFiles)
            {
                File.Delete(tlogFile);
            }
            FileTrackerTestHelper.WriteAll("test.in", "foo");

            int exit = FileTrackerTestHelper.RunCommand(s_defaultTrackerPath, "/d " + s_defaultFileTrackerPath + " /c " + s_cmd32Path + " /c " + s_cmd64Path + " /c findstr /ip foo test.in");
            tlogFiles = Directory.GetFiles(Environment.CurrentDirectory, "cmd*-findstr.read.1.tlog", SearchOption.TopDirectoryOnly);
            Console.WriteLine("");
            Assert.Equal(0, exit);
            FileTrackerTestHelper.AssertFoundStringInTLog(Path.GetFullPath("test.in").ToUpperInvariant(), tlogFiles[0]);
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void FileTrackerFileIsUnderPath()
        {
            Console.WriteLine("Test: FileTrackerFileIsUnderPath");

            // YES: Both refer to something under baz, so yes this is on the path
            Assert.True(FileTracker.FileIsUnderPath(@"c:\foo\bar\baz\", @"c:\foo\bar\baz\"));

            // NO: Not under the path, since this *is* the path
            Assert.False(FileTracker.FileIsUnderPath(@"c:\foo\bar\baz", @"c:\foo\bar\baz\"));

            // NO: Not under the path, since the path is below
            Assert.False(FileTracker.FileIsUnderPath(@"c:\foo\bar\baz", @"c:\foo\bar\baz\"));

            // YES: Since the first parameter is a filename the extra '\' indicates we are referring to something
            // other than the actual directory - so this would be under the path
            Assert.True(FileTracker.FileIsUnderPath(@"c:\foo\bar\baz\", @"c:\foo\bar\baz"));

            // YES: this is under the path
            Assert.True(FileTracker.FileIsUnderPath(@"c:\foo\bar\baz\hobbits.tmp", @"c:\foo\bar\baz\"));

            // YES: this is under the path
            Assert.True(FileTracker.FileIsUnderPath(@"c:\foo\bar\baz\hobbits.tmp", @"c:\foo\bar\baz"));

            // YES: this is under the path
            Assert.True(FileTracker.FileIsUnderPath(@"c:\foo\bar\baz\hobbits", @"c:\foo\bar\baz\"));

            // YES: this is under the path
            Assert.True(FileTracker.FileIsUnderPath(@"c:\foo\bar\baz\hobbits", @"c:\foo\bar\baz"));

            // YES: this is under the path
            Assert.True(FileTracker.FileIsUnderPath(@"c:\foo\bar\baz\bootle\hobbits.tmp", @"c:\foo\bar\baz\"));

            // NO: this is not under the path
            Assert.False(FileTracker.FileIsUnderPath(@"c:\foo\bar\baz\hobbits.tmp", @"c:\boo1\far\chaz\"));

            // NO: this is not under the path
            Assert.False(FileTracker.FileIsUnderPath(@"c:\foo1.cpp", @"c:\averyveryverylongtemp\path\this\is"));

            // NO: this is not under the path
            Assert.False(FileTracker.FileIsUnderPath(@"c:\foo\rumble.cpp", @"c:\foo\rumble"));

            // NO: this is not under the path
            Assert.False(FileTracker.FileIsUnderPath(@"c:\foo\rumble.cpp", @"c:\foo\rumble\"));
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void FileTrackerFileIsExcludedFromDependencies()
        {
            Console.WriteLine("Test: FileTrackerFileIsExcludedFromDependencies");

            string applicationDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string localApplicationDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string localLowApplicationDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData\\LocalLow");
            // The default path to temp, used to create explicitly short and long paths
            string tempPath = Path.GetTempPath();
            // The short path to temp
            string tempShortPath = NativeMethodsShared.IsUnixLike
                                       ? tempPath
                                       : FileUtilities.EnsureTrailingSlash(
                                           NativeMethodsShared.GetShortFilePath(tempPath).ToUpperInvariant());
            // The long path to temp
            string tempLongPath = NativeMethodsShared.IsUnixLike
                                      ? tempPath
                                      : FileUtilities.EnsureTrailingSlash(
                                          NativeMethodsShared.GetLongFilePath(tempPath).ToUpperInvariant());
            string testFile;

            // We don't want to be including these as dependencies or outputs:
            // 1. Files under %USERPROFILE%\Application Data in XP and %USERPROFILE%\AppData\Roaming in Vista and later.
            // 2. Files under %USERPROFILE%\Local Settings\Application Data in XP and %USERPROFILE%\AppData\Local in Vista and later.
            // 3. Files under %USERPROFILE%\AppData\LocalLow in Vista and later.
            // 4. Files that are in the TEMP directory (Since on XP, temp files are not
            //    located under AppData, they would not be compacted out correctly otherwise).

            // This file's NOT excluded from dependencies
            testFile = @"c:\foo\bar\baz";
            Assert.False(FileTracker.FileIsExcludedFromDependencies(testFile));

            // This file IS excluded from dependencies
            testFile = Path.Combine(applicationDataPath, "blah.log");
            Assert.True(FileTracker.FileIsExcludedFromDependencies(testFile));

            // This file IS excluded from dependencies
            testFile = Path.Combine(localApplicationDataPath, "blah.log");
            Assert.True(FileTracker.FileIsExcludedFromDependencies(testFile));

            // This file IS excluded from dependencies
            testFile = Path.Combine(localLowApplicationDataPath, "blah.log");
            Assert.True(FileTracker.FileIsExcludedFromDependencies(testFile));

            // This file IS excluded from dependencies
            testFile = Path.Combine(tempShortPath, "blah.log");
            Assert.True(FileTracker.FileIsExcludedFromDependencies(testFile));

            // This file IS excluded from dependencies
            testFile = Path.Combine(tempLongPath, "blah.log");
            Assert.True(FileTracker.FileIsExcludedFromDependencies(testFile));
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void InProcTrackingTest1()
        {
            string sourceFile = "inlinetrackingtest.txt";
            string tlogRootName = "foo_inline";
            string tlogWriteFile = $"{tlogRootName}.write.1.tlog";

            File.Delete(tlogWriteFile);

            FileTracker.StartTrackingContext(Path.GetFullPath("."), "InProcTrackingTest1");

            File.WriteAllText(sourceFile, "this is a inline tracking test");

            FileTracker.WriteContextTLogs(Path.GetFullPath("."), tlogRootName);

            FileTracker.StopTrackingAndCleanup();
            string[] lines = FileTrackerTestHelper.ReadLinesFromFile(tlogWriteFile);
            Assert.Equal(2, lines.Length);
            Assert.Equal(Path.GetFullPath(sourceFile).ToUpperInvariant(), lines[1]);

            File.Delete(tlogWriteFile);
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void InProcTrackingTest2()
        {
            // Do test 1 twice in a row to make sure there is no leakage
            InProcTrackingTest1();
            InProcTrackingTest1();
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void InProcTrackingTestSuspendResume()
        {
            string sourceFile = "inlinetrackingtest.txt";
            string tlogRootName = "foo_inline";
            string tlogWriteFile = $"{tlogRootName}.write.1.tlog";

            File.Delete(tlogWriteFile);

            FileTracker.StartTrackingContext(Path.GetFullPath("."), "InProcTrackingTestSuspendResume");

            File.WriteAllText(sourceFile, "this is a inline tracking test");

            // Nothing should be tracked following this call
            FileTracker.SuspendTracking();

            File.WriteAllText(sourceFile + "_s", "this is a inline tracking test");

            // And tracking should resume
            FileTracker.ResumeTracking();

            File.WriteAllText(sourceFile + "_r", "this is a inline tracking test");

            FileTracker.WriteContextTLogs(Path.GetFullPath("."), tlogRootName);

            FileTracker.StopTrackingAndCleanup();
            string[] lines = FileTrackerTestHelper.ReadLinesFromFile(tlogWriteFile);
            Assert.Equal(3, lines.Length);
            Assert.Equal(Path.GetFullPath(sourceFile).ToUpperInvariant(), lines[1]);
            Assert.Equal(Path.GetFullPath(sourceFile + "_r").ToUpperInvariant(), lines[2]);

            File.Delete(tlogWriteFile);
            File.Delete(sourceFile);
            File.Delete(sourceFile + "_s");
            File.Delete(sourceFile + "_r");
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void InProcTrackingTestStopBeforeWrite()
        {
            Assert.Throws<COMException>(() =>
            {
                string sourceFile = "inlinetrackingtest.txt";
                string tlogRootName = "foo_inline";
                string tlogWriteFile = $"{tlogRootName}.write.1.tlog";

                File.Delete(tlogWriteFile);
                File.Delete(sourceFile);

                FileTracker.StartTrackingContext(Path.GetFullPath("."), "InProcTrackingTestStopBeforeWrite");

                File.WriteAllText(sourceFile, "this is a inline tracking test");

                FileTracker.StopTrackingAndCleanup();

                // This should throw a COMException, since we have cleaned up
                FileTracker.WriteContextTLogs(Path.GetFullPath("."), tlogRootName);
            }
           );
        }
        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void InProcTrackingTestNotStop()
        {
            InProcTrackingTesterNoStop(1);
            // Since we didn't stop in the test, we should stop now
            // to ensure we don't leak into the other tests
            FileTracker.StopTrackingAndCleanup();
        }

        private static void InProcTrackingTesterNoStop(int iteration)
        {
            string sourceFile = $"inlinetrackingtest{iteration}.txt";
            string tlogRootName = $"foo_nonstopinline{iteration}";
            string tlogWriteFile = $"{tlogRootName}.write.1.tlog";
            string tlogReadFile = $"{tlogRootName}.read.1.tlog";

            File.Delete(tlogWriteFile);
            File.Delete(tlogReadFile);

            FileTracker.StartTrackingContext(Path.GetFullPath("."), "InProcTrackingTesterNoStop");

            File.WriteAllText(sourceFile, "this is a inline tracking test");

            FileTracker.WriteContextTLogs(Path.GetFullPath("."), tlogRootName);

            File.WriteAllText(sourceFile + "_s", "this is a inline tracking test - again");

            FileTracker.WriteContextTLogs(Path.GetFullPath("."), tlogRootName);

            string[] lines = FileTrackerTestHelper.ReadLinesFromFile(tlogWriteFile);
            Assert.Equal(4, lines.Length);
            Assert.Equal(Path.GetFullPath(sourceFile).ToUpperInvariant(), lines[1]);
            Assert.Equal(Path.GetFullPath(sourceFile + "_s").ToUpperInvariant(), lines[3]);

            File.Delete(tlogWriteFile);
            // Since we are non-stop during iteration we actually get read tlogs
            // Because of the "ReadLinesFromFile" above. However it will be empty
            // Since by default the tracker does not write entries for files that
            // do not exist - and we did delete the file being tracked on the previous
            // iteration!
            File.Delete(tlogReadFile);
            File.Delete(sourceFile);
            File.Delete(sourceFile + "_s");
        }

        private static void InProcTrackingTester(int iteration)
        {
            string sourceFile = $"inlinetrackingtest{iteration}.txt";
            string tlogRootName = $"foo_inline{iteration}";
            string tlogWriteFile = $"{tlogRootName}.write.1.tlog";

            File.Delete(tlogWriteFile);

            FileTracker.StartTrackingContext(Path.GetFullPath("."), "InProcTrackingTester");

            File.WriteAllText(sourceFile, "this is a inline tracking test");

            FileTracker.WriteContextTLogs(Path.GetFullPath("."), tlogRootName);

            FileTracker.StopTrackingAndCleanup();
            string[] lines = FileTrackerTestHelper.ReadLinesFromFile(tlogWriteFile);
            Assert.Equal(2, lines.Length);
            Assert.Equal(Path.GetFullPath(sourceFile).ToUpperInvariant(), lines[1]);

            File.Delete(tlogWriteFile);
            File.Delete(sourceFile);
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void InProcTrackingTestIteration()
        {
            for (int iter = 0; iter < 50; iter++)
            {
                InProcTrackingTester(iter);
            }
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void InProcTrackingNonStopTestIteration()
        {
            for (int iter = 0; iter < 50; iter++)
            {
                InProcTrackingTesterNoStop(iter);
            }
            FileTracker.StopTrackingAndCleanup();
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void InProcTrackingTwoContexts()
        {
            string sourceFile = "inlinetrackingtest.txt";
            string sourceFile2 = "inlinetrackingtest2.txt";
            string sourceFile3 = "inlinetrackingtest3.txt";
            string tlogRootName = "foo_inline";
            string tlogWriteFile = $"{tlogRootName}.write.1.tlog";
            string tlogWriteFile2 = $"{tlogRootName}2.write.1.tlog";

            File.Delete(tlogWriteFile);
            File.Delete(tlogWriteFile2);

            // Context 1
            FileTracker.StartTrackingContext(Path.GetFullPath("."), "Context1");
            File.WriteAllText(sourceFile, "this is a inline tracking test");

            // Context 2
            FileTracker.StartTrackingContext(Path.GetFullPath("."), "Context2");
            File.WriteAllText(sourceFile2, "this is a inline tracking test - in a second context");
            FileTracker.WriteContextTLogs(Path.GetFullPath("."), tlogRootName + "2");
            FileTracker.EndTrackingContext();

            // Back to context 1
            File.WriteAllText(sourceFile3, "this is a second inline tracking test in the first context");
            FileTracker.WriteContextTLogs(Path.GetFullPath("."), tlogRootName);
            FileTracker.EndTrackingContext();

            FileTracker.StopTrackingAndCleanup();
            string[] lines = FileTrackerTestHelper.ReadLinesFromFile(tlogWriteFile);
            string[] lines2 = FileTrackerTestHelper.ReadLinesFromFile(tlogWriteFile2);
            Assert.Equal(3, lines.Length);
            Assert.Equal(2, lines2.Length);
            Assert.Equal(Path.GetFullPath(sourceFile).ToUpperInvariant(), lines[1]);
            Assert.Equal(Path.GetFullPath(sourceFile3).ToUpperInvariant(), lines[2]);
            Assert.Equal(Path.GetFullPath(sourceFile2).ToUpperInvariant(), lines2[1]);

            File.Delete(tlogWriteFile);
            File.Delete(tlogWriteFile2);
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void InProcTrackingTwoContextsWithRoot()
        {
            string sourceFile = "inlinetrackingtest.txt";
            string sourceFile2 = "vi\u00FCes\u00E4tato633833475975527668.txt";
            string sourceFile3 = "inlinetrackingtest3.txt";
            string tlogRootName = "foo_inline";
            string tlogWriteFile = $"{tlogRootName}.write.1.tlog";
            string tlogWriteFile2 = $"{tlogRootName}2.write.1.tlog";

            string rootMarker = FileTracker.FormatRootingMarker(new TaskItem(sourceFile2));
            string responseFile = FileTracker.CreateRootingMarkerResponseFile(rootMarker);

            File.Delete(tlogWriteFile);
            File.Delete(tlogWriteFile2);

            try
            {
                // Context 1
                FileTracker.StartTrackingContext(Path.GetFullPath("."), "Context1");
                File.WriteAllText(sourceFile, "this is a inline tracking test");

                // Context 2
                FileTracker.StartTrackingContextWithRoot(Path.GetFullPath("."), "Context2", responseFile);
                File.WriteAllText(sourceFile2, "this is a inline tracking test - in a second context");
                FileTracker.WriteContextTLogs(Path.GetFullPath("."), tlogRootName + "2");
                FileTracker.EndTrackingContext();

                // Back to context 1
                File.WriteAllText(sourceFile3, "this is a second inline tracking test in the first context");
                FileTracker.WriteContextTLogs(Path.GetFullPath("."), tlogRootName);
                FileTracker.EndTrackingContext();

                FileTracker.StopTrackingAndCleanup();
                string[] lines = FileTrackerTestHelper.ReadLinesFromFile(tlogWriteFile);
                string[] lines2 = FileTrackerTestHelper.ReadLinesFromFile(tlogWriteFile2);
                Assert.Equal(3, lines.Length);
                Assert.Equal(3, lines2.Length);
                Assert.Equal(Path.GetFullPath(sourceFile).ToUpperInvariant(), lines[1]);
                Assert.Equal(Path.GetFullPath(sourceFile3).ToUpperInvariant(), lines[2]);
                Assert.Equal("^" + rootMarker, lines2[1]);
                Assert.Equal(rootMarker, lines2[2]);
            }
            finally
            {
                File.Delete(tlogWriteFile);
                File.Delete(tlogWriteFile2);
                File.Delete(responseFile);
            }
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void InProcTrackingSpawnsOutOfProcTool()
        {
            string intermediateDir = Path.GetTempPath() + @"InProcTrackingSpawnsOutOfProcTool\";

            string sourceFile = intermediateDir + @"inlinetracking1.txt";
            string commandFile = intermediateDir + @"command.bat";
            string tlogRootName = "inproc_spawn";
            string tlogWriteFile = intermediateDir + $"{tlogRootName}-cmd.write.1.tlog";
            string rootMarker = @"\\THIS\IS\MY\ROOT|\\IT\IS\COMPOUND\TOO";
            string rootMarkerRsp = intermediateDir + @"rootmarker.rsp";

            if (Directory.Exists(intermediateDir))
            {
                Directory.Delete(intermediateDir, true);
            }

            try
            {
                Directory.CreateDirectory(intermediateDir);

                File.WriteAllText(commandFile, "echo this is out of proc tracking writing stuff > \"" + sourceFile + "\"");
                File.WriteAllText(rootMarkerRsp, "/r " + rootMarker);

                FileTracker.StartTrackingContextWithRoot(intermediateDir, tlogRootName, rootMarkerRsp);

                ProcessStartInfo ps = new ProcessStartInfo("cmd.exe", "/C \"" + commandFile + "\"");
                // Clear out all environment variables
                Process cmd = Process.Start(ps);
                cmd.WaitForExit();

                FileTracker.StopTrackingAndCleanup();

                string[] lines = FileTrackerTestHelper.ReadLinesFromFile(tlogWriteFile);

                Assert.Equal(3, lines.Length);
                Assert.Equal("^" + rootMarker, lines[1]);
                Assert.Equal(sourceFile.ToUpperInvariant(), lines[2]);
            }
            finally
            {
                if (Directory.Exists(intermediateDir))
                {
                    Directory.Delete(intermediateDir, true);
                }
            }
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void InProcTrackingSpawnsOutOfProcTool_OverrideEnvironment()
        {
            string intermediateDir = Path.GetTempPath() + @"InProcTrackingSpawnsOutOfProcTool_OverrideEnvironment\";

            string sourceFile = intermediateDir + @"inlinetracking1.txt";
            string commandFile = intermediateDir + @"command.bat";
            string tlogRootName = "inproc_spawn_env";
            string tlogWriteFile = intermediateDir + $"{tlogRootName}-cmd.write.1.tlog";
            string rootMarker = @"\\THIS\IS\MY\ROOT|\\IT\IS\COMPOUND\TOO";
            string rootMarkerRsp = intermediateDir + @"rootmarker.rsp";

            if (Directory.Exists(intermediateDir))
            {
                Directory.Delete(intermediateDir, true);
            }

            try
            {
                Directory.CreateDirectory(intermediateDir);

                File.WriteAllText(commandFile, "echo this is out of proc tracking writing stuff > \"" + sourceFile + "\"");
                File.WriteAllText(rootMarkerRsp, "/r " + rootMarker);

                FileTracker.StartTrackingContextWithRoot(intermediateDir, tlogRootName, rootMarkerRsp);

                ProcessStartInfo ps = new ProcessStartInfo("cmd.exe", "/C \"" + commandFile + "\"");
                ps.EnvironmentVariables["TRACKER_TOOLCHAIN"] = "MSBuild";
                ps.UseShellExecute = false;

                Process cmd = Process.Start(ps);
                cmd.WaitForExit();

                FileTracker.StopTrackingAndCleanup();

                string[] lines = FileTrackerTestHelper.ReadLinesFromFile(tlogWriteFile);

                Assert.Equal(3, lines.Length);
                Assert.Equal("^" + rootMarker, lines[1]);
                Assert.Equal(sourceFile.ToUpperInvariant(), lines[2]);
            }
            finally
            {
                if (Directory.Exists(intermediateDir))
                {
                    Directory.Delete(intermediateDir, true);
                }
            }
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void InProcTrackingSpawnsToolWithTrackerResponseFile()
        {
            Console.WriteLine("Test: InProcTrackingSpawnsToolWithTrackerResponseFile");

            InProcTrackingSpawnsToolWithTracker(true);
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void InProcTrackingSpawnsToolWithTrackerNoResponseFile()
        {
            Console.WriteLine("Test: InProcTrackingSpawnsToolWithTrackerNoResponseFile");

            InProcTrackingSpawnsToolWithTracker(false);
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void InProcTrackingTwoContextsTwoEnds()
        {
            Assert.Throws<COMException>(() =>
            {
                string sourceFile = "inlinetrackingtest.txt";
                string sourceFile2 = "inlinetrackingtest2.txt";
                string tlogRootName = "foo_inline";
                string tlogWriteFile = $"{tlogRootName}.write.1.tlog";
                string tlogWriteFile2 = $"{tlogRootName}2.write.1.tlog";

                try
                {
                    File.Delete(tlogWriteFile);
                    File.Delete(tlogWriteFile2);

                    // Context 1
                    FileTracker.StartTrackingContext(Path.GetFullPath("."), "Context1");
                    File.WriteAllText(sourceFile, "this is a inline tracking test");

                    // Context 2
                    FileTracker.StartTrackingContext(Path.GetFullPath("."), "Context2");
                    File.WriteAllText(sourceFile2, "this is a inline tracking test - in a second context");
                    FileTracker.WriteContextTLogs(Path.GetFullPath("."), tlogRootName + "2");
                    FileTracker.EndTrackingContext();
                    // This will cause the outer context to end which will mean there is nothing in the context for the write
                    FileTracker.EndTrackingContext();

                    // There is nothing in the context to write from, we should get an exception here:
                    FileTracker.WriteContextTLogs(Path.GetFullPath("."), tlogRootName);
                    FileTracker.EndTrackingContext();
                }
                finally
                {
                    FileTracker.StopTrackingAndCleanup();

                    File.Delete(tlogWriteFile);
                    File.Delete(tlogWriteFile2);
                }
            }
           );
        }

        [Fact(Skip = "Test fails in xunit because tracker includes the PID in the log file.")]
        public void InProcTrackingStartProcessFindStrIn()
        {
            Console.WriteLine("Test: InProcTrackingStartProcessFindStrIn");
            int exit;

            try
            {
                File.Delete("findstr.read.1.tlog");
                File.Delete("InProcTrackingStartProcessFindStrIn-findstr.read.1.tlog");
                FileTrackerTestHelper.WriteAll("test.in", "foo");

                FileTracker.StartTrackingContext(Path.GetFullPath("."), "InProcTrackingStartProcessFindStrIn");
                exit = FileTrackerTestHelper.RunCommand("findstr", "/ip foo test.in");
                FileTracker.WriteContextTLogs(Path.GetFullPath("."), "inlinefind");
                FileTracker.EndTrackingContext();
            }
            finally
            {
                FileTracker.StopTrackingAndCleanup();
            }
            Console.WriteLine("");
            Assert.Equal(0, exit);
            // This line is the problem.  It seems to have been reliable in MSTest 
            // but in xunit when run with other tests (NOT by itself), filetracker
            // puts a PID in the path, so this tries to open the wrong file and throws.
            FileTrackerTestHelper.AssertFoundStringInTLog(Path.GetFullPath("test.in").ToUpperInvariant(), "InProcTrackingStartProcessFindStrIn-findstr.read.1.tlog");
            File.Delete("findstr.read.1.tlog");
            File.Delete("InProcTrackingStartProcessFindStrIn-findstr.read.1.tlog");
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void InProcTrackingStartProcessFindStrNullCommandLine()
        {
            Console.WriteLine("Test: InProcTrackingStartProcessFindStrNullCommandLine");

            try
            {
                FileTracker.StartTrackingContext(Path.GetFullPath("."), "InProcTrackingStartProcessFindStrIn");
                BackEndNativeMethods.STARTUP_INFO startInfo = new BackEndNativeMethods.STARTUP_INFO();
                startInfo.cb = Marshal.SizeOf<BackEndNativeMethods.STARTUP_INFO>();
                uint dwCreationFlags = BackEndNativeMethods.NORMALPRIORITYCLASS;

                startInfo.hStdError = BackEndNativeMethods.InvalidHandle;
                startInfo.hStdInput = BackEndNativeMethods.InvalidHandle;
                startInfo.hStdOutput = BackEndNativeMethods.InvalidHandle;
                startInfo.dwFlags = BackEndNativeMethods.STARTFUSESTDHANDLES;
                dwCreationFlags = dwCreationFlags | BackEndNativeMethods.CREATENOWINDOW;

                BackEndNativeMethods.SECURITY_ATTRIBUTES pSec = new BackEndNativeMethods.SECURITY_ATTRIBUTES();
                BackEndNativeMethods.SECURITY_ATTRIBUTES tSec = new BackEndNativeMethods.SECURITY_ATTRIBUTES();
                pSec.nLength = Marshal.SizeOf<BackEndNativeMethods.SECURITY_ATTRIBUTES>();
                tSec.nLength = Marshal.SizeOf<BackEndNativeMethods.SECURITY_ATTRIBUTES>();

                BackEndNativeMethods.PROCESS_INFORMATION pInfo = new BackEndNativeMethods.PROCESS_INFORMATION();

                string appName = Path.Combine(Environment.SystemDirectory, "findstr.exe");

                Assert.True(File.Exists(appName));

                string cmdLine = null;
                bool created = BackEndNativeMethods.CreateProcess(appName, cmdLine,
                                            ref pSec, ref tSec,
                                            false, dwCreationFlags,
                                            BackEndNativeMethods.NullPtr, null, ref startInfo, out pInfo);

                // We should have correctly started the process even though the command-line was null
                Assert.True(created);

                FileTracker.WriteContextTLogs(Path.GetFullPath("."), "inlinefind");
                FileTracker.EndTrackingContext();
            }
            finally
            {
                FileTracker.StopTrackingAndCleanup();
            }
            File.Delete("inlinefind.read.1.tlog");
        }


        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void InProcTrackingStartProcessFindStrInDefaultTaskName()
        {
            Console.WriteLine("Test: InProcTrackingStartProcessFindStrInDefaultTaskName");
            int exit = 0;

            try
            {
                File.Delete("findstr.read.1.tlog");
                File.Delete("InProcTrackingStartProcessFindStrIn-findstr.read.1.tlog");
                FileTrackerTestHelper.WriteAll("test.in", "foo");

                FileTracker.StartTrackingContext(Path.GetFullPath("."), "");
                exit = FileTrackerTestHelper.RunCommand("findstr", "/ip foo test.in");
                FileTracker.EndTrackingContext();
            }
            finally
            {
                FileTracker.StopTrackingAndCleanup();
            }

            Console.WriteLine("");
            Assert.Equal(0, exit);
            FileTrackerTestHelper.AssertFoundStringInTLog(Path.GetFullPath("test.in").ToUpperInvariant(), "findstr.read.1.tlog");

            File.Delete("findstr.read.1.tlog");
            File.Delete("InProcTrackingStartProcessFindStrIn-findstr.read.1.tlog");
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void InProcTrackingChildThreadTrackedAuto()
        {
            FileTracker.SetThreadCount(1);
            string sourceFile = "inlinetrackingtest.txt";
            string tlogRootName = "foo_inline_parent";
            string tlogChildRootName = "InProcTrackingChildThreadTrackedAuto";
            string tlogWriteFile = $"{tlogRootName}.write.1.tlog";
            string tlogChildWriteFile = $"{tlogChildRootName}.write.2.tlog";

            File.Delete(tlogWriteFile);
            File.Delete(tlogChildWriteFile);
            File.Delete(sourceFile);

            FileTracker.StartTrackingContext(Path.GetFullPath("."), "InProcTrackingChildThreadTrackedAuto");

            File.WriteAllText(sourceFile, "parent thread\r\n");

            Thread t = new Thread(ThreadProcAutoTLog);
            t.Start();
            t.Join(); // wait for our child to complete

            FileTracker.WriteContextTLogs(Path.GetFullPath("."), tlogRootName); // parent will write an explicit tlog

            FileTracker.StopTrackingAndCleanup();
            string[] writtenlines = FileTrackerTestHelper.ReadLinesFromFile(sourceFile);
            string[] lines = FileTrackerTestHelper.ReadLinesFromFile(tlogWriteFile);
            string[] childLines = FileTrackerTestHelper.ReadLinesFromFile(tlogChildWriteFile);
            Assert.Equal(2, lines.Length);
            Assert.Equal(2, childLines.Length);
            Assert.Equal(2, writtenlines.Length);
            Assert.Equal(Path.GetFullPath(sourceFile).ToUpperInvariant(), lines[1]);
            Assert.Equal(Path.GetFullPath(sourceFile).ToUpperInvariant(), childLines[1]);
            Assert.Equal("parent thread", writtenlines[0]);
            Assert.Equal("child thread", writtenlines[1]);

            File.Delete(tlogWriteFile);
            File.Delete(tlogChildWriteFile);
            File.Delete(sourceFile);
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void InProcTrackingChildThreadTrackedManual()
        {
            FileTracker.SetThreadCount(1);
            string sourceFile = "inlinetrackingtest.txt";
            string tlogRootName = "foo_inline_parent";
            string tlogChildRootName = "foo_inline_child";
            string tlogWriteFile = $"{tlogRootName}.write.1.tlog";
            string tlogChildWriteFile = $"{tlogChildRootName}.write.2.tlog";

            File.Delete(tlogWriteFile);

            FileTracker.StartTrackingContext(Path.GetFullPath("."), "InProcTrackingChildThreadTrackedAuto");

            File.WriteAllText(sourceFile, "parent thread\r\n");

            Thread t = new Thread(ThreadProcManualTLog);
            t.Start();
            t.Join(); // wait for our child to complete

            FileTracker.WriteContextTLogs(Path.GetFullPath("."), tlogRootName); // parent will write an explicit tlog

            FileTracker.StopTrackingAndCleanup();
            string[] writtenlines = FileTrackerTestHelper.ReadLinesFromFile(sourceFile);
            string[] lines = FileTrackerTestHelper.ReadLinesFromFile(tlogWriteFile);
            string[] childLines = FileTrackerTestHelper.ReadLinesFromFile(tlogChildWriteFile);
            Assert.Equal(2, lines.Length);
            Assert.Equal(2, childLines.Length);
            Assert.Equal(2, writtenlines.Length);
            Assert.Equal(Path.GetFullPath(sourceFile).ToUpperInvariant(), lines[1]);
            Assert.Equal(Path.GetFullPath(sourceFile).ToUpperInvariant(), childLines[1]);
            Assert.Equal("parent thread", writtenlines[0]);
            Assert.Equal("child thread", writtenlines[1]);

            File.Delete(tlogWriteFile);
            File.Delete(tlogChildWriteFile);
            File.Delete(sourceFile);
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void InProcTrackingChildThreadNotTracked()
        {
            FileTracker.SetThreadCount(1);
            string sourceFile = "inlinetrackingtest.txt";
            string tlogRootName = "foo_inline_parent";
            string tlogChildRootName = "ThreadProcTrackedAutoTLog";
            string tlogWriteFile = $"{tlogRootName}.write.1.tlog";
            string tlogChildWriteFile = $"{tlogChildRootName}.write.2.tlog";

            File.Delete(tlogWriteFile);

            FileTracker.StartTrackingContext(Path.GetFullPath("."), "InProcTrackingChildThreadTrackedAuto");
            FileTracker.SuspendTracking();

            File.WriteAllText(sourceFile, "parent thread\r\n");

            Thread t = new Thread(ThreadProcAutoTLog);
            t.Start();
            t.Join(); // wait for our child to complete

            FileTracker.WriteContextTLogs(Path.GetFullPath("."), tlogRootName); // parent will write an explicit tlog

            FileTracker.StopTrackingAndCleanup();
            Assert.False(File.Exists(tlogWriteFile));
            Assert.False(File.Exists(tlogChildRootName));
            string[] writtenlines = FileTrackerTestHelper.ReadLinesFromFile(sourceFile);
            Assert.Equal(2, writtenlines.Length);
            Assert.Equal("parent thread", writtenlines[0]);
            Assert.Equal("child thread", writtenlines[1]);

            File.Delete(tlogWriteFile);
            File.Delete(tlogChildWriteFile);
            File.Delete(sourceFile);
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void InProcTrackingChildThreadNotTrackedLocallyTracked()
        {
            FileTracker.SetThreadCount(1);
            string sourceFile = "inlinetrackingtest.txt";
            string tlogRootName = "foo_inline_parent";
            string tlogChildRootName = "ThreadProcLocallyTracked";
            string tlogWriteFile = $"{tlogRootName}.write.1.tlog";
            string tlogChildWriteFile = $"{tlogChildRootName}.write.2.tlog";

            File.Delete(tlogWriteFile);

            FileTracker.StartTrackingContext(Path.GetFullPath("."), "InProcTrackingChildThreadNotTrackedLocallyTracked");
            FileTracker.SuspendTracking();

            File.WriteAllText(sourceFile, "parent thread\r\n");

            Thread t = new Thread(ThreadProcLocallyTracked);
            t.Start();
            t.Join(); // wait for our child to complete

            FileTracker.WriteContextTLogs(Path.GetFullPath("."), tlogRootName); // parent will write an explicit tlog

            FileTracker.StopTrackingAndCleanup();
            Assert.False(File.Exists(tlogWriteFile));
            string[] writtenlines = FileTrackerTestHelper.ReadLinesFromFile(sourceFile);
            string[] childLines = FileTrackerTestHelper.ReadLinesFromFile(tlogChildWriteFile);
            Assert.Equal(2, childLines.Length);
            Assert.Equal(2, writtenlines.Length);
            Assert.Equal(Path.GetFullPath(sourceFile).ToUpperInvariant(), childLines[1]);
            Assert.Equal("parent thread", writtenlines[0]);
            Assert.Equal("child thread", writtenlines[1]);

            File.Delete(tlogWriteFile);
            File.Delete(tlogChildWriteFile);
            File.Delete(sourceFile);
        }

        private static void ThreadProcLocallyTracked()
        {
            FileTracker.StartTrackingContext(Path.GetFullPath("."), "ThreadProcLocallyTracked");
            string sourceFile = "inlinetrackingtest.txt";
            File.AppendAllText(sourceFile, "child thread\r\n");
            FileTracker.WriteContextTLogs(Path.GetFullPath("."), "ThreadProcLocallyTracked"); // will write an explicit tlog
            FileTracker.EndTrackingContext();
        }

        private static void ThreadProcAutoTLog()
        {
            string sourceFile = "inlinetrackingtest.txt";
            File.AppendAllText(sourceFile, "child thread\r\n");
        }

        private static void ThreadProcManualTLog()
        {
            string tlogRootName = "foo_inline_child";
            string sourceFile = "inlinetrackingtest.txt";
            File.AppendAllText(sourceFile, "child thread\r\n");
            FileTracker.WriteContextTLogs(Path.GetFullPath("."), tlogRootName);
        }


        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void InProcTrackingChildCustomEnvironment()
        {
            string sourceFile = "allenvironment.txt";
            string commandFile = "inlinetrackingtest.cmd";
            string tlogRootName = "CustomEnvironment";
            string tlogReadFile = $"{tlogRootName}-cmd.read.1.tlog";
            string tlogWriteFile = $"{tlogRootName}-cmd.write.1.tlog";
            File.Delete(tlogWriteFile);

            File.WriteAllText(commandFile, "SET > " + sourceFile);

            FileTracker.StartTrackingContext(Path.GetFullPath("."), tlogRootName);

            ProcessStartInfo ps = new ProcessStartInfo("cmd.exe", "/C " + commandFile);

            ps.EnvironmentVariables.Add("TESTVAR", "THE_RIGHT_VALUE");
            ps.UseShellExecute = false;

            Process cmd = Process.Start(ps);
            cmd.WaitForExit();

            FileTracker.StopTrackingAndCleanup();

            // Read in the environment file and check that the variable that we set is there
            string[] envLines = File.ReadAllLines(sourceFile);
            int trackerEnvValueCount = 0;

            string varValue = null;
            string toolChainValue = null;
            foreach (string envLine in envLines)
            {
                if (envLine.StartsWith("TRACKER_", StringComparison.OrdinalIgnoreCase))
                {
                    trackerEnvValueCount++;
                }

                if (envLine.StartsWith("TESTVAR=", StringComparison.OrdinalIgnoreCase) && varValue == null)
                {
                    string[] varVal = envLine.Split(MSBuildConstants.EqualsChar);
                    varValue = varVal[1];
                }
                else if (envLine.StartsWith("TRACKER_TOOLCHAIN=", StringComparison.OrdinalIgnoreCase) && toolChainValue == null)
                {
                    string[] varVal = envLine.Split(MSBuildConstants.EqualsChar);
                    toolChainValue = varVal[1];
                }
            }

            Assert.True(trackerEnvValueCount >= 7); // "Not enough tracking environment set"
            Assert.Equal("THE_RIGHT_VALUE", varValue);
            Assert.Equal(tlogRootName + "-cmd", toolChainValue);
            string[] writeLines = FileTrackerTestHelper.ReadLinesFromFile(tlogWriteFile);
            string[] readLines = FileTrackerTestHelper.ReadLinesFromFile(tlogReadFile);
            Assert.Equal(2, writeLines.Length);
            Assert.Equal(2, readLines.Length);
            Assert.Equal(Path.GetFullPath(commandFile).ToUpperInvariant(), readLines[1]);
            Assert.Equal(Path.GetFullPath(sourceFile).ToUpperInvariant(), writeLines[1]);

            File.Delete(tlogReadFile);
            File.Delete(tlogWriteFile);
            File.Delete(sourceFile);
            File.Delete(commandFile);
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void CreateFileDoesntRecordWriteIfNotWrittenTo()
        {
            string testDir = Path.Combine(Path.GetTempPath(), "CreateFileDoesntRecordWriteIfNotWrittenTo");
            string readFile = Path.Combine(testDir, "readfile.txt");
            string tlogRootName = "CreateFileRead";

            if (Directory.Exists(testDir))
            {
                Directory.Delete(testDir, true /* recursive */);
            }

            try
            {
                Directory.CreateDirectory(testDir);
                File.WriteAllText(readFile, "this is some sample text that doesn't really matter");

                // wait a bit to give the timestamps time to settle
                Thread.Sleep(100);

                FileTracker.StartTrackingContext(testDir, tlogRootName);

                var buffer = new byte[10];
                using (FileStream fs = File.Open(readFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    fs.Read(buffer, 0, 10);
                }

                FileTracker.WriteContextTLogs(testDir, tlogRootName);

                FileTrackerTestHelper.AssertFoundStringInTLog(readFile.ToUpperInvariant(), Path.Combine(testDir, tlogRootName + ".read.1.tlog"));
                FileTrackerTestHelper.AssertDidntFindStringInTLog(readFile.ToUpperInvariant(), Path.Combine(testDir, tlogRootName + ".write.1.tlog"));
            }
            finally
            {
                FileTracker.StopTrackingAndCleanup();
                if (Directory.Exists(testDir))
                {
                    Directory.Delete(testDir, true /* recursive */);
                }
            }
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void CopyAlwaysRecordsWrites()
        {
            string testDir = Path.Combine(Path.GetTempPath(), "CopyAlwaysRecordsWrites");
            string tlogRootName = "CopyFileTest";
            string copyFromFile = Path.Combine(testDir, "copyFrom.txt");
            string copyToFile = Path.Combine(testDir, "copyTo.txt");
            string tlogReadFile = Path.Combine(testDir, tlogRootName + ".read.1.tlog");
            string tlogWriteFile = Path.Combine(testDir, tlogRootName + ".write.1.tlog");

            if (Directory.Exists(testDir))
            {
                Directory.Delete(testDir, true /* recursive */);
            }

            try
            {
                Directory.CreateDirectory(testDir);

                try
                {
                    File.WriteAllText(copyFromFile, "text in the file!");

                    FileTracker.StartTrackingContext(testDir, tlogRootName);

                    File.Copy(copyFromFile, copyToFile);

                    FileTracker.WriteContextTLogs(testDir, tlogRootName);

                    FileTrackerTestHelper.AssertFoundStringInTLog(copyFromFile.ToUpperInvariant(), tlogReadFile);
                    FileTrackerTestHelper.AssertFoundStringInTLog(copyToFile.ToUpperInvariant(), tlogWriteFile);
                }
                finally
                {
                    File.Delete(tlogReadFile);
                    File.Delete(tlogWriteFile);
                    FileTracker.StopTrackingAndCleanup();
                }

                // wait a bit to give the timestamps time to settle
                Thread.Sleep(100);

                try
                {
                    File.Delete(copyToFile);

                    FileTracker.StartTrackingContext(testDir, tlogRootName);

                    File.Copy(copyFromFile, copyToFile);

                    FileTracker.WriteContextTLogs(testDir, tlogRootName);

                    FileTrackerTestHelper.AssertFoundStringInTLog(copyFromFile.ToUpperInvariant(), tlogReadFile);
                    FileTrackerTestHelper.AssertFoundStringInTLog(copyToFile.ToUpperInvariant(), tlogWriteFile);
                }
                finally
                {
                    FileTracker.StopTrackingAndCleanup();
                }
            }
            finally
            {
                if (Directory.Exists(testDir))
                {
                    Directory.Delete(testDir, true /* recursive */);
                }
            }
        }

        [Fact(Skip = "Needs investigation")]
        public void MoveAlwaysRecordsWrites()
        {
            string testDir = Path.Combine(Path.GetTempPath(), "MoveAlwaysRecordsWrites");
            string tlogRootName = "MoveFileTest";
            string moveFromFile = Path.Combine(testDir, "MoveFrom.txt");
            string moveToFile = Path.Combine(testDir, "MoveTo.txt");
            string moveToFile2 = Path.Combine(testDir, "MoveTo2.txt");
            string tlogDeleteFile = Path.Combine(testDir, tlogRootName + ".delete.1.tlog");
            string tlogWriteFile = Path.Combine(testDir, tlogRootName + ".write.1.tlog");

            if (Directory.Exists(testDir))
            {
                Directory.Delete(testDir, true /* recursive */);
            }

            try
            {
                Directory.CreateDirectory(testDir);

                try
                {
                    File.WriteAllText(moveFromFile, "text in the file!");

                    FileTracker.StartTrackingContext(testDir, tlogRootName);

                    File.Move(moveFromFile, moveToFile);

                    FileTracker.WriteContextTLogs(testDir, tlogRootName);

                    FileTrackerTestHelper.AssertFoundStringInTLog(moveFromFile.ToUpperInvariant(), tlogDeleteFile);
                    FileTrackerTestHelper.AssertFoundStringInTLog(moveToFile.ToUpperInvariant(), tlogWriteFile);
                }
                finally
                {
                    File.Delete(tlogDeleteFile);
                    File.Delete(tlogWriteFile);
                    FileTracker.StopTrackingAndCleanup();
                }

                // wait a bit to give the timestamps time to settle
                Thread.Sleep(100);

                try
                {
                    File.WriteAllText(moveFromFile, "text in the file!");
                    File.Delete(moveToFile);

                    FileTracker.StartTrackingContext(testDir, tlogRootName);

                    File.Move(moveFromFile, moveToFile);
                    File.Move(moveToFile, moveToFile2);

                    FileTracker.WriteContextTLogs(testDir, tlogRootName);

                    FileTrackerTestHelper.AssertFoundStringInTLog(moveFromFile.ToUpperInvariant(), tlogDeleteFile);
                    FileTrackerTestHelper.AssertFoundStringInTLog(moveToFile.ToUpperInvariant(), tlogDeleteFile);
                    FileTrackerTestHelper.AssertFoundStringInTLog(moveToFile2.ToUpperInvariant(), tlogWriteFile);
                }
                finally
                {
                    FileTracker.StopTrackingAndCleanup();
                }
            }
            finally
            {
                if (Directory.Exists(testDir))
                {
                    Directory.Delete(testDir, true /* recursive */);
                }
            }
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void LaunchMultipleOfSameTool_SameCommand()
        {
            string testDir = Path.Combine(Path.GetTempPath(), "LaunchMultipleOfSameTool_SameCommand");
            FileUtilities.DeleteDirectoryNoThrow(testDir, true);

            try
            {
                Directory.CreateDirectory(testDir);

                string originalFindstrPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86), "findstr.exe");
                string destinationFindstrPath = Path.Combine(testDir, "abc.exe");
                File.Copy(originalFindstrPath, destinationFindstrPath);

                string tempFilePath = Path.Combine(testDir, "bar.txt");
                File.WriteAllText(tempFilePath, "foo baz");

                // Item1: appname
                // Item2: command line
                // Item3: number of times to launch
                IList<Tuple<string, string, int>> toolsToLaunch = new List<Tuple<string, string, int>>();
                toolsToLaunch.Add(new Tuple<string, string, int>(destinationFindstrPath, "/i baz " + tempFilePath, 3));

                // Item1: FileTracker context name
                // Item2: Tuple <string, string, int> as described above
                IList<Tuple<string, IList<Tuple<string, string, int>>>> contextSpecifications = new List<Tuple<string, IList<Tuple<string, string, int>>>>();
                contextSpecifications.Add(new Tuple<string, IList<Tuple<string, string, int>>>("ProcessLaunchTest", toolsToLaunch));

                // Item1: tlog pattern
                // Item2: # times it's expected to appear
                IList<Tuple<string, int>> tlogPatterns = new List<Tuple<string, int>>();
                tlogPatterns.Add(new Tuple<string, int>("ProcessLaunchTest-abc*tlog", 3));

                LaunchDuplicateToolsAndVerifyTlogExistsForEach(testDir, contextSpecifications, tlogPatterns, createTestDirectory: false);
            }
            finally
            {
                FileUtilities.DeleteDirectoryNoThrow(testDir, true);
            }
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void LaunchMultipleOfSameTool_DifferentCommands1()
        {
            string testDir = Path.Combine(Path.GetTempPath(), "LaunchMultipleOfSameTool_DifferentCommands1");
            FileUtilities.DeleteDirectoryNoThrow(testDir, true);

            try
            {
                Directory.CreateDirectory(testDir);
                string originalFindstrPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86), "findstr.exe");
                string destinationFindstrPath = Path.Combine(testDir, "abc.exe");
                File.Copy(originalFindstrPath, destinationFindstrPath);

                string tempFilePath = Path.Combine(testDir, "bar.txt");
                File.WriteAllText(tempFilePath, "foo baz");

                // Item1: appname
                // Item2: command line
                // Item3: number of times to launch
                IList<Tuple<string, string, int>> toolsToLaunch = new List<Tuple<string, string, int>>();
                toolsToLaunch.Add(new Tuple<string, string, int>(destinationFindstrPath, "/i foo " + tempFilePath, 3));
                toolsToLaunch.Add(new Tuple<string, string, int>(null, "\"" + destinationFindstrPath + "\" /i baz " + tempFilePath, 3));

                // Item1: FileTracker context name
                // Item2: Tuple <string, string, int> as described above
                IList<Tuple<string, IList<Tuple<string, string, int>>>> contextSpecifications = new List<Tuple<string, IList<Tuple<string, string, int>>>>();
                contextSpecifications.Add(new Tuple<string, IList<Tuple<string, string, int>>>("ProcessLaunchTest", toolsToLaunch));

                // Item1: tlog pattern
                // Item2: # times it's expected to appear
                IList<Tuple<string, int>> tlogPatterns = new List<Tuple<string, int>>();
                tlogPatterns.Add(new Tuple<string, int>("ProcessLaunchTest-abc*tlog", 6));

                LaunchDuplicateToolsAndVerifyTlogExistsForEach(testDir, contextSpecifications, tlogPatterns, createTestDirectory: false);
            }
            finally
            {
                FileUtilities.DeleteDirectoryNoThrow(testDir, true);
            }
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void LaunchMultipleOfSameTool_DifferentCommands2()
        {
            string testDir = Path.Combine(Path.GetTempPath(), "LaunchMultipleOfSameTool_DifferentCommands2");

            try
            {
                if (FileUtilities.DirectoryExistsNoThrow(testDir))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDir, true);
                }

                Directory.CreateDirectory(testDir);

                string tempFilePath = Path.Combine(testDir, "bar.txt");
                File.WriteAllText(tempFilePath, "");

                string originalFindstrPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86), "findstr.exe");
                string destinationFindstrPath = Path.Combine(testDir, "abc.exe");
                File.Copy(originalFindstrPath, destinationFindstrPath);

                // Item1: appname
                // Item2: command line
                // Item3: number of times to launch
                IList<Tuple<string, string, int>> toolsToLaunch = new List<Tuple<string, string, int>>();
                toolsToLaunch.Add(new Tuple<string, string, int>(destinationFindstrPath, "/i baz " + tempFilePath, 3));
                toolsToLaunch.Add(new Tuple<string, string, int>(null, "\"" + destinationFindstrPath + "\" /i foo " + tempFilePath, 2));
                toolsToLaunch.Add(new Tuple<string, string, int>(null, "\"" + destinationFindstrPath + "\" /i ba " + tempFilePath, 2));

                // Item1: FileTracker context name
                // Item2: Tuple <string, string, int> as described above
                IList<Tuple<string, IList<Tuple<string, string, int>>>> contextSpecifications = new List<Tuple<string, IList<Tuple<string, string, int>>>>();
                contextSpecifications.Add(new Tuple<string, IList<Tuple<string, string, int>>>("ProcessLaunchTest", toolsToLaunch));

                // Item1: tlog pattern
                // Item2: # times it's expected to appear
                IList<Tuple<string, int>> tlogPatterns = new List<Tuple<string, int>>();
                tlogPatterns.Add(new Tuple<string, int>("ProcessLaunchTest-abc*tlog", 7));

                LaunchDuplicateToolsAndVerifyTlogExistsForEach(testDir, contextSpecifications, tlogPatterns, createTestDirectory: false);
            }
            finally
            {
                if (FileUtilities.DirectoryExistsNoThrow(testDir))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDir, true);
                }
            }
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void LaunchMultipleOfSameTool_DifferentCommands3()
        {
            string testDir = Path.Combine(Path.GetTempPath(), "LaunchMultipleOfSameTool_DifferentCommands3");
            string oldCurrentDirectory = Directory.GetCurrentDirectory();
            try
            {
                if (FileUtilities.DirectoryExistsNoThrow(testDir))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDir, true);
                }

                Directory.CreateDirectory(testDir);

                string originalFindstrPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86), "findstr.exe");
                string destinationFindstrPath = Path.Combine(testDir, "findstr.exe");
                File.Copy(originalFindstrPath, destinationFindstrPath);

                string tempFilePath = Path.Combine(testDir, "bar.txt");
                File.WriteAllText(tempFilePath, "foo baz");

                // Item1: appname
                // Item2: command line
                // Item3: number of times to launch
                IList<Tuple<string, string, int>> toolsToLaunch = new List<Tuple<string, string, int>>();
                toolsToLaunch.Add(new Tuple<string, string, int>(originalFindstrPath, "/i foo " + tempFilePath, 3));
                toolsToLaunch.Add(new Tuple<string, string, int>(destinationFindstrPath, "/i baz " + tempFilePath, 3));
                toolsToLaunch.Add(new Tuple<string, string, int>(null, "FIndsTr /i ba " + tempFilePath, 2));

                // Item1: FileTracker context name
                // Item2: Tuple <string, string, int> as described above
                IList<Tuple<string, IList<Tuple<string, string, int>>>> contextSpecifications = new List<Tuple<string, IList<Tuple<string, string, int>>>>();
                contextSpecifications.Add(new Tuple<string, IList<Tuple<string, string, int>>>("ProcessLaunchTest", toolsToLaunch));

                // Item1: tlog pattern
                // Item2: # times it's expected to appear
                IList<Tuple<string, int>> tlogPatterns = new List<Tuple<string, int>>();
                tlogPatterns.Add(new Tuple<string, int>("ProcessLaunchTest-findstr*tlog", 8));

                LaunchDuplicateToolsAndVerifyTlogExistsForEach(testDir, contextSpecifications, tlogPatterns, createTestDirectory: false);
            }
            finally
            {
                Directory.SetCurrentDirectory(oldCurrentDirectory);

                if (FileUtilities.DirectoryExistsNoThrow(testDir))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDir, true);
                }
            }
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void LaunchMultipleOfSameTool_DifferentCommands4()
        {
            string testDir = Path.Combine(Path.GetTempPath(), "LaunchMultipleOfSameTool_DifferentCommands4");
            string oldPath = Environment.GetEnvironmentVariable("PATH");

            try
            {
                if (FileUtilities.DirectoryExistsNoThrow(testDir))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDir, true);
                }

                Directory.CreateDirectory(testDir);

                string tempFilePath = Path.Combine(testDir, "bar.txt");
                File.WriteAllText(tempFilePath, "");

                string originalFindstrPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86), "findstr.exe");
                string destinationFindstrPath = Path.Combine(testDir, "abc.exe");
                File.Copy(originalFindstrPath, destinationFindstrPath);

                Environment.SetEnvironmentVariable("PATH", Path.GetDirectoryName(destinationFindstrPath) + ";" + oldPath);

                // Item1: appname
                // Item2: command line
                // Item3: number of times to launch
                IList<Tuple<string, string, int>> toolsToLaunch = new List<Tuple<string, string, int>>();
                toolsToLaunch.Add(new Tuple<string, string, int>(destinationFindstrPath, "/ip oo " + tempFilePath, 3));
                toolsToLaunch.Add(new Tuple<string, string, int>(null, "abc.exe /i foo " + tempFilePath, 3));

                // Item1: FileTracker context name
                // Item2: Tuple <string, string, int> as described above
                IList<Tuple<string, IList<Tuple<string, string, int>>>> contextSpecifications = new List<Tuple<string, IList<Tuple<string, string, int>>>>();
                contextSpecifications.Add(new Tuple<string, IList<Tuple<string, string, int>>>("ProcessLaunchTest", toolsToLaunch));

                // Item1: tlog pattern
                // Item2: # times it's expected to appear
                IList<Tuple<string, int>> tlogPatterns = new List<Tuple<string, int>>();
                tlogPatterns.Add(new Tuple<string, int>("ProcessLaunchTest-abc*tlog", 6));

                LaunchDuplicateToolsAndVerifyTlogExistsForEach(testDir, contextSpecifications, tlogPatterns, createTestDirectory: false);
            }
            finally
            {
                Environment.SetEnvironmentVariable("PATH", oldPath);

                if (FileUtilities.DirectoryExistsNoThrow(testDir))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDir, true);
                }
            }
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void LaunchMultipleDifferentTools()
        {
            string testDir = Path.Combine(Path.GetTempPath(), "LaunchMultipleDifferentTools");

            try
            {
                if (FileUtilities.DirectoryExistsNoThrow(testDir))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDir, true);
                }

                Directory.CreateDirectory(testDir);
                string tempFilePath = Path.Combine(testDir, "bar.txt");
                File.WriteAllText(tempFilePath, "");

                string originalFindstrPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86), "findstr.exe");
                string destinationFindstrPath = Path.Combine(testDir, "abc.exe");
                File.Copy(originalFindstrPath, destinationFindstrPath);

                // Item1: appname
                // Item2: command line
                // Item3: number of times to launch
                IList<Tuple<string, string, int>> toolsToLaunch = new List<Tuple<string, string, int>>();
                toolsToLaunch.Add(new Tuple<string, string, int>(destinationFindstrPath, "/i foo " + tempFilePath, 3));
                toolsToLaunch.Add(new Tuple<string, string, int>(null, "findstr /i foo " + tempFilePath, 3));

                // Item1: FileTracker context name
                // Item2: Tuple <string, string, int> as described above
                IList<Tuple<string, IList<Tuple<string, string, int>>>> contextSpecifications = new List<Tuple<string, IList<Tuple<string, string, int>>>>();
                contextSpecifications.Add(new Tuple<string, IList<Tuple<string, string, int>>>("ProcessLaunchTest", toolsToLaunch));

                // Item1: tlog pattern
                // Item2: # times it's expected to appear
                IList<Tuple<string, int>> tlogPatterns = new List<Tuple<string, int>>();
                tlogPatterns.Add(new Tuple<string, int>("ProcessLaunchTest-abc*tlog", 3));
                tlogPatterns.Add(new Tuple<string, int>("ProcessLaunchTest-findstr*tlog", 3));

                LaunchDuplicateToolsAndVerifyTlogExistsForEach(testDir, contextSpecifications, tlogPatterns, createTestDirectory: false);
            }
            finally
            {
                if (FileUtilities.DirectoryExistsNoThrow(testDir))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDir, true);
                }
            }
        }

        [Fact(Skip = "FileTracker tests require VS2015 Update 3 or a packaged version of Tracker.exe https://github.com/Microsoft/msbuild/issues/649")]
        public void LaunchMultipleOfSameTool_DifferentContexts()
        {
            string testDir = Path.Combine(Path.GetTempPath(), "LaunchMultipleOfSameTool_DifferentContexts");
            FileUtilities.DeleteDirectoryNoThrow(testDir, true);
            try
            {
                Directory.CreateDirectory(testDir);

                string originalFindstrPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86), "findstr.exe");
                string destinationFindstrPath = Path.Combine(testDir, "abc.exe");
                File.Copy(originalFindstrPath, destinationFindstrPath);

                string tempFilePath = Path.Combine(testDir, "bar.txt");
                File.WriteAllText(tempFilePath, "foo baz");

                // Item1: appname
                // Item2: command line
                // Item3: number of times to launch
                IList<Tuple<string, string, int>> toolsToLaunch = new List<Tuple<string, string, int>>();
                toolsToLaunch.Add(new Tuple<string, string, int>(destinationFindstrPath, "/i baz " + tempFilePath, 3));

                // Item1: FileTracker context name
                // Item2: Tuple <string, string, int> as described above
                IList<Tuple<string, IList<Tuple<string, string, int>>>> contextSpecifications =
                    new List<Tuple<string, IList<Tuple<string, string, int>>>>();
                contextSpecifications.Add(new Tuple<string, IList<Tuple<string, string, int>>>("ProcessLaunchTest", toolsToLaunch));
                contextSpecifications.Add(new Tuple<string, IList<Tuple<string, string, int>>>("ProcessLaunchTest2", toolsToLaunch));

                // Item1: tlog pattern
                // Item2: # times it's expected to appear
                IList<Tuple<string, int>> tlogPatterns = new List<Tuple<string, int>>();
                tlogPatterns.Add(new Tuple<string, int>("ProcessLaunchTest-abc*tlog", 3));
                tlogPatterns.Add(new Tuple<string, int>("ProcessLaunchTest2-abc*tlog", 3));

                LaunchDuplicateToolsAndVerifyTlogExistsForEach(testDir, contextSpecifications, tlogPatterns, false);
            }
            catch (Exception)
            {
                FileUtilities.DeleteDirectoryNoThrow(testDir, true);
            }

        }

        [Fact(Skip = "Needs investigation")]
        public void LaunchMultipleOfSameTool_ToolLaunchesOthers()
        {
            string testDir = Path.Combine(Path.GetTempPath(), "LaunchMultipleOfSameTool_ToolLaunchesOthers");

            try
            {
                if (FileUtilities.DirectoryExistsNoThrow(testDir))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDir, true);
                }

                Directory.CreateDirectory(testDir);

                // File to run findstr against. 
                string tempFilePath = Path.Combine(testDir, "bar.txt");
                File.WriteAllText(tempFilePath, "");

                // Sample app that runs findstr.
                string outputFile = Path.Combine(testDir, "FindstrLauncher.exe");
                string codeContent = @"
using System;
using System.Diagnostics;

namespace ConsoleApplication4
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length > 1)
            {
                for (int i = 0; i < Int32.Parse(args[0]); i++)
                {
                    Process p = Process.Start(""findstr"", ""/i foo "" + args[1]);
                    p.WaitForExit();
                }
            }
        }
    }
}";

                File.Delete(outputFile);

                string codeFile = Path.Combine(testDir, "Program.cs");
                File.WriteAllText(codeFile, codeContent);
                Csc csc = new Csc();
                csc.BuildEngine = new MockEngine();
                csc.Sources = new ITaskItem[] { new TaskItem(codeFile) };
                csc.OutputAssembly = new TaskItem(outputFile);
                csc.Platform = "x86";
                bool compileSucceeded = csc.Execute();

                Assert.True(compileSucceeded);

                // Item1: appname
                // Item2: command line
                // Item3: number of times to launch
                IList<Tuple<string, string, int>> toolsToLaunch = new List<Tuple<string, string, int>>();
                toolsToLaunch.Add(new Tuple<string, string, int>(outputFile, outputFile + " 3 " + tempFilePath, 3));

                // Item1: FileTracker context name
                // Item2: Tuple <string, string, int> as described above
                IList<Tuple<string, IList<Tuple<string, string, int>>>> contextSpecifications = new List<Tuple<string, IList<Tuple<string, string, int>>>>();
                contextSpecifications.Add(new Tuple<string, IList<Tuple<string, string, int>>>("ProcessLaunchTest", toolsToLaunch));
                contextSpecifications.Add(new Tuple<string, IList<Tuple<string, string, int>>>("ProcessLaunchTest2", toolsToLaunch));

                // Item1: tlog pattern
                // Item2: # times it's expected to appear
                IList<Tuple<string, int>> tlogPatterns = new List<Tuple<string, int>>();
                tlogPatterns.Add(new Tuple<string, int>("ProcessLaunchTest-FindstrLauncher-findstr*tlog", 3));
                tlogPatterns.Add(new Tuple<string, int>("ProcessLaunchTest-FindstrLauncher.*-findstr*tlog", 6));
                tlogPatterns.Add(new Tuple<string, int>("ProcessLaunchTest2-FindstrLauncher-findstr*tlog", 3));
                tlogPatterns.Add(new Tuple<string, int>("ProcessLaunchTest2-FindstrLauncher.*-findstr*tlog", 6));

                LaunchDuplicateToolsAndVerifyTlogExistsForEach(testDir, contextSpecifications, tlogPatterns, createTestDirectory: false);
            }
            finally
            {
                if (FileUtilities.DirectoryExistsNoThrow(testDir))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDir, true);
                }
            }
        }

        private static void InProcTrackingSpawnsToolWithTracker(bool useTrackerResponseFile)
        {
            const string testInFile = "test.in";
            const string testInFileContent = "foo";
            const string tool = "findstr";
            const string toolReadTlog = tool + ".read.1.tlog";
            const string rootingMarker = "jibbit goo";
            const string inprocTrackingContext = "Context1";
            const string tlogRootName = "foo_inline";
            const string sourceFile = "inlinetrackingtest.txt";
            const string trackerResponseFile = "test-tracker.rsp";
            const string fileTrackerParameters = "/d FileTracker.dll /r \"" + rootingMarker + "\"";

            File.Delete(toolReadTlog);
            File.Delete(sourceFile);
            FileTrackerTestHelper.WriteAll(testInFile, testInFileContent);
            FileTrackerTestHelper.WriteAll(trackerResponseFile, fileTrackerParameters);

            try
            {
                FileTracker.StartTrackingContext(Path.GetFullPath("."), inprocTrackingContext);
                File.WriteAllText(sourceFile, "this is a inline tracking test");

                string firstParameters = useTrackerResponseFile ? "@\"" + trackerResponseFile + "\"" : fileTrackerParameters;
                int exit = FileTrackerTestHelper.RunCommand(
                    s_defaultTrackerPath,
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "{0} /c {1} /ip {2} {3}",
                        firstParameters,
                        tool,
                        testInFileContent,
                        testInFile));

                Assert.Equal(0, exit);
                Assert.Equal("^" + rootingMarker.ToUpperInvariant(),
                                       FileTrackerTestHelper.ReadLineFromFile(toolReadTlog, 1).ToUpperInvariant());
                FileTrackerTestHelper.AssertFoundStringInTLog(Path.GetFullPath(testInFile).ToUpperInvariant(), toolReadTlog);

                FileTracker.WriteContextTLogs(Path.GetFullPath("."), tlogRootName);
                Assert.Equal(Path.GetFullPath(sourceFile).ToUpperInvariant(),
                                       FileTrackerTestHelper.ReadLineFromFile(tlogRootName + ".write.1.tlog", 1).ToUpperInvariant());
            }
            finally
            {
                File.Delete(trackerResponseFile);
                File.Delete(testInFile);
                File.Delete(sourceFile);
                File.Delete(tlogRootName + ".write.1.tlog");
                File.Delete(tlogRootName + ".read.1.tlog");
                File.Delete(tool + ".read.1.tlog");
                File.Delete(inprocTrackingContext + "-Tracker.read.1.tlog");
                FileTracker.StopTrackingAndCleanup();
            }
        }

        private static void LaunchDuplicateToolsAndVerifyTlogExistsForEach(string tlogPath, IList<Tuple<string, IList<Tuple<string, string, int>>>> contextSpecifications, IList<Tuple<string, int>> tlogPatterns, bool createTestDirectory)
        {
            try
            {
                if (createTestDirectory)
                {
                    if (FileUtilities.DirectoryExistsNoThrow(tlogPath))
                    {
                        FileUtilities.DeleteDirectoryNoThrow(tlogPath, true);
                    }

                    Directory.CreateDirectory(tlogPath);
                }

                BackEndNativeMethods.STARTUP_INFO startInfo = new BackEndNativeMethods.STARTUP_INFO();
                startInfo.cb = Marshal.SizeOf<BackEndNativeMethods.STARTUP_INFO>();
                uint dwCreationFlags = BackEndNativeMethods.NORMALPRIORITYCLASS;

                startInfo.hStdError = BackEndNativeMethods.InvalidHandle;
                startInfo.hStdInput = BackEndNativeMethods.InvalidHandle;
                startInfo.hStdOutput = BackEndNativeMethods.InvalidHandle;
                startInfo.dwFlags = BackEndNativeMethods.STARTFUSESTDHANDLES;
                dwCreationFlags = dwCreationFlags | BackEndNativeMethods.CREATENOWINDOW;

                BackEndNativeMethods.SECURITY_ATTRIBUTES pSec = new BackEndNativeMethods.SECURITY_ATTRIBUTES();
                BackEndNativeMethods.SECURITY_ATTRIBUTES tSec = new BackEndNativeMethods.SECURITY_ATTRIBUTES();
                pSec.nLength = Marshal.SizeOf<BackEndNativeMethods.SECURITY_ATTRIBUTES>();
                tSec.nLength = Marshal.SizeOf<BackEndNativeMethods.SECURITY_ATTRIBUTES>();

                BackEndNativeMethods.PROCESS_INFORMATION pInfo = new BackEndNativeMethods.PROCESS_INFORMATION();

                foreach (var specification in contextSpecifications)
                {
                    // Item1: FileTracker context name
                    // Item2: Tuple <string, string, int> as described below
                    FileTracker.StartTrackingContext(tlogPath, specification.Item1);

                    foreach (var processSpecification in specification.Item2)
                    {
                        // Item1: appname
                        // Item2: command line
                        // Item3: number of times to launch
                        for (int i = 0; i < processSpecification.Item3; i++)
                        {
                            BackEndNativeMethods.CreateProcess(processSpecification.Item1, processSpecification.Item2,
                                                        ref pSec, ref tSec,
                                                        false, dwCreationFlags,
                                                        BackEndNativeMethods.NullPtr, null, ref startInfo, out pInfo);
                        }
                    }

                    FileTracker.WriteContextTLogs(tlogPath, specification.Item1);
                    FileTracker.StopTrackingAndCleanup();
                }

                int tlogCount = 0;
                foreach (Tuple<string, int> pattern in tlogPatterns)
                {
                    tlogCount += pattern.Item2;
                }

                // make sure the disk write gets time for NTFS to recognize its existence.  Estimate time needed to sleep based
                // roughly on the number of tlogs that we're looking for (presumably roughly proportional to the number of tlogs
                // being written. 
                Thread.Sleep(Math.Max(200, 250 * tlogCount));

                // Item1: The pattern the tlog name should follow
                // Item2: The number of tlogs following that pattern that should exist in the output directory
                foreach (Tuple<string, int> pattern in tlogPatterns)
                {
                    string[] tlogNames = Directory.GetFiles(tlogPath, pattern.Item1, SearchOption.TopDirectoryOnly);

                    Assert.Equal(pattern.Item2, tlogNames.Length);
                }
            }
            finally
            {
                if (FileUtilities.DirectoryExistsNoThrow(tlogPath))
                {
                    FileUtilities.DeleteDirectoryNoThrow(tlogPath, true);
                }
            }
        }
    }

    internal static class FileTrackerTestHelper
    {
        public static int RunCommand(string command, string arguments)
            => RunCommandWithOptions(command, arguments, true /* print stdout & stderr */, out string _);

        public static int RunCommandNoStdOut(string command, string arguments)
            => RunCommandWithOptions(command, arguments, false /* don't print stdout & stderr */, out string _);

        public static int RunCommandWithLog(string command, string arguments, out string outputAsLog)
            => RunCommandWithOptions(command, arguments, true /* print stdout & stderr */, out outputAsLog);

        private static int RunCommandWithOptions(string command, string arguments, bool printOutput, out string outputAsLog)
        {
            outputAsLog = null;
            ProcessStartInfo si = new ProcessStartInfo(command, arguments);
            if (printOutput)
            {
                si.RedirectStandardOutput = true;
                si.RedirectStandardError = true;
            }

            si.UseShellExecute = false;
            si.CreateNoWindow = true;
            Process p = Process.Start(si);
            p.WaitForExit();

            if (printOutput)
            {
                outputAsLog = "StdOut: \n" + p.StandardOutput.ReadToEnd() + "\nStdErr: \n" + p.StandardError.ReadToEnd();
                Console.Write(outputAsLog);
            }

            return p.ExitCode;
        }

        public static string ReadLineFromFile(string filename, int linenumber) => File.ReadAllLines(filename)[linenumber];

        public static string[] ReadLinesFromFile(string filename) => File.ReadAllLines(filename);

        public static void CleanTlogs()
        {
            string[] tlogFiles = Directory.GetFiles(".", "*.tlog", SearchOption.AllDirectories);

            foreach (string file in tlogFiles)
            {
                File.Delete(file);
            }

            File.Delete("test.in");
            File.Delete("t\u1EBCst.in");
            File.Delete("test.out");

            if (Directory.Exists("outdir"))
            {
                Directory.Delete("outdir", true);
            }
        }

        public static void WriteAll(string filename, string content) => File.WriteAllText(filename, content);


        public static bool FindStringInTlog(string file, string tlog)
            => ReadLinesFromFile(tlog).Contains(file, StringComparer.OrdinalIgnoreCase);

        public static void AssertDidntFindStringInTLog(string file, string tlog)
        {
            string[] lines = ReadLinesFromFile(tlog);

            for (int i = 0; i < lines.Length; i++)
            {
                if (file.Equals(lines[i], StringComparison.OrdinalIgnoreCase))
                {
                    Assert.True(false, "Found string '" + file + "' in '" + tlog + "' at line " + i + ", when it shouldn't have been in the log at all.");
                }
            }
        }

        public static void AssertFoundStringInTLog(string file, string tlog, int timesFound)
        {
            int timesFoundSoFar = 0;
            string[] lines = ReadLinesFromFile(tlog);

            foreach (string line in lines)
            {
                if (file.Equals(line, StringComparison.OrdinalIgnoreCase))
                {
                    timesFoundSoFar++;

                    if (timesFoundSoFar == timesFound)
                    {
                        break;
                    }
                }
            }

            if (timesFound != timesFoundSoFar)
            {
                Assert.True(false, "Searched " + tlog + " but didn't find " + timesFound + " instances of " + file);
            }
        }

        public static void AssertFoundStringInTLog(string file, string tlog) => AssertFoundStringInTLog(file, tlog, 1);
    }
}
#endif
