// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using NUnit.Framework;
using System.Text;

using Microsoft.Build.Shared;
using System.IO;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class FileUtilities_Tests
    {
        /// <summary>
        /// Exercises FileUtilities.ItemSpecModifiers.GetItemSpecModifier
        /// </summary>
        [Test]
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

        [Test]
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
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void GetItemSpecModifierOnBadPath()
        {
            TestGetItemSpecModifierOnBadPath(Environment.CurrentDirectory);
        }

        /// <summary>
        /// Exercises FileUtilities.ItemSpecModifiers.GetItemSpecModifier on a bad path.
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

        [Test]
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

        [Test]
        public void GetFileInfoNoThrowNonexistent()
        {
            FileInfo info = FileUtilities.GetFileInfoNoThrow("this_file_is_nonexistent");
            Assert.IsTrue(info == null);
        }

        /// <summary>
        /// Exercises FileUtilities.EndsWithSlash
        /// </summary>
        [Test]
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
        [Test]
        public void GetDirectoryWithTrailingSlash()
        {
            Assert.AreEqual(NativeMethodsShared.IsWindows ? @"c:\" : "/", FileUtilities.GetDirectory(NativeMethodsShared.IsWindows ? @"c:\" : "/"));
            Assert.AreEqual(NativeMethodsShared.IsWindows ? @"c:\" : "/", FileUtilities.GetDirectory(NativeMethodsShared.IsWindows ? @"c:\foo" : "/foo"));
            Assert.AreEqual(NativeMethodsShared.IsWindows ? @"c:" : "/", FileUtilities.GetDirectory(NativeMethodsShared.IsWindows ? @"c:" : "/"));
            Assert.AreEqual(FileUtilities.FixFilePath(@"\"), FileUtilities.GetDirectory(@"\"));
            Assert.AreEqual(FileUtilities.FixFilePath(@"\"), FileUtilities.GetDirectory(@"\foo"));
            Assert.AreEqual(FileUtilities.FixFilePath(@"..\"), FileUtilities.GetDirectory(@"..\foo"));
            Assert.AreEqual(FileUtilities.FixFilePath(@"\foo\"), FileUtilities.GetDirectory(@"\foo\"));
            Assert.AreEqual(FileUtilities.FixFilePath(@"\\server\share"), FileUtilities.GetDirectory(@"\\server\share"));
            Assert.AreEqual(FileUtilities.FixFilePath(@"\\server\share\"), FileUtilities.GetDirectory(@"\\server\share\"));
            Assert.AreEqual(FileUtilities.FixFilePath(@"\\server\share\"), FileUtilities.GetDirectory(@"\\server\share\file"));
            Assert.AreEqual(FileUtilities.FixFilePath(@"\\server\share\directory\"), FileUtilities.GetDirectory(@"\\server\share\directory\"));
            Assert.AreEqual(FileUtilities.FixFilePath(@"foo\"), FileUtilities.GetDirectory(@"foo\bar"));
            Assert.AreEqual(FileUtilities.FixFilePath(@"\foo\bar\"), FileUtilities.GetDirectory(@"\foo\bar\"));
            Assert.AreEqual(String.Empty, FileUtilities.GetDirectory("foo"));
        }

        /// <summary>
        /// Exercises FileUtilities.HasExtension
        /// </summary>
        [Test]
        public void HasExtension()
        {
            Assert.IsTrue(FileUtilities.HasExtension("foo.txt", new string[] { ".EXE", ".TXT" }), "test 1");
            Assert.IsFalse(FileUtilities.HasExtension("foo.txt", new string[] { ".EXE", ".DLL" }), "test 2");
        }

        /// <summary>
        /// Exercises FileUtilities.EnsureTrailingSlash
        /// </summary>
        [Test]
        public void EnsureTrailingSlash()
        {
            // Doesn't have a trailing slash to start with.
            Assert.AreEqual(FileUtilities.FixFilePath(@"foo\bar\"), FileUtilities.EnsureTrailingSlash(@"foo\bar"), "test 1");
            Assert.AreEqual(FileUtilities.FixFilePath(@"foo/bar\"), FileUtilities.EnsureTrailingSlash(@"foo/bar"), "test 2");

            // Already has a trailing slash to start with.
            Assert.AreEqual(FileUtilities.FixFilePath(@"foo/bar/"), FileUtilities.EnsureTrailingSlash(@"foo/bar/"), "test 3");
            Assert.AreEqual(FileUtilities.FixFilePath(@"foo\bar\"), FileUtilities.EnsureTrailingSlash(@"foo\bar\"), "test 4");
            Assert.AreEqual(FileUtilities.FixFilePath(@"foo/bar\"), FileUtilities.EnsureTrailingSlash(@"foo/bar\"), "test 5");
            Assert.AreEqual(FileUtilities.FixFilePath(@"foo\bar/"), FileUtilities.EnsureTrailingSlash(@"foo\bar/"), "test 5");
        }

        /// <summary>
        /// Exercises FileUtilities.ItemSpecModifiers.IsItemSpecModifier
        /// </summary>
        [Test]
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

        [Test]
        public void CheckDerivableItemSpecModifiers()
        {
            Assert.IsTrue(FileUtilities.ItemSpecModifiers.IsDerivableItemSpecModifier("Filename"));
            Assert.IsFalse(FileUtilities.ItemSpecModifiers.IsDerivableItemSpecModifier("RecursiveDir"));
            Assert.IsFalse(FileUtilities.ItemSpecModifiers.IsDerivableItemSpecModifier("recursivedir"));
        }

        [Test]
        public void GetExecutablePath()
        {
            string path;

            // If FileUtilities knows we are running tests, it will return the assembly path, not the
            // module path
            if (FileUtilities.RunningTests)
            {
                path =
                    Path.Combine(
                        Path.GetDirectoryName(FileUtilities.ExecutingAssemblyPath)
                            .TrimEnd(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }),
                        "MSBuild.exe");
            }
            else
            {
                StringBuilder sb = new StringBuilder(NativeMethodsShared.MAX_PATH);
                NativeMethodsShared.GetModuleFileName(NativeMethodsShared.NullHandleRef, sb, sb.Capacity);
                path = sb.ToString();
            }

            string configPath = FileUtilities.CurrentExecutableConfigurationFilePath;
            string directoryName = FileUtilities.CurrentExecutableDirectory;
            string executablePath = FileUtilities.CurrentExecutablePath;
            Assert.IsTrue(string.Compare(configPath, executablePath + ".config", StringComparison.OrdinalIgnoreCase) == 0);
            Assert.IsTrue(string.Compare(path, executablePath, StringComparison.OrdinalIgnoreCase) == 0);
            Assert.IsTrue(string.Compare(directoryName, Path.GetDirectoryName(path), StringComparison.OrdinalIgnoreCase) == 0);
        }

        [Test]
        public void NormalizePathThatFitsIntoMaxPath()
        {
            string currentDirectory = @"c:\aardvark\aardvark\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890";
            string filePath = @"..\..\..\..\..\..\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\a.cs";
            string fullPath = @"c:\aardvark\aardvark\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\a.cs";

            Assert.AreEqual(fullPath, FileUtilities.NormalizePath(Path.Combine(currentDirectory, filePath)));
        }

        [Test]
        [ExpectedException(typeof(PathTooLongException))]
        public void NormalizePathThatDoesntFitIntoMaxPath()
        {
            string currentDirectory = @"c:\aardvark\aardvark\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890";
            string filePath = @"..\..\..\..\..\..\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\a.cs";

            // This path ends up over 420 characters long
            string fullPath = @"c:\aardvark\aardvark\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\a.cs";

            Assert.AreEqual(fullPath, FileUtilities.NormalizePath(Path.Combine(currentDirectory, filePath)));
        }

        [Test]
        public void GetItemSpecModifierRootDirThatFitsIntoMaxPath()
        {
            string currentDirectory = @"c:\aardvark\aardvark\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890";
            string fullPath = @"c:\aardvark\aardvark\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\a.cs";
            string cache = fullPath;

            Assert.AreEqual(@"c:\", FileUtilities.ItemSpecModifiers.GetItemSpecModifier(currentDirectory, fullPath, String.Empty, FileUtilities.ItemSpecModifiers.RootDir, ref cache));
        }

        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void NormalizePathNull()
        {
            Assert.AreEqual(null, FileUtilities.NormalizePath(null));
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void NormalizePathEmpty()
        {
            Assert.AreEqual(null, FileUtilities.NormalizePath(String.Empty));
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void NormalizePathBadUNC1()
        {
            Assert.AreEqual(null, FileUtilities.NormalizePath(@"\\"));
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void NormalizePathBadUNC2()
        {
            Assert.AreEqual(null, FileUtilities.NormalizePath(@"\\XXX\"));
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void NormalizePathBadUNC3()
        {
            Assert.AreEqual(@"\\localhost", FileUtilities.NormalizePath(@"\\localhost"));
        }

        [Test]
        public void NormalizePathGoodUNC()
        {
            Assert.AreEqual(@"\\localhost\share", FileUtilities.NormalizePath(@"\\localhost\share"));
        }

        [Test]
        public void NormalizePathTooLongWithDots()
        {
            string longPart = new string('x', 300);
            Assert.AreEqual(@"c:\abc\def", FileUtilities.NormalizePath(@"c:\abc\" + longPart + @"\..\def"));
        }

        [Test]
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

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void NormalizePathInvalid()
        {
            string filePath = @"c:\aardvark\|||";
            Assert.AreEqual(null, FileUtilities.NormalizePath(filePath));
        }

        [Test]
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

        [Test]
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

        [Test]
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

        [Test]
        public void DirectoryExistsNoThrowTooLongWithDots()
        {
            string path = Path.Combine(Environment.SystemDirectory, "..", "..", "..") + Path.DirectorySeparatorChar;
            if (NativeMethodsShared.IsWindows)
            {
                path += Environment.SystemDirectory.Substring(3);
            }

            int length = path.Length;

            string longPart = new string('x', 260 - length); // We want the shortest that is > max path.

            string inputPath = Path.Combine(new[] { Environment.SystemDirectory, longPart, "..", "..", ".." })
                               + Path.DirectorySeparatorChar;
            if (NativeMethodsShared.IsWindows)
            {
                path += Environment.SystemDirectory.Substring(3);
            }

            Console.WriteLine(inputPath.Length);

            // "c:\windows\system32\<verylong>\..\..\windows\system32" exists
            Assert.AreEqual(true, FileUtilities.DirectoryExistsNoThrow(inputPath));
        }

        [Test]
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

        [Test]
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

        [Test]
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

        [Test]
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

        [Test]
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
        [Test]
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
        [Test]
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
        [Test]
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
        [Test]
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
        [Test]
        [ExpectedException(typeof(IOException))]
        public void GenerateTempBatchFileWithBadExtension()
        {
            Shared.FileUtilities.GetTemporaryFile("|");
        }

        /// <summary>
        /// No extension is given
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void GenerateTempBatchFileWithEmptyExtension()
        {
            Shared.FileUtilities.GetTemporaryFile(String.Empty);
        }

        /// <summary>
        /// Directory is invalid
        /// </summary>
        [Test]
        [ExpectedException(typeof(IOException))]
        public void GenerateTempBatchFileWithBadDirectory()
        {
            Shared.FileUtilities.GetTemporaryFile("|", ".tmp");
        }
    }
}
