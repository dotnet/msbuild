// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;

using Microsoft.Build.Collections;
using Microsoft.Build.Shared;
using System.IO;
using System.Collections.Generic;

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public class FileUtilities_Tests
    {
        /// <summary>
        /// Exercises FileUtilities.ItemSpecModifiers.GetItemSpecModifier
        /// </summary>
        [TestMethod]
        public void GetItemSpecModifier()
        {
            TestGetItemSpecModifier(Environment.CurrentDirectory);
            TestGetItemSpecModifier(null);
        }

        private static void TestGetItemSpecModifier(string currentDirectory)
        {
            string cache = null;
            string modifier = FileUtilities.ItemSpecModifiers.GetItemSpecModifier(currentDirectory, "foo", String.Empty, FileUtilities.ItemSpecModifiers.RecursiveDir, ref cache);
            Assert.AreEqual(String.Empty, modifier);

            cache = null;
            modifier = FileUtilities.ItemSpecModifiers.GetItemSpecModifier(currentDirectory, "foo", String.Empty, FileUtilities.ItemSpecModifiers.ModifiedTime, ref cache);
            Assert.AreEqual(String.Empty, modifier);

            cache = null;
            modifier = FileUtilities.ItemSpecModifiers.GetItemSpecModifier(currentDirectory, @"foo\goo", String.Empty, FileUtilities.ItemSpecModifiers.RelativeDir, ref cache);
            Assert.AreEqual(@"foo\", modifier);

            // confirm we get the same thing back the second time
            modifier = FileUtilities.ItemSpecModifiers.GetItemSpecModifier(currentDirectory, @"foo\goo", String.Empty, FileUtilities.ItemSpecModifiers.RelativeDir, ref cache);
            Assert.AreEqual(@"foo\", modifier);

            cache = null;
            modifier = FileUtilities.ItemSpecModifiers.GetItemSpecModifier(currentDirectory, @"c:\foo.txt", String.Empty, FileUtilities.ItemSpecModifiers.FullPath, ref cache);
            Assert.AreEqual(@"c:\foo.txt", modifier);
            Assert.AreEqual(@"c:\foo.txt", cache);

            modifier = FileUtilities.ItemSpecModifiers.GetItemSpecModifier(currentDirectory, @"c:\foo.txt", String.Empty, FileUtilities.ItemSpecModifiers.RootDir, ref cache);
            Assert.AreEqual(@"c:\", modifier);

            modifier = FileUtilities.ItemSpecModifiers.GetItemSpecModifier(currentDirectory, @"c:\foo.txt", String.Empty, FileUtilities.ItemSpecModifiers.Filename, ref cache);
            Assert.AreEqual(@"foo", modifier);

            modifier = FileUtilities.ItemSpecModifiers.GetItemSpecModifier(currentDirectory, @"c:\foo.txt", String.Empty, FileUtilities.ItemSpecModifiers.Extension, ref cache);
            Assert.AreEqual(@".txt", modifier);

            modifier = FileUtilities.ItemSpecModifiers.GetItemSpecModifier(currentDirectory, @"c:\foo.txt", String.Empty, FileUtilities.ItemSpecModifiers.Directory, ref cache);
            Assert.AreEqual(String.Empty, modifier);

            modifier = FileUtilities.ItemSpecModifiers.GetItemSpecModifier(currentDirectory, @"c:\foo.txt", String.Empty, FileUtilities.ItemSpecModifiers.Identity, ref cache);
            Assert.AreEqual(@"c:\foo.txt", modifier);

            modifier = FileUtilities.ItemSpecModifiers.GetItemSpecModifier(currentDirectory, @"c:\foo.txt", @"c:\abc\goo.proj", FileUtilities.ItemSpecModifiers.DefiningProjectDirectory, ref cache);
            Assert.AreEqual(@"c:\abc\", modifier);

            modifier = FileUtilities.ItemSpecModifiers.GetItemSpecModifier(currentDirectory, @"c:\foo.txt", @"c:\abc\goo.proj", FileUtilities.ItemSpecModifiers.DefiningProjectExtension, ref cache);
            Assert.AreEqual(@".proj", modifier);

            modifier = FileUtilities.ItemSpecModifiers.GetItemSpecModifier(currentDirectory, @"c:\foo.txt", @"c:\abc\goo.proj", FileUtilities.ItemSpecModifiers.DefiningProjectFullPath, ref cache);
            Assert.AreEqual(@"c:\abc\goo.proj", modifier);

            modifier = FileUtilities.ItemSpecModifiers.GetItemSpecModifier(currentDirectory, @"c:\foo.txt", @"c:\abc\goo.proj", FileUtilities.ItemSpecModifiers.DefiningProjectName, ref cache);
            Assert.AreEqual(@"goo", modifier);
        }

        [TestMethod]
        public void MakeRelativeTests()
        {
            Assert.AreEqual(@"foo.cpp", FileUtilities.MakeRelative(@"c:\abc\def", @"c:\abc\def\foo.cpp"));
            Assert.AreEqual(@"def\foo.cpp", FileUtilities.MakeRelative(@"c:\abc\", @"c:\abc\def\foo.cpp"));
            Assert.AreEqual(@"..\foo.cpp", FileUtilities.MakeRelative(@"c:\abc\def\xyz", @"c:\abc\def\foo.cpp"));
            Assert.AreEqual(@"..\ttt\foo.cpp", FileUtilities.MakeRelative(@"c:\abc\def\xyz\", @"c:\abc\def\ttt\foo.cpp"));
            Assert.AreEqual(@"e:\abc\def\foo.cpp", FileUtilities.MakeRelative(@"c:\abc\def", @"e:\abc\def\foo.cpp"));
            Assert.AreEqual(@"foo.cpp", FileUtilities.MakeRelative(@"\\aaa\abc\def", @"\\aaa\abc\def\foo.cpp"));
            Assert.AreEqual(@"foo.cpp", FileUtilities.MakeRelative(@"c:\abc\def", @"foo.cpp"));
            Assert.AreEqual(@"foo.cpp", FileUtilities.MakeRelative(@"c:\abc\def", @"..\def\foo.cpp"));
            Assert.AreEqual(@"\\host\path\file", FileUtilities.MakeRelative(@"c:\abc\def", @"\\host\path\file"));
            Assert.AreEqual(@"\\host\d$\file", FileUtilities.MakeRelative(@"c:\abc\def", @"\\host\d$\file"));
        }

        /// <summary>
        /// Exercises FileUtilities.ItemSpecModifiers.GetItemSpecModifier on a bad path.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void GetItemSpecModifierOnBadPath()
        {
            TestGetItemSpecModifierOnBadPath(Environment.CurrentDirectory);
        }

        /// <summary>
        /// Exercises FileUtilities.ItemSpecModifiers.GetItemSpecModifier on a bad path.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void GetItemSpecModifierOnBadPath2()
        {
            TestGetItemSpecModifierOnBadPath(null);
        }

        private static void TestGetItemSpecModifierOnBadPath(string currentDirectory)
        {
            try
            {
                string cache = null;
                string modifier = FileUtilities.ItemSpecModifiers.GetItemSpecModifier(currentDirectory, @"http://www.microsoft.com", String.Empty, FileUtilities.ItemSpecModifiers.RootDir, ref cache);
            }
            catch (Exception e)
            {
                // so I can see the exception message in NUnit's "Standard Out" window
                Console.WriteLine(e.Message);
                throw;
            }
        }

        [TestMethod]
        public void GetFileInfoNoThrowBasic()
        {
            string file = null;
            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = FileUtilities.GetFileInfoNoThrow(file);
                Assert.IsTrue(info.LastWriteTime == new FileInfo(file).LastWriteTime);
            }
            finally
            {
                if (file != null) File.Delete(file);
            }
        }

        [TestMethod]
        public void GetFileInfoNoThrowNonexistent()
        {
            FileInfo info = FileUtilities.GetFileInfoNoThrow("this_file_is_nonexistent");
            Assert.IsTrue(info == null);
        }

        /// <summary>
        /// Exercises FileUtilities.EndsWithSlash
        /// </summary>
        [TestMethod]
        public void EndsWithSlash()
        {
            Assert.IsTrue(FileUtilities.EndsWithSlash(@"C:\foo\"));
            Assert.IsTrue(FileUtilities.EndsWithSlash(@"C:\"));
            Assert.IsTrue(FileUtilities.EndsWithSlash(@"\"));

            Assert.IsTrue(FileUtilities.EndsWithSlash(@"http://www.microsoft.com/"));
            Assert.IsTrue(FileUtilities.EndsWithSlash(@"//server/share/"));
            Assert.IsTrue(FileUtilities.EndsWithSlash(@"/"));

            Assert.IsFalse(FileUtilities.EndsWithSlash(@"C:\foo"));
            Assert.IsFalse(FileUtilities.EndsWithSlash(@"C:"));
            Assert.IsFalse(FileUtilities.EndsWithSlash(@"foo"));

            // confirm that empty string doesn't barf
            Assert.IsFalse(FileUtilities.EndsWithSlash(String.Empty));
        }

        /// <summary>
        /// Exercises FileUtilities.GetDirectory
        /// </summary>
        [TestMethod]
        public void GetDirectoryWithTrailingSlash()
        {
            Assert.AreEqual(@"c:\", FileUtilities.GetDirectory(@"c:\"));
            Assert.AreEqual(@"c:\", FileUtilities.GetDirectory(@"c:\foo"));
            Assert.AreEqual(@"c:", FileUtilities.GetDirectory(@"c:"));
            Assert.AreEqual(@"\", FileUtilities.GetDirectory(@"\"));
            Assert.AreEqual(@"\", FileUtilities.GetDirectory(@"\foo"));
            Assert.AreEqual(@"..\", FileUtilities.GetDirectory(@"..\foo"));
            Assert.AreEqual(@"\foo\", FileUtilities.GetDirectory(@"\foo\"));
            Assert.AreEqual(@"\\server\share", FileUtilities.GetDirectory(@"\\server\share"));
            Assert.AreEqual(@"\\server\share\", FileUtilities.GetDirectory(@"\\server\share\"));
            Assert.AreEqual(@"\\server\share\", FileUtilities.GetDirectory(@"\\server\share\file"));
            Assert.AreEqual(@"\\server\share\directory\", FileUtilities.GetDirectory(@"\\server\share\directory\"));
            Assert.AreEqual(@"foo\", FileUtilities.GetDirectory(@"foo\bar"));
            Assert.AreEqual(@"\foo\bar\", FileUtilities.GetDirectory(@"\foo\bar\"));
            Assert.AreEqual(String.Empty, FileUtilities.GetDirectory("foo"));
        }

        /// <summary>
        /// Exercises FileUtilities.HasExtension
        /// </summary>
        [TestMethod]
        public void HasExtension()
        {
            Assert.IsTrue(FileUtilities.HasExtension("foo.txt", new string[] { ".EXE", ".TXT" }), "test 1");
            Assert.IsFalse(FileUtilities.HasExtension("foo.txt", new string[] { ".EXE", ".DLL" }), "test 2");
        }

        /// <summary>
        /// Exercises FileUtilities.EnsureTrailingSlash
        /// </summary>
        [TestMethod]
        public void EnsureTrailingSlash()
        {
            // Doesn't have a trailing slash to start with.
            Assert.AreEqual(@"foo\bar\", FileUtilities.EnsureTrailingSlash(@"foo\bar"), "test 1");
            Assert.AreEqual(@"foo/bar\", FileUtilities.EnsureTrailingSlash(@"foo/bar"), "test 2");

            // Already has a trailing slash to start with.
            Assert.AreEqual(@"foo/bar/", FileUtilities.EnsureTrailingSlash(@"foo/bar/"), "test 3");
            Assert.AreEqual(@"foo\bar\", FileUtilities.EnsureTrailingSlash(@"foo\bar\"), "test 4");
            Assert.AreEqual(@"foo/bar\", FileUtilities.EnsureTrailingSlash(@"foo/bar\"), "test 5");
            Assert.AreEqual(@"foo\bar/", FileUtilities.EnsureTrailingSlash(@"foo\bar/"), "test 5");
        }

        /// <summary>
        /// Exercises FileUtilities.ItemSpecModifiers.IsItemSpecModifier
        /// </summary>
        [TestMethod]
        public void IsItemSpecModifier()
        {
            // Positive matches using exact case.
            Assert.IsTrue(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("FullPath"), "test 1");
            Assert.IsTrue(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("RootDir"), "test 2");
            Assert.IsTrue(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("Filename"), "test 3");
            Assert.IsTrue(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("Extension"), "test 4");
            Assert.IsTrue(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("RelativeDir"), "test 5");
            Assert.IsTrue(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("Directory"), "test 6");
            Assert.IsTrue(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("RecursiveDir"), "test 7");
            Assert.IsTrue(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("Identity"), "test 8");
            Assert.IsTrue(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("ModifiedTime"), "test 9");
            Assert.IsTrue(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("CreatedTime"), "test 10");
            Assert.IsTrue(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("AccessedTime"), "test 11");

            // Positive matches using different case.
            Assert.IsTrue(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("fullPath"), "test 21");
            Assert.IsTrue(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("rootDir"), "test 22");
            Assert.IsTrue(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("filename"), "test 23");
            Assert.IsTrue(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("extension"), "test 24");
            Assert.IsTrue(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("relativeDir"), "test 25");
            Assert.IsTrue(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("directory"), "test 26");
            Assert.IsTrue(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("recursiveDir"), "test 27");
            Assert.IsTrue(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("identity"), "test 28");
            Assert.IsTrue(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("modifiedTime"), "test 29");
            Assert.IsTrue(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("createdTime"), "test 30");
            Assert.IsTrue(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("accessedTime"), "test 31");

            // Negative tests to get maximum code coverage inside the many many different branches
            // of FileUtilities.ItemSpecModifiers.IsItemSpecModifier.
            Assert.IsFalse(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("rootxxx"), "test 41");
            Assert.IsFalse(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("Rootxxx"), "test 42");
            Assert.IsFalse(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("xxxxxxx"), "test 43");

            Assert.IsFalse(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("filexxxx"), "test 44");
            Assert.IsFalse(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("Filexxxx"), "test 45");
            Assert.IsFalse(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("idenxxxx"), "test 46");
            Assert.IsFalse(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("Idenxxxx"), "test 47");
            Assert.IsFalse(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("xxxxxxxx"), "test 48");

            Assert.IsFalse(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("extenxxxx"), "test 49");
            Assert.IsFalse(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("Extenxxxx"), "test 50");
            Assert.IsFalse(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("direcxxxx"), "test 51");
            Assert.IsFalse(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("Direcxxxx"), "test 52");
            Assert.IsFalse(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("xxxxxxxxx"), "test 53");

            Assert.IsFalse(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("xxxxxxxxxx"), "test 54");

            Assert.IsFalse(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("relativexxx"), "test 55");
            Assert.IsFalse(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("Relativexxx"), "test 56");
            Assert.IsFalse(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("createdxxxx"), "test 57");
            Assert.IsFalse(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("Createdxxxx"), "test 58");
            Assert.IsFalse(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("xxxxxxxxxxx"), "test 59");

            Assert.IsFalse(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("recursivexxx"), "test 60");
            Assert.IsFalse(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("Recursivexxx"), "test 61");
            Assert.IsFalse(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("accessedxxxx"), "test 62");
            Assert.IsFalse(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("Accessedxxxx"), "test 63");
            Assert.IsFalse(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("modifiedxxxx"), "test 64");
            Assert.IsFalse(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("Modifiedxxxx"), "test 65");
            Assert.IsFalse(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("xxxxxxxxxxxx"), "test 66");

            Assert.IsFalse(FileUtilities.ItemSpecModifiers.IsItemSpecModifier(null), "test 67");
        }

        [TestMethod]
        public void CheckDerivableItemSpecModifiers()
        {
            Assert.IsTrue(FileUtilities.ItemSpecModifiers.IsDerivableItemSpecModifier("Filename"));
            Assert.IsFalse(FileUtilities.ItemSpecModifiers.IsDerivableItemSpecModifier("RecursiveDir"));
            Assert.IsFalse(FileUtilities.ItemSpecModifiers.IsDerivableItemSpecModifier("recursivedir"));
        }

        [TestMethod]
        public void GetExecutablePath()
        {
            string path = Path.Combine(Environment.CurrentDirectory, "msbuild.exe").ToLowerInvariant();

            string configPath = FileUtilities.CurrentExecutableConfigurationFilePath.ToLowerInvariant();
            string directoryName = FileUtilities.CurrentExecutableDirectory.ToLowerInvariant();
            string executablePath = FileUtilities.CurrentExecutablePath.ToLowerInvariant();

            Assert.AreEqual(configPath, executablePath + ".config");
            Assert.AreEqual(path, executablePath);
            Assert.AreEqual(directoryName, Path.GetDirectoryName(path));
        }

        [TestMethod]
        public void NormalizePathThatFitsIntoMaxPath()
        {
            string currentDirectory = @"c:\aardvark\aardvark\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890";
            string filePath = @"..\..\..\..\..\..\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\a.cs";
            string fullPath = @"c:\aardvark\aardvark\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\a.cs";

            Assert.AreEqual(fullPath, FileUtilities.NormalizePath(Path.Combine(currentDirectory, filePath)));
        }

        [TestMethod]
        [ExpectedException(typeof(PathTooLongException))]
        public void NormalizePathThatDoesntFitIntoMaxPath()
        {
            string currentDirectory = @"c:\aardvark\aardvark\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890";
            string filePath = @"..\..\..\..\..\..\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\a.cs";

            // This path ends up over 420 characters long
            string fullPath = @"c:\aardvark\aardvark\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\a.cs";

            Assert.AreEqual(fullPath, FileUtilities.NormalizePath(Path.Combine(currentDirectory, filePath)));
        }

        [TestMethod]
        public void GetItemSpecModifierRootDirThatFitsIntoMaxPath()
        {
            string currentDirectory = @"c:\aardvark\aardvark\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890";
            string fullPath = @"c:\aardvark\aardvark\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\a.cs";
            string cache = fullPath;

            Assert.AreEqual(@"c:\", FileUtilities.ItemSpecModifiers.GetItemSpecModifier(currentDirectory, fullPath, String.Empty, FileUtilities.ItemSpecModifiers.RootDir, ref cache));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void NormalizePathNull()
        {
            Assert.AreEqual(null, FileUtilities.NormalizePath(null));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void NormalizePathEmpty()
        {
            Assert.AreEqual(null, FileUtilities.NormalizePath(String.Empty));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void NormalizePathBadUNC1()
        {
            Assert.AreEqual(null, FileUtilities.NormalizePath(@"\\"));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void NormalizePathBadUNC2()
        {
            Assert.AreEqual(null, FileUtilities.NormalizePath(@"\\XXX\"));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void NormalizePathBadUNC3()
        {
            Assert.AreEqual(@"\\localhost", FileUtilities.NormalizePath(@"\\localhost"));
        }

        [TestMethod]
        public void NormalizePathGoodUNC()
        {
            Assert.AreEqual(@"\\localhost\share", FileUtilities.NormalizePath(@"\\localhost\share"));
        }

        [TestMethod]
        public void NormalizePathTooLongWithDots()
        {
            string longPart = new string('x', 300);
            Assert.AreEqual(@"c:\abc\def", FileUtilities.NormalizePath(@"c:\abc\" + longPart + @"\..\def"));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void NormalizePathBadGlobalroot()
        {
            /*
             From Path.cs
               // Check for \\?\Globalroot, an internal mechanism to the kernel
               // that provides aliases for drives and other undocumented stuff.
               // The kernel team won't even describe the full set of what
               // is available here - we don't want managed apps mucking 
               // with this for security reasons.
             * */
            Assert.AreEqual(null, FileUtilities.NormalizePath(@"\\?\globalroot\XXX"));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void NormalizePathInvalid()
        {
            string filePath = @"c:\aardvark\|||";
            Assert.AreEqual(null, FileUtilities.NormalizePath(filePath));
        }

        [TestMethod]
        public void FileOrDirectoryExistsNoThrow()
        {
            Assert.AreEqual(false, FileUtilities.FileOrDirectoryExistsNoThrow("||"));
            Assert.AreEqual(false, FileUtilities.FileOrDirectoryExistsNoThrow("c:\\doesnot_exist"));
            Assert.AreEqual(true, FileUtilities.FileOrDirectoryExistsNoThrow("c:\\"));
            Assert.AreEqual(true, FileUtilities.FileOrDirectoryExistsNoThrow(Path.GetTempPath()));

            string path = null;

            try
            {
                path = FileUtilities.GetTemporaryFile();
                Assert.AreEqual(true, FileUtilities.FileOrDirectoryExistsNoThrow(path));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void FileOrDirectoryExistsNoThrowTooLongWithDots()
        {
            int length = (Environment.SystemDirectory + @"\" + @"\..\..\..\" + Environment.SystemDirectory.Substring(3)).Length;

            string longPart = new string('x', 260 - length); // We want the shortest that is > max path.

            string inputPath = Environment.SystemDirectory + @"\" + longPart + @"\..\..\..\" + Environment.SystemDirectory.Substring(3);
            Console.WriteLine(inputPath.Length);

            // "c:\windows\system32\<verylong>\..\..\windows\system32" exists
            Assert.AreEqual(true, FileUtilities.FileOrDirectoryExistsNoThrow(inputPath));
            Assert.AreEqual(false, FileUtilities.FileOrDirectoryExistsNoThrow(inputPath.Replace('\\', 'X')));
        }

        [TestMethod]
        public void FileOrDirectoryExistsNoThrowTooLongWithDotsRelative()
        {
            int length = (Environment.SystemDirectory + @"\" + @"\..\..\..\" + Environment.SystemDirectory.Substring(3)).Length;

            string longPart = new string('x', 260 - length); // We want the shortest that is > max path.

            string inputPath = longPart + @"\..\..\..\" + Environment.SystemDirectory.Substring(3);
            Console.WriteLine(inputPath.Length);

            // "c:\windows\system32\<verylong>\..\..\windows\system32" exists

            string currentDirectory = Environment.CurrentDirectory;

            try
            {
                currentDirectory = Environment.CurrentDirectory;
                Environment.CurrentDirectory = Environment.SystemDirectory;

                Assert.AreEqual(true, FileUtilities.FileOrDirectoryExistsNoThrow(inputPath));
                Assert.AreEqual(false, FileUtilities.FileOrDirectoryExistsNoThrow(inputPath.Replace('\\', 'X')));
            }
            finally
            {
                Environment.CurrentDirectory = currentDirectory;
            }
        }

        [TestMethod]
        public void DirectoryExistsNoThrowTooLongWithDots()
        {
            int length = (Environment.SystemDirectory + @"\" + @"\..\..\..\" + Environment.SystemDirectory.Substring(3)).Length;

            string longPart = new string('x', 260 - length); // We want the shortest that is > max path.

            string inputPath = Environment.SystemDirectory + @"\" + longPart + @"\..\..\..\" + Environment.SystemDirectory.Substring(3);
            Console.WriteLine(inputPath.Length);

            // "c:\windows\system32\<verylong>\..\..\windows\system32" exists
            Assert.AreEqual(true, FileUtilities.DirectoryExistsNoThrow(inputPath));
        }

        [TestMethod]
        public void DirectoryExistsNoThrowTooLongWithDotsRelative()
        {
            int length = (Environment.SystemDirectory + @"\" + @"\..\..\..\" + Environment.SystemDirectory.Substring(3)).Length;

            string longPart = new string('x', 260 - length); // We want the shortest that is > max path.

            string inputPath = longPart + @"\..\..\..\" + Environment.SystemDirectory.Substring(3);
            Console.WriteLine(inputPath.Length);

            // "c:\windows\system32\<verylong>\..\..\windows\system32" exists

            string currentDirectory = Environment.CurrentDirectory;

            try
            {
                currentDirectory = Environment.CurrentDirectory;
                Environment.CurrentDirectory = Environment.SystemDirectory;

                Assert.AreEqual(true, FileUtilities.DirectoryExistsNoThrow(inputPath));
                Assert.AreEqual(false, FileUtilities.DirectoryExistsNoThrow(inputPath.Replace('\\', 'X')));
            }
            finally
            {
                Environment.CurrentDirectory = currentDirectory;
            }
        }

        [TestMethod]
        public void FileExistsNoThrowTooLongWithDots()
        {
            int length = (Environment.SystemDirectory + @"\" + @"\..\..\..\" + Environment.SystemDirectory.Substring(3) + @"\..\explorer.exe").Length;

            string longPart = new string('x', 260 - length); // We want the shortest that is > max path.

            string inputPath = Environment.SystemDirectory + @"\" + longPart + @"\..\..\..\" + Environment.SystemDirectory.Substring(3) + @"\..\explorer.exe";
            Console.WriteLine(inputPath.Length);
            Console.WriteLine(inputPath);

            // "c:\windows\system32\<verylong>\..\..\windows\system32" exists
            Assert.AreEqual(true, FileUtilities.FileExistsNoThrow(inputPath));
        }

        [TestMethod]
        public void FileExistsNoThrowTooLongWithDotsRelative()
        {
            int length = (Environment.SystemDirectory + @"\" + @"\..\..\..\" + Environment.SystemDirectory.Substring(3) + @"\..\explorer.exe").Length;

            string longPart = new string('x', 260 - length); // We want the shortest that is > max path.

            string inputPath = longPart + @"\..\..\..\" + Environment.SystemDirectory.Substring(3) + @"\..\explorer.exe";
            Console.WriteLine(inputPath.Length);

            // "c:\windows\system32\<verylong>\..\..\windows\system32" exists

            string currentDirectory = Environment.CurrentDirectory;

            try
            {
                currentDirectory = Environment.CurrentDirectory;
                Environment.CurrentDirectory = Environment.SystemDirectory;

                Assert.AreEqual(true, FileUtilities.FileExistsNoThrow(inputPath));
                Assert.AreEqual(false, FileUtilities.FileExistsNoThrow(inputPath.Replace('\\', 'X')));
            }
            finally
            {
                Environment.CurrentDirectory = currentDirectory;
            }
        }

        [TestMethod]
        public void GetFileInfoNoThrowTooLongWithDots()
        {
            int length = (Environment.SystemDirectory + @"\" + @"\..\..\..\" + Environment.SystemDirectory.Substring(3) + @"\..\explorer.exe").Length;

            string longPart = new string('x', 260 - length); // We want the shortest that is > max path.

            string inputPath = Environment.SystemDirectory + @"\" + longPart + @"\..\..\..\" + Environment.SystemDirectory.Substring(3) + @"\..\explorer.exe";
            Console.WriteLine(inputPath.Length);

            // "c:\windows\system32\<verylong>\..\..\windows\system32" exists
            Assert.AreEqual(true, FileUtilities.GetFileInfoNoThrow(inputPath) != null);
            Assert.AreEqual(false, FileUtilities.GetFileInfoNoThrow(inputPath.Replace('\\', 'X')) != null);
        }

        [TestMethod]
        public void GetFileInfoNoThrowTooLongWithDotsRelative()
        {
            int length = (Environment.SystemDirectory + @"\" + @"\..\..\..\" + Environment.SystemDirectory.Substring(3) + @"\..\explorer.exe").Length;

            string longPart = new string('x', 260 - length); // We want the shortest that is > max path.

            string inputPath = longPart + @"\..\..\..\" + Environment.SystemDirectory.Substring(3) + @"\..\explorer.exe";
            Console.WriteLine(inputPath.Length);

            // "c:\windows\system32\<verylong>\..\..\windows\system32" exists

            string currentDirectory = Environment.CurrentDirectory;

            try
            {
                currentDirectory = Environment.CurrentDirectory;
                Environment.CurrentDirectory = Environment.SystemDirectory;

                Assert.AreEqual(true, FileUtilities.GetFileInfoNoThrow(inputPath) != null);
                Assert.AreEqual(false, FileUtilities.GetFileInfoNoThrow(inputPath.Replace('\\', 'X')) != null);
            }
            finally
            {
                Environment.CurrentDirectory = currentDirectory;
            }
        }

        /// <summary>
        /// Simple test, neither the base file nor retry files exist
        /// </summary>
        [TestMethod]
        public void GenerateTempFileNameSimple()
        {
            string path = null;

            try
            {
                path = FileUtilities.GetTemporaryFile();

                Assert.AreEqual(true, path.EndsWith(".tmp"));
                Assert.AreEqual(true, File.Exists(path));
                Assert.AreEqual(true, path.StartsWith(Path.GetTempPath()));
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Choose an extension
        /// </summary>
        [TestMethod]
        public void GenerateTempFileNameWithExtension()
        {
            string path = null;

            try
            {
                path = Shared.FileUtilities.GetTemporaryFile(".bat");

                Assert.AreEqual(true, path.EndsWith(".bat"));
                Assert.AreEqual(true, File.Exists(path));
                Assert.AreEqual(true, path.StartsWith(Path.GetTempPath()));
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Choose a (missing) directory and extension
        /// </summary>
        [TestMethod]
        public void GenerateTempFileNameWithDirectoryAndExtension()
        {
            string path = null;
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "subfolder");

            try
            {
                path = Shared.FileUtilities.GetTemporaryFile(directory, ".bat");

                Assert.AreEqual(true, path.EndsWith(".bat"));
                Assert.AreEqual(true, File.Exists(path));
                Assert.AreEqual(true, path.StartsWith(directory));
            }
            finally
            {
                File.Delete(path);
                Directory.Delete(directory);
            }
        }

        /// <summary>
        /// Extension without a period
        /// </summary>
        [TestMethod]
        public void GenerateTempFileNameWithExtensionNoPeriod()
        {
            string path = null;

            try
            {
                path = Shared.FileUtilities.GetTemporaryFile("bat");

                Assert.AreEqual(true, path.EndsWith(".bat"));
                Assert.AreEqual(true, File.Exists(path));
                Assert.AreEqual(true, path.StartsWith(Path.GetTempPath()));
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Extension is invalid
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(IOException))]
        public void GenerateTempBatchFileWithBadExtension()
        {
            Shared.FileUtilities.GetTemporaryFile("|");
        }

        /// <summary>
        /// No extension is given
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void GenerateTempBatchFileWithEmptyExtension()
        {
            Shared.FileUtilities.GetTemporaryFile(String.Empty);
        }

        /// <summary>
        /// Directory is invalid
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(IOException))]
        public void GenerateTempBatchFileWithBadDirectory()
        {
            Shared.FileUtilities.GetTemporaryFile("|", ".tmp");
        }
    }
}
