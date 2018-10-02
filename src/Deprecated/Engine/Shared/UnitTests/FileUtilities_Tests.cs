// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Diagnostics;
using NUnit.Framework;
using System.Text;

using Microsoft.Build.BuildEngine.Shared;
using System.IO;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class FileUtilities_Tests
    {
        /// <summary>
        /// Exercises FileUtilities.GetItemSpecModifier
        /// </summary>
        /// <owner>SumedhK</owner>
        [Test]
        public void GetItemSpecModifier()
        {
            TestGetItemSpecModifier(Environment.CurrentDirectory);
            TestGetItemSpecModifier(null);
        }

        private static void TestGetItemSpecModifier(string currentDirectory)
        {
            Hashtable cache = null;
            string modifier = FileUtilities.GetItemSpecModifier(currentDirectory, "foobar", FileUtilities.ItemSpecModifiers.RecursiveDir, ref cache);
            Assertion.AssertEquals(String.Empty, modifier);
            // cache should be created for constant modifiers
            Assertion.AssertNotNull(cache);

            cache = null;
            modifier = FileUtilities.GetItemSpecModifier(currentDirectory, "foobar", FileUtilities.ItemSpecModifiers.ModifiedTime, ref cache);
            Assertion.AssertEquals(String.Empty, modifier);
            // cache shouldn't be created for volatile modifiers
            Assertion.AssertNull(cache);

            cache = null;
            modifier = FileUtilities.GetItemSpecModifier(currentDirectory, @"foo\bar", FileUtilities.ItemSpecModifiers.RelativeDir, ref cache);
            Assertion.AssertEquals(@"foo\", modifier);
            Assertion.AssertNotNull(cache);
            Assertion.AssertEquals(modifier, cache[FileUtilities.ItemSpecModifiers.RelativeDir]);
            // confirm we get the same thing back the second time
            modifier = FileUtilities.GetItemSpecModifier(currentDirectory, @"foo\bar", FileUtilities.ItemSpecModifiers.RelativeDir, ref cache);
            Assertion.AssertEquals(@"foo\", modifier);
            Assertion.AssertNotNull(cache);
            Assertion.AssertEquals(modifier, cache[FileUtilities.ItemSpecModifiers.RelativeDir]);

            cache = null;
            modifier = FileUtilities.GetItemSpecModifier(currentDirectory, @"c:\foo.txt", FileUtilities.ItemSpecModifiers.FullPath, ref cache);
            Assertion.AssertEquals(@"c:\foo.txt", modifier);

            modifier = FileUtilities.GetItemSpecModifier(currentDirectory, @"c:\foo.txt", FileUtilities.ItemSpecModifiers.RootDir, ref cache);
            Assertion.AssertEquals(@"c:\", modifier);

            modifier = FileUtilities.GetItemSpecModifier(currentDirectory, @"c:\foo.txt", FileUtilities.ItemSpecModifiers.Filename, ref cache);
            Assertion.AssertEquals(@"foo", modifier);

            modifier = FileUtilities.GetItemSpecModifier(currentDirectory, @"c:\foo.txt", FileUtilities.ItemSpecModifiers.Extension, ref cache);
            Assertion.AssertEquals(@".txt", modifier);

            modifier = FileUtilities.GetItemSpecModifier(currentDirectory, @"c:\foo.txt", FileUtilities.ItemSpecModifiers.Directory, ref cache);
            Assertion.AssertEquals(String.Empty, modifier);

            modifier = FileUtilities.GetItemSpecModifier(currentDirectory, @"c:\foo.txt", FileUtilities.ItemSpecModifiers.Identity, ref cache);
            Assertion.AssertEquals(@"c:\foo.txt", modifier);
        }

        /// <summary>
        /// Exercises FileUtilities.GetItemSpecModifier on a bad path.
        /// </summary>
        /// <owner>SumedhK</owner>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void GetItemSpecModifierOnBadPath()
        {
            TestGetItemSpecModifierOnBadPath(Environment.CurrentDirectory);
        }

        /// <summary>
        /// Exercises FileUtilities.GetItemSpecModifier on a bad path.
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void GetItemSpecModifierOnBadPath2()
        {
            TestGetItemSpecModifierOnBadPath(null);
        }

        private static void TestGetItemSpecModifierOnBadPath(string currentDirectory)
        {
            try
            {
                Hashtable cache = null;
                string modifier = FileUtilities.GetItemSpecModifier(currentDirectory, @"http://www.microsoft.com", FileUtilities.ItemSpecModifiers.RootDir, ref cache);
            }
            catch (Exception e)
            {
                // so I can see the exception message in NUnit's "Standard Out" window
                Console.WriteLine(e.Message);
                throw;
            }
        }

        [Test]
        public void GetFileInfoNoThrowBasic()
        {
            string file = null;
            try
            {
                file = Path.GetTempFileName();
                FileInfo info = FileUtilities.GetFileInfoNoThrow(file);
                Assertion.Assert(info.LastWriteTime == new FileInfo(file).LastWriteTime);
            }
            finally
            {
                if (file != null) File.Delete(file);
            }
        }

        [Test]
        public void GetFileInfoNoThrowNonexistent()
        {
            FileInfo info = FileUtilities.GetFileInfoNoThrow("this_file_is_nonexistent");
            Assertion.Assert(info == null);
        }

        /// <summary>
        /// Exercises FileUtilities.EndsWithSlash
        /// </summary>
        /// <owner>SumedhK</owner>
        [Test]
        public void EndsWithSlash()
        {
            Assertion.Assert(FileUtilities.EndsWithSlash(@"C:\foo\"));
            Assertion.Assert(FileUtilities.EndsWithSlash(@"C:\"));
            Assertion.Assert(FileUtilities.EndsWithSlash(@"\"));

            Assertion.Assert(FileUtilities.EndsWithSlash(@"http://www.microsoft.com/"));
            Assertion.Assert(FileUtilities.EndsWithSlash(@"//server/share/"));
            Assertion.Assert(FileUtilities.EndsWithSlash(@"/"));

            Assertion.Assert(!FileUtilities.EndsWithSlash(@"C:\foo"));
            Assertion.Assert(!FileUtilities.EndsWithSlash(@"C:"));
            Assertion.Assert(!FileUtilities.EndsWithSlash(@"foobar"));

            // confirm that empty string doesn't barf
            Assertion.Assert(!FileUtilities.EndsWithSlash(String.Empty));
        }

        /// <summary>
        /// Exercises FileUtilities.GetDirectory
        /// </summary>
        /// <owner>SumedhK</owner>
        [Test]
        public void GetDirectoryWithTrailingSlash()
        {
            Assertion.AssertEquals(@"c:\", FileUtilities.GetDirectory(@"c:\"));
            Assertion.AssertEquals(@"c:\", FileUtilities.GetDirectory(@"c:\foo"));
            Assertion.AssertEquals(@"c:", FileUtilities.GetDirectory(@"c:"));
            Assertion.AssertEquals(@"\", FileUtilities.GetDirectory(@"\"));
            Assertion.AssertEquals(@"\", FileUtilities.GetDirectory(@"\foo"));
            Assertion.AssertEquals(@"..\", FileUtilities.GetDirectory(@"..\foo"));
            Assertion.AssertEquals(@"\foo\", FileUtilities.GetDirectory(@"\foo\"));
            Assertion.AssertEquals(@"\\server\share", FileUtilities.GetDirectory(@"\\server\share"));
            Assertion.AssertEquals(@"\\server\share\", FileUtilities.GetDirectory(@"\\server\share\"));
            Assertion.AssertEquals(@"\\server\share\", FileUtilities.GetDirectory(@"\\server\share\file"));
            Assertion.AssertEquals(@"\\server\share\directory\", FileUtilities.GetDirectory(@"\\server\share\directory\"));
            Assertion.AssertEquals(@"foo\", FileUtilities.GetDirectory(@"foo\bar"));
            Assertion.AssertEquals(@"\foo\bar\", FileUtilities.GetDirectory(@"\foo\bar\"));
            Assertion.AssertEquals(String.Empty, FileUtilities.GetDirectory("foobar"));
        }

        /// <summary>
        /// Exercises FileUtilities.HasExtension
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void HasExtension()
        {
            Assertion.Assert("test 1", FileUtilities.HasExtension("foo.txt", new string[] {".EXE", ".TXT"}));
            Assertion.Assert("test 2", !FileUtilities.HasExtension("foo.txt", new string[] {".EXE", ".DLL"}));
        }

        /// <summary>
        /// Exercises FileUtilities.EnsureTrailingSlash
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void EnsureTrailingSlash()
        {
            // Doesn't have a trailing slash to start with.
            Assertion.AssertEquals("test 1", @"foo\bar\", FileUtilities.EnsureTrailingSlash(@"foo\bar"));
            Assertion.AssertEquals("test 2", @"foo/bar\", FileUtilities.EnsureTrailingSlash(@"foo/bar"));

            // Already has a trailing slash to start with.
            Assertion.AssertEquals("test 3", @"foo/bar/", FileUtilities.EnsureTrailingSlash(@"foo/bar/"));
            Assertion.AssertEquals("test 4", @"foo\bar\", FileUtilities.EnsureTrailingSlash(@"foo\bar\"));
            Assertion.AssertEquals("test 5", @"foo/bar\", FileUtilities.EnsureTrailingSlash(@"foo/bar\"));
            Assertion.AssertEquals("test 5", @"foo\bar/", FileUtilities.EnsureTrailingSlash(@"foo\bar/"));
        }

        /// <summary>
        /// Exercises FileUtilities.IsItemSpecModifier
        /// </summary>
        [Test]
        public void IsItemSpecModifier()
        {
            // Positive matches using exact case.
            Assertion.Assert("test 1", FileUtilities.IsItemSpecModifier("FullPath"));
            Assertion.Assert("test 2", FileUtilities.IsItemSpecModifier("RootDir"));
            Assertion.Assert("test 3", FileUtilities.IsItemSpecModifier("Filename"));
            Assertion.Assert("test 4", FileUtilities.IsItemSpecModifier("Extension"));
            Assertion.Assert("test 5", FileUtilities.IsItemSpecModifier("RelativeDir"));
            Assertion.Assert("test 6", FileUtilities.IsItemSpecModifier("Directory"));
            Assertion.Assert("test 7", FileUtilities.IsItemSpecModifier("RecursiveDir"));
            Assertion.Assert("test 8", FileUtilities.IsItemSpecModifier("Identity"));
            Assertion.Assert("test 9", FileUtilities.IsItemSpecModifier("ModifiedTime"));
            Assertion.Assert("test 10", FileUtilities.IsItemSpecModifier("CreatedTime"));
            Assertion.Assert("test 11", FileUtilities.IsItemSpecModifier("AccessedTime"));

            // Positive matches using different case.
            Assertion.Assert("test 21", FileUtilities.IsItemSpecModifier("fullPath"));
            Assertion.Assert("test 22", FileUtilities.IsItemSpecModifier("rootDir"));
            Assertion.Assert("test 23", FileUtilities.IsItemSpecModifier("filename"));
            Assertion.Assert("test 24", FileUtilities.IsItemSpecModifier("extension"));
            Assertion.Assert("test 25", FileUtilities.IsItemSpecModifier("relativeDir"));
            Assertion.Assert("test 26", FileUtilities.IsItemSpecModifier("directory"));
            Assertion.Assert("test 27", FileUtilities.IsItemSpecModifier("recursiveDir"));
            Assertion.Assert("test 28", FileUtilities.IsItemSpecModifier("identity"));
            Assertion.Assert("test 29", FileUtilities.IsItemSpecModifier("modifiedTime"));
            Assertion.Assert("test 30", FileUtilities.IsItemSpecModifier("createdTime"));
            Assertion.Assert("test 31", FileUtilities.IsItemSpecModifier("accessedTime"));

            // Negative tests to get maximum code coverage inside the many many different branches
            // of FileUtilities.IsItemSpecModifier.
            Assertion.Assert("test 41", !FileUtilities.IsItemSpecModifier("rootxxx"));
            Assertion.Assert("test 42", !FileUtilities.IsItemSpecModifier("Rootxxx"));
            Assertion.Assert("test 43", !FileUtilities.IsItemSpecModifier("xxxxxxx"));

            Assertion.Assert("test 44", !FileUtilities.IsItemSpecModifier("filexxxx"));
            Assertion.Assert("test 45", !FileUtilities.IsItemSpecModifier("Filexxxx"));
            Assertion.Assert("test 46", !FileUtilities.IsItemSpecModifier("idenxxxx"));
            Assertion.Assert("test 47", !FileUtilities.IsItemSpecModifier("Idenxxxx"));
            Assertion.Assert("test 48", !FileUtilities.IsItemSpecModifier("xxxxxxxx"));

            Assertion.Assert("test 49", !FileUtilities.IsItemSpecModifier("extenxxxx"));
            Assertion.Assert("test 50", !FileUtilities.IsItemSpecModifier("Extenxxxx"));
            Assertion.Assert("test 51", !FileUtilities.IsItemSpecModifier("direcxxxx"));
            Assertion.Assert("test 52", !FileUtilities.IsItemSpecModifier("Direcxxxx"));
            Assertion.Assert("test 53", !FileUtilities.IsItemSpecModifier("xxxxxxxxx"));

            Assertion.Assert("test 54", !FileUtilities.IsItemSpecModifier("xxxxxxxxxx"));

            Assertion.Assert("test 55", !FileUtilities.IsItemSpecModifier("relativexxx"));
            Assertion.Assert("test 56", !FileUtilities.IsItemSpecModifier("Relativexxx"));
            Assertion.Assert("test 57", !FileUtilities.IsItemSpecModifier("createdxxxx"));
            Assertion.Assert("test 58", !FileUtilities.IsItemSpecModifier("Createdxxxx"));
            Assertion.Assert("test 59", !FileUtilities.IsItemSpecModifier("xxxxxxxxxxx"));
            
            Assertion.Assert("test 60", !FileUtilities.IsItemSpecModifier("recursivexxx"));
            Assertion.Assert("test 61", !FileUtilities.IsItemSpecModifier("Recursivexxx"));
            Assertion.Assert("test 62", !FileUtilities.IsItemSpecModifier("accessedxxxx"));
            Assertion.Assert("test 63", !FileUtilities.IsItemSpecModifier("Accessedxxxx"));
            Assertion.Assert("test 64", !FileUtilities.IsItemSpecModifier("modifiedxxxx"));
            Assertion.Assert("test 65", !FileUtilities.IsItemSpecModifier("Modifiedxxxx"));
            Assertion.Assert("test 66", !FileUtilities.IsItemSpecModifier("xxxxxxxxxxxx"));
            
            Assertion.Assert("test 67", !FileUtilities.IsItemSpecModifier(null));
        }

        [Test]
        public void CheckDerivableItemSpecModifiers()
        {
            Assertion.Assert(FileUtilities.IsDerivableItemSpecModifier("Filename"));
            Assertion.Assert(!FileUtilities.IsDerivableItemSpecModifier("RecursiveDir"));
            Assertion.Assert(!FileUtilities.IsDerivableItemSpecModifier("recursivedir"));
        }

        [Test]
        public void GetExecutablePath()
        {
            StringBuilder sb = new StringBuilder(NativeMethods.MAX_PATH);
            NativeMethods.GetModuleFileName(NativeMethods.NullHandleRef, sb, sb.Capacity);
            string path = sb.ToString();

            string configPath = FileUtilities.CurrentExecutableConfigurationFilePath;
            string directoryName = FileUtilities.CurrentExecutableDirectory;
            string executablePath = FileUtilities.CurrentExecutablePath;
            Assert.IsTrue(string.Compare(configPath, executablePath + ".config", StringComparison.OrdinalIgnoreCase) == 0);
            Assert.IsTrue(string.Compare(path, executablePath, StringComparison.OrdinalIgnoreCase) == 0);
            Assert.IsTrue(string.Compare(directoryName, Path.GetDirectoryName(path), StringComparison.OrdinalIgnoreCase) == 0);

        }
    }
}
