// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class FileUtilities_Tests
    {
        /// <summary>
        /// Exercises FileUtilities.ItemSpecModifiers.GetItemSpecModifier
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void GetItemSpecModifier()
        {
            TestGetItemSpecModifier(Directory.GetCurrentDirectory());
            TestGetItemSpecModifier(null);
        }

        private static void TestGetItemSpecModifier(string currentDirectory)
        {
            string cache = null;
            string modifier = FileUtilities.ItemSpecModifiers.GetItemSpecModifier(currentDirectory, "foo", String.Empty, FileUtilities.ItemSpecModifiers.RecursiveDir, ref cache);
            Assert.Equal(String.Empty, modifier);

            cache = null;
            modifier = FileUtilities.ItemSpecModifiers.GetItemSpecModifier(currentDirectory, "foo", String.Empty, FileUtilities.ItemSpecModifiers.ModifiedTime, ref cache);
            Assert.Equal(String.Empty, modifier);

            cache = null;
            modifier = FileUtilities.ItemSpecModifiers.GetItemSpecModifier(currentDirectory, @"foo\goo", String.Empty, FileUtilities.ItemSpecModifiers.RelativeDir, ref cache);
            Assert.Equal(@"foo" + Path.DirectorySeparatorChar, modifier);

            // confirm we get the same thing back the second time
            modifier = FileUtilities.ItemSpecModifiers.GetItemSpecModifier(currentDirectory, @"foo\goo", String.Empty, FileUtilities.ItemSpecModifiers.RelativeDir, ref cache);
            Assert.Equal(@"foo" + Path.DirectorySeparatorChar, modifier);

            cache = null;
            string itemSpec = NativeMethodsShared.IsWindows ? @"c:\foo.txt" : "/foo.txt";
            string itemSpecDir = NativeMethodsShared.IsWindows ? @"c:\" : "/";
            modifier = FileUtilities.ItemSpecModifiers.GetItemSpecModifier(currentDirectory, itemSpec, String.Empty, FileUtilities.ItemSpecModifiers.FullPath, ref cache);
            Assert.Equal(itemSpec, modifier);
            Assert.Equal(itemSpec, cache);

            modifier = FileUtilities.ItemSpecModifiers.GetItemSpecModifier(currentDirectory, itemSpec, String.Empty, FileUtilities.ItemSpecModifiers.RootDir, ref cache);
            Assert.Equal(itemSpecDir, modifier);

            modifier = FileUtilities.ItemSpecModifiers.GetItemSpecModifier(currentDirectory, itemSpec, String.Empty, FileUtilities.ItemSpecModifiers.Filename, ref cache);
            Assert.Equal(@"foo", modifier);

            modifier = FileUtilities.ItemSpecModifiers.GetItemSpecModifier(currentDirectory, itemSpec, String.Empty, FileUtilities.ItemSpecModifiers.Extension, ref cache);
            Assert.Equal(@".txt", modifier);

            modifier = FileUtilities.ItemSpecModifiers.GetItemSpecModifier(currentDirectory, itemSpec, String.Empty, FileUtilities.ItemSpecModifiers.Directory, ref cache);
            Assert.Equal(String.Empty, modifier);

            modifier = FileUtilities.ItemSpecModifiers.GetItemSpecModifier(currentDirectory, itemSpec, String.Empty, FileUtilities.ItemSpecModifiers.Identity, ref cache);
            Assert.Equal(itemSpec, modifier);

            string projectPath = NativeMethodsShared.IsWindows ? @"c:\abc\goo.proj" : @"/abc/goo.proj";
            string projectPathDir = NativeMethodsShared.IsWindows ? @"c:\abc\" : @"/abc/";
            modifier = FileUtilities.ItemSpecModifiers.GetItemSpecModifier(currentDirectory, itemSpec, projectPath, FileUtilities.ItemSpecModifiers.DefiningProjectDirectory, ref cache);
            Assert.Equal(projectPathDir, modifier);

            modifier = FileUtilities.ItemSpecModifiers.GetItemSpecModifier(currentDirectory, itemSpec, projectPath, FileUtilities.ItemSpecModifiers.DefiningProjectExtension, ref cache);
            Assert.Equal(@".proj", modifier);

            modifier = FileUtilities.ItemSpecModifiers.GetItemSpecModifier(currentDirectory, itemSpec, projectPath, FileUtilities.ItemSpecModifiers.DefiningProjectFullPath, ref cache);
            Assert.Equal(projectPath, modifier);

            modifier = FileUtilities.ItemSpecModifiers.GetItemSpecModifier(currentDirectory, itemSpec, projectPath, FileUtilities.ItemSpecModifiers.DefiningProjectName, ref cache);
            Assert.Equal(@"goo", modifier);
        }

        [Fact]
        [Trait("Category", "mono-osx-failing")]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void MakeRelativeTests()
        {
            if (NativeMethodsShared.IsWindows)
            {
                Assert.Equal(@"foo.cpp", FileUtilities.MakeRelative(@"c:\abc\def", @"c:\abc\def\foo.cpp"));
                Assert.Equal(@"def\foo.cpp", FileUtilities.MakeRelative(@"c:\abc\", @"c:\abc\def\foo.cpp"));
                Assert.Equal(@"..\foo.cpp", FileUtilities.MakeRelative(@"c:\abc\def\xyz", @"c:\abc\def\foo.cpp"));
                Assert.Equal(@"..\ttt\foo.cpp", FileUtilities.MakeRelative(@"c:\abc\def\xyz\", @"c:\abc\def\ttt\foo.cpp"));
                Assert.Equal(@"e:\abc\def\foo.cpp", FileUtilities.MakeRelative(@"c:\abc\def", @"e:\abc\def\foo.cpp"));
                Assert.Equal(@"foo.cpp", FileUtilities.MakeRelative(@"\\aaa\abc\def", @"\\aaa\abc\def\foo.cpp"));
                Assert.Equal(@"foo.cpp", FileUtilities.MakeRelative(@"c:\abc\def", @"foo.cpp"));
                Assert.Equal(@"foo.cpp", FileUtilities.MakeRelative(@"c:\abc\def", @"..\def\foo.cpp"));
                Assert.Equal(@"\\host\path\file", FileUtilities.MakeRelative(@"c:\abc\def", @"\\host\path\file"));
                Assert.Equal(@"\\host\d$\file", FileUtilities.MakeRelative(@"c:\abc\def", @"\\host\d$\file"));
                Assert.Equal(@"..\fff\ggg.hh", FileUtilities.MakeRelative(@"c:\foo\bar\..\abc\cde", @"c:\foo\bar\..\abc\fff\ggg.hh"));
            }
            else
            {
                Assert.Equal(@"foo.cpp", FileUtilities.MakeRelative(@"/abc/def", @"/abc/def/foo.cpp"));
                Assert.Equal(@"def/foo.cpp", FileUtilities.MakeRelative(@"/abc/", @"/abc/def/foo.cpp"));
                Assert.Equal(@"..\foo.cpp", FileUtilities.MakeRelative(@"/abc/def/xyz", @"/abc/def/foo.cpp"));
                Assert.Equal(@"..\ttt\foo.cpp", FileUtilities.MakeRelative(@"/abc/def/xyz/", @"/abc/def/ttt/foo.cpp"));
                Assert.Equal(@"/abc/def/foo.cpp", FileUtilities.MakeRelative(@"/abc/def", @"/abc/def/foo.cpp"));
                Assert.Equal(@"foo.cpp", FileUtilities.MakeRelative(@"/abc/def", @"foo.cpp"));
                Assert.Equal(@"foo.cpp", FileUtilities.MakeRelative(@"/abc/def", @"../def/foo.cpp"));
                Assert.Equal(@"../fff/ggg.hh", FileUtilities.MakeRelative(@"/foo/bar/../abc/cde", @"/foo/bar/../abc/fff/ggg.hh"));
            }
        }

        /// <summary>
        /// Exercises FileUtilities.ItemSpecModifiers.GetItemSpecModifier on a bad path.
        /// </summary>
        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)] // On Unix there no invalid file name characters
        [SkipOnTargetFramework(TargetFrameworkMonikers.Netcoreapp, ".NET Core 2.1+ no longer validates paths: https://github.com/dotnet/corefx/issues/27779#issuecomment-371253486")]
        public void GetItemSpecModifierOnBadPath()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                TestGetItemSpecModifierOnBadPath(Directory.GetCurrentDirectory());
            }
           );
        }
        /// <summary>
        /// Exercises FileUtilities.ItemSpecModifiers.GetItemSpecModifier on a bad path.
        /// </summary>
        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)] // On Unix there no invalid file name characters
        [SkipOnTargetFramework(TargetFrameworkMonikers.Netcoreapp, ".NET Core 2.1+ no longer validates paths: https://github.com/dotnet/corefx/issues/27779#issuecomment-371253486")]
        public void GetItemSpecModifierOnBadPath2()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                TestGetItemSpecModifierOnBadPath(null);
            }
           );
        }

        private static void TestGetItemSpecModifierOnBadPath(string currentDirectory)
        {
            try
            {
                string cache = null;
                FileUtilities.ItemSpecModifiers.GetItemSpecModifier(currentDirectory, @"http://www.microsoft.com", String.Empty, FileUtilities.ItemSpecModifiers.RootDir, ref cache);
            }
            catch (Exception e)
            {
                // so I can see the exception message in NUnit's "Standard Out" window
                Console.WriteLine(e.Message);
                throw;
            }
        }

        [Fact]
        public void GetFileInfoNoThrowBasic()
        {
            string file = null;
            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = FileUtilities.GetFileInfoNoThrow(file);
                Assert.Equal(info.LastWriteTime, new FileInfo(file).LastWriteTime);
            }
            finally
            {
                if (file != null) File.Delete(file);
            }
        }

        [Fact]
        public void GetFileInfoNoThrowNonexistent()
        {
            FileInfo info = FileUtilities.GetFileInfoNoThrow("this_file_is_nonexistent");
            Assert.Null(info);
        }

        /// <summary>
        /// Exercises FileUtilities.EndsWithSlash
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void EndsWithSlash()
        {
            Assert.True(FileUtilities.EndsWithSlash(@"C:\foo\"));
            Assert.True(FileUtilities.EndsWithSlash(@"C:\"));
            Assert.True(FileUtilities.EndsWithSlash(@"\"));

            Assert.True(FileUtilities.EndsWithSlash(@"http://www.microsoft.com/"));
            Assert.True(FileUtilities.EndsWithSlash(@"//server/share/"));
            Assert.True(FileUtilities.EndsWithSlash(@"/"));

            Assert.False(FileUtilities.EndsWithSlash(@"C:\foo"));
            Assert.False(FileUtilities.EndsWithSlash(@"C:"));
            Assert.False(FileUtilities.EndsWithSlash(@"foo"));

            // confirm that empty string doesn't barf
            Assert.False(FileUtilities.EndsWithSlash(String.Empty));
        }

        /// <summary>
        /// Exercises FileUtilities.GetDirectory
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void GetDirectoryWithTrailingSlash()
        {
            Assert.Equal(NativeMethodsShared.IsWindows ? @"c:\" : "/", FileUtilities.GetDirectory(NativeMethodsShared.IsWindows ? @"c:\" : "/"));
            Assert.Equal(NativeMethodsShared.IsWindows ? @"c:\" : "/", FileUtilities.GetDirectory(NativeMethodsShared.IsWindows ? @"c:\foo" : "/foo"));
            Assert.Equal(NativeMethodsShared.IsWindows ? @"c:" : "/", FileUtilities.GetDirectory(NativeMethodsShared.IsWindows ? @"c:" : "/"));
            Assert.Equal(FileUtilities.FixFilePath(@"\"), FileUtilities.GetDirectory(@"\"));
            Assert.Equal(FileUtilities.FixFilePath(@"\"), FileUtilities.GetDirectory(@"\foo"));
            Assert.Equal(FileUtilities.FixFilePath(@"..\"), FileUtilities.GetDirectory(@"..\foo"));
            Assert.Equal(FileUtilities.FixFilePath(@"\foo\"), FileUtilities.GetDirectory(@"\foo\"));
            Assert.Equal(FileUtilities.FixFilePath(@"\\server\share"), FileUtilities.GetDirectory(@"\\server\share"));
            Assert.Equal(FileUtilities.FixFilePath(@"\\server\share\"), FileUtilities.GetDirectory(@"\\server\share\"));
            Assert.Equal(FileUtilities.FixFilePath(@"\\server\share\"), FileUtilities.GetDirectory(@"\\server\share\file"));
            Assert.Equal(FileUtilities.FixFilePath(@"\\server\share\directory\"), FileUtilities.GetDirectory(@"\\server\share\directory\"));
            Assert.Equal(FileUtilities.FixFilePath(@"foo\"), FileUtilities.GetDirectory(@"foo\bar"));
            Assert.Equal(FileUtilities.FixFilePath(@"\foo\bar\"), FileUtilities.GetDirectory(@"\foo\bar\"));
            Assert.Equal(String.Empty, FileUtilities.GetDirectory("foo"));
        }

        [Theory]
        [InlineData("foo.txt",      new[] { ".txt" })]
        [InlineData("foo.txt",      new[] { ".TXT" })]
        [InlineData("foo.txt",      new[] { ".EXE", ".TXT" })]
        public void HasExtension_WhenFileNameHasExtension_ReturnsTrue(string fileName, string[] allowedExtensions)
        {
            var result = FileUtilities.HasExtension(fileName, allowedExtensions);

            if (!FileUtilities.GetIsFileSystemCaseSensitive() || allowedExtensions.Any(extension => fileName.Contains(extension)))
            {
                result.ShouldBeTrue();
            }
        }

        [Theory]
        [InlineData("foo.txt",      new[] { ".DLL" })]
        [InlineData("foo.txt",      new[] { ".EXE", ".DLL" })]
        [InlineData("foo.exec",     new[] { ".exe", })]
        [InlineData("foo.exe",      new[] { ".exec", })]
        [InlineData("foo",          new[] { ".exe", })]
        [InlineData("",             new[] { ".exe" })]
        [InlineData(null,           new[] { ".exe" })]
        public void HasExtension_WhenFileNameDoesNotHaveExtension_ReturnsFalse(string fileName, string[] allowedExtensions)
        {
            var result = FileUtilities.HasExtension(fileName, allowedExtensions);

            Assert.False(result);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        [SkipOnTargetFramework(TargetFrameworkMonikers.Netcoreapp)]
        public void HasExtension_WhenInvalidFileName_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                FileUtilities.HasExtension("|/", new[] { ".exe" });
            });
        }

        [Fact]
        public void HasExtension_UsesOrdinalIgnoreCase()
        {
            var currentCulture = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("tr-TR"); // Turkish

                var result = FileUtilities.HasExtension("foo.ini", new string[] { ".INI" });

                result.ShouldBe(!FileUtilities.GetIsFileSystemCaseSensitive());
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = currentCulture;
            }
        }

        /// <summary>
        /// Exercises FileUtilities.EnsureTrailingSlash
        /// </summary>
        [Fact]
        public void EnsureTrailingSlash()
        {
            // Doesn't have a trailing slash to start with.
            Assert.Equal(FileUtilities.FixFilePath(@"foo\bar\"), FileUtilities.EnsureTrailingSlash(@"foo\bar")); // "test 1"
            Assert.Equal(FileUtilities.FixFilePath(@"foo/bar\"), FileUtilities.EnsureTrailingSlash(@"foo/bar")); // "test 2"

            // Already has a trailing slash to start with.
            Assert.Equal(FileUtilities.FixFilePath(@"foo/bar/"), FileUtilities.EnsureTrailingSlash(@"foo/bar/")); //test 3"
            Assert.Equal(FileUtilities.FixFilePath(@"foo\bar\"), FileUtilities.EnsureTrailingSlash(@"foo\bar\")); //test 4"
            Assert.Equal(FileUtilities.FixFilePath(@"foo/bar\"), FileUtilities.EnsureTrailingSlash(@"foo/bar\")); //test 5"
            Assert.Equal(FileUtilities.FixFilePath(@"foo\bar/"), FileUtilities.EnsureTrailingSlash(@"foo\bar/")); //"test 5"
        }

        /// <summary>
        /// Exercises FileUtilities.ItemSpecModifiers.IsItemSpecModifier
        /// </summary>
        [Fact]
        public void IsItemSpecModifier()
        {
            // Positive matches using exact case.
            Assert.True(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("FullPath")); // "test 1"
            Assert.True(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("RootDir")); // "test 2"
            Assert.True(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("Filename")); // "test 3"
            Assert.True(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("Extension")); // "test 4"
            Assert.True(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("RelativeDir")); // "test 5"
            Assert.True(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("Directory")); // "test 6"
            Assert.True(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("RecursiveDir")); // "test 7"
            Assert.True(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("Identity")); // "test 8"
            Assert.True(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("ModifiedTime")); // "test 9"
            Assert.True(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("CreatedTime")); // "test 10"
            Assert.True(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("AccessedTime")); // "test 11"

            // Positive matches using different case.
            Assert.True(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("fullPath")); // "test 21"
            Assert.True(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("rootDir")); // "test 22"
            Assert.True(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("filename")); // "test 23"
            Assert.True(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("extension")); // "test 24"
            Assert.True(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("relativeDir")); // "test 25"
            Assert.True(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("directory")); // "test 26"
            Assert.True(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("recursiveDir")); // "test 27"
            Assert.True(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("identity")); // "test 28"
            Assert.True(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("modifiedTime")); // "test 29"
            Assert.True(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("createdTime")); // "test 30"
            Assert.True(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("accessedTime")); // "test 31"

            // Negative tests to get maximum code coverage inside the many different branches
            // of FileUtilities.ItemSpecModifiers.IsItemSpecModifier.
            Assert.False(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("rootxxx")); // "test 41"
            Assert.False(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("Rootxxx")); // "test 42"
            Assert.False(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("xxxxxxx")); // "test 43"

            Assert.False(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("filexxxx")); // "test 44"
            Assert.False(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("Filexxxx")); // "test 45"
            Assert.False(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("idenxxxx")); // "test 46"
            Assert.False(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("Idenxxxx")); // "test 47"
            Assert.False(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("xxxxxxxx")); // "test 48"

            Assert.False(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("extenxxxx")); // "test 49"
            Assert.False(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("Extenxxxx")); // "test 50"
            Assert.False(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("direcxxxx")); // "test 51"
            Assert.False(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("Direcxxxx")); // "test 52"
            Assert.False(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("xxxxxxxxx")); // "test 53"

            Assert.False(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("xxxxxxxxxx")); // "test 54"

            Assert.False(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("relativexxx")); // "test 55"
            Assert.False(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("Relativexxx")); // "test 56"
            Assert.False(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("createdxxxx")); // "test 57"
            Assert.False(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("Createdxxxx")); // "test 58"
            Assert.False(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("xxxxxxxxxxx")); // "test 59"

            Assert.False(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("recursivexxx")); // "test 60"
            Assert.False(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("Recursivexxx")); // "test 61"
            Assert.False(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("accessedxxxx")); // "test 62"
            Assert.False(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("Accessedxxxx")); // "test 63"
            Assert.False(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("modifiedxxxx")); // "test 64"
            Assert.False(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("Modifiedxxxx")); // "test 65"
            Assert.False(FileUtilities.ItemSpecModifiers.IsItemSpecModifier("xxxxxxxxxxxx")); // "test 66"

            Assert.False(FileUtilities.ItemSpecModifiers.IsItemSpecModifier(null)); // "test 67"
        }

        [Fact]
        public void CheckDerivableItemSpecModifiers()
        {
            Assert.True(FileUtilities.ItemSpecModifiers.IsDerivableItemSpecModifier("Filename"));
            Assert.False(FileUtilities.ItemSpecModifiers.IsDerivableItemSpecModifier("RecursiveDir"));
            Assert.False(FileUtilities.ItemSpecModifiers.IsDerivableItemSpecModifier("recursivedir"));
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void NormalizePathThatFitsIntoMaxPath()
        {
            string currentDirectory = @"c:\aardvark\aardvark\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890";
            string filePath = @"..\..\..\..\..\..\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\a.cs";
            string fullPath = @"c:\aardvark\aardvark\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\a.cs";

            Assert.Equal(fullPath, FileUtilities.NormalizePath(Path.Combine(currentDirectory, filePath)));
        }

        [ConditionalFact(typeof(NativeMethodsShared), nameof(NativeMethodsShared.IsMaxPathLegacyWindows))]
        [PlatformSpecific(TestPlatforms.Windows)]
        [SkipOnTargetFramework(TargetFrameworkMonikers.Netcoreapp, "https://github.com/microsoft/msbuild/issues/4363")]
        public void NormalizePathThatDoesntFitIntoMaxPath()
        {
            Assert.Throws<PathTooLongException>(() =>
            {
                string currentDirectory = @"c:\aardvark\aardvark\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890";
                string filePath = @"..\..\..\..\..\..\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\a.cs";

                // This path ends up over 420 characters long
                string fullPath = @"c:\aardvark\aardvark\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\a.cs";

                Assert.Equal(fullPath, FileUtilities.NormalizePath(Path.Combine(currentDirectory, filePath)));
            }
           );
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void GetItemSpecModifierRootDirThatFitsIntoMaxPath()
        {
            string currentDirectory = @"c:\aardvark\aardvark\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890";
            string fullPath = @"c:\aardvark\aardvark\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\a.cs";
            string cache = fullPath;

            Assert.Equal(@"c:\", FileUtilities.ItemSpecModifiers.GetItemSpecModifier(currentDirectory, fullPath, String.Empty, FileUtilities.ItemSpecModifiers.RootDir, ref cache));
        }

        [Fact]
        public void NormalizePathNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                Assert.Null(FileUtilities.NormalizePath(null, null));
            }
           );
        }

        [Fact]
        public void NormalizePathEmpty()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                Assert.Null(FileUtilities.NormalizePath(String.Empty));
            }
           );
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        [SkipOnTargetFramework(TargetFrameworkMonikers.Netcoreapp, ".NET Core 2.1+ no longer validates paths: https://github.com/dotnet/corefx/issues/27779#issuecomment-371253486")]
        public void NormalizePathBadUNC1()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                Assert.Null(FileUtilities.NormalizePath(@"\\"));
            }
           );
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        [SkipOnTargetFramework(TargetFrameworkMonikers.Netcoreapp, ".NET Core 2.1+ no longer validates paths: https://github.com/dotnet/corefx/issues/27779#issuecomment-371253486")]
        public void NormalizePathBadUNC2()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                Assert.Null(FileUtilities.NormalizePath(@"\\XXX\"));
            }
           );
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        [SkipOnTargetFramework(TargetFrameworkMonikers.Netcoreapp, ".NET Core 2.1+ no longer validates paths: https://github.com/dotnet/corefx/issues/27779#issuecomment-371253486")]
        public void NormalizePathBadUNC3()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                Assert.Equal(@"\\localhost", FileUtilities.NormalizePath(@"\\localhost"));
            }
           );
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void NormalizePathGoodUNC()
        {
            Assert.Equal(@"\\localhost\share", FileUtilities.NormalizePath(@"\\localhost\share"));
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void NormalizePathTooLongWithDots()
        {
            string longPart = new string('x', 300);
            Assert.Equal(@"c:\abc\def", FileUtilities.NormalizePath(@"c:\abc\" + longPart + @"\..\def"));
        }

#if FEATURE_LEGACY_GETFULLPATH
        [Fact(Skip="https://github.com/Microsoft/msbuild/issues/4205")]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void NormalizePathBadGlobalroot()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                /*
                 From Path.cs
                   // Check for \\?\Globalroot, an internal mechanism to the kernel
                   // that provides aliases for drives and other undocumented stuff.
                   // The kernel team won't even describe the full set of what
                   // is available here - we don't want managed apps mucking
                   // with this for security reasons.
                 * */
                Assert.Null(FileUtilities.NormalizePath(@"\\?\globalroot\XXX"));
            }
           );
        }
#endif

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        [SkipOnTargetFramework(TargetFrameworkMonikers.Netcoreapp, ".NET Core 2.1+ no longer validates paths: https://github.com/dotnet/corefx/issues/27779#issuecomment-371253486")]
        public void NormalizePathInvalid()
        {
            string filePath = @"c:\aardvark\|||";

            Assert.Throws<ArgumentException>(() =>
            {
                FileUtilities.NormalizePath(filePath);
            });
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void CannotNormalizePathWithNewLineAndSpace()
        {
            string filePath = "\r\n      C:\\work\\sdk3\\artifacts\\tmp\\Debug\\SimpleNamesWi---6143883E\\NETFrameworkLibrary\\bin\\Debug\\net462\\NETFrameworkLibrary.dll\r\n      ";

#if FEATURE_LEGACY_GETFULLPATH
            Assert.Throws<ArgumentException>(() => FileUtilities.NormalizePath(filePath));
#else
            Assert.NotEqual("C:\\work\\sdk3\\artifacts\\tmp\\Debug\\SimpleNamesWi---6143883E\\NETFrameworkLibrary\\bin\\Debug\\net462\\NETFrameworkLibrary.dll", FileUtilities.NormalizePath(filePath));
#endif
        }

        [Fact]
        public void FileOrDirectoryExistsNoThrow()
        {
            var isWindows = NativeMethodsShared.IsWindows;

            Assert.False(FileUtilities.FileOrDirectoryExistsNoThrow("||"));
            Assert.False(FileUtilities.FileOrDirectoryExistsNoThrow(isWindows ? @"c:\doesnot_exist" : "/doesnot_exist"));
            Assert.True(FileUtilities.FileOrDirectoryExistsNoThrow(isWindows ? @"c:\" : "/"));
            Assert.True(FileUtilities.FileOrDirectoryExistsNoThrow(Path.GetTempPath()));

            string path = null;

            try
            {
                path = FileUtilities.GetTemporaryFile();
                Assert.True(FileUtilities.FileOrDirectoryExistsNoThrow(path));
            }
            finally
            {
                File.Delete(path);
            }
        }

#if FEATURE_ENVIRONMENT_SYSTEMDIRECTORY
        // These tests will need to be redesigned for Linux

        [ConditionalFact(nameof(RunTestsThatDependOnWindowsShortPathBehavior_Workaround4241))]
        [Trait("Category", "mono-osx-failing")]
        public void FileOrDirectoryExistsNoThrowTooLongWithDots()
        {
            int length = (Environment.SystemDirectory + @"\" + @"\..\..\..\" + Environment.SystemDirectory.Substring(3)).Length;

            string longPart = new string('x', 260 - length); // We want the shortest that is > max path.

            string inputPath = Environment.SystemDirectory + @"\" + longPart + @"\..\..\..\" + Environment.SystemDirectory.Substring(3);
            Console.WriteLine(inputPath.Length);

            // "c:\windows\system32\<verylong>\..\..\windows\system32" exists
            Assert.True(FileUtilities.FileOrDirectoryExistsNoThrow(inputPath));
            Assert.False(FileUtilities.FileOrDirectoryExistsNoThrow(inputPath.Replace('\\', 'X')));
        }

        [ConditionalFact(nameof(RunTestsThatDependOnWindowsShortPathBehavior_Workaround4241))]
        [Trait("Category", "mono-osx-failing")]
        public void FileOrDirectoryExistsNoThrowTooLongWithDotsRelative()
        {
            int length = (Environment.SystemDirectory + @"\" + @"\..\..\..\" + Environment.SystemDirectory.Substring(3)).Length;

            string longPart = new string('x', 260 - length); // We want the shortest that is > max path.

            string inputPath = longPart + @"\..\..\..\" + Environment.SystemDirectory.Substring(3);
            Console.WriteLine(inputPath.Length);

            // "c:\windows\system32\<verylong>\..\..\windows\system32" exists

            string currentDirectory = Directory.GetCurrentDirectory();

            try
            {
                Directory.SetCurrentDirectory(Environment.SystemDirectory);

                Assert.True(FileUtilities.FileOrDirectoryExistsNoThrow(inputPath));
                Assert.False(FileUtilities.FileOrDirectoryExistsNoThrow(inputPath.Replace('\\', 'X')));
            }
            finally
            {
                Directory.SetCurrentDirectory(currentDirectory);
            }
        }

        [Fact]
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
            Assert.True(FileUtilities.DirectoryExistsNoThrow(inputPath));
        }

        [ConditionalFact(nameof(RunTestsThatDependOnWindowsShortPathBehavior_Workaround4241))]
        [Trait("Category", "mono-osx-failing")]
        public void DirectoryExistsNoThrowTooLongWithDotsRelative()
        {
            int length = (Environment.SystemDirectory + @"\" + @"\..\..\..\" + Environment.SystemDirectory.Substring(3)).Length;

            string longPart = new string('x', 260 - length); // We want the shortest that is > max path.

            string inputPath = longPart + @"\..\..\..\" + Environment.SystemDirectory.Substring(3);
            Console.WriteLine(inputPath.Length);

            // "c:\windows\system32\<verylong>\..\..\..\windows\system32" exists

            string currentDirectory = Directory.GetCurrentDirectory();

            try
            {
                Directory.SetCurrentDirectory(Environment.SystemDirectory);

                FileUtilities.DirectoryExistsNoThrow(inputPath).ShouldBeTrue();
                FileUtilities.DirectoryExistsNoThrow(inputPath.Replace('\\', 'X')).ShouldBeFalse();
            }
            finally
            {
                Directory.SetCurrentDirectory(currentDirectory);
            }
        }

        public static bool RunTestsThatDependOnWindowsShortPathBehavior_Workaround4241()
        {
            // Run these tests only when we're not on Windows
            return !NativeMethodsShared.IsWindows ||
            // OR we're on Windows and long paths aren't enabled
            // https://github.com/Microsoft/msbuild/issues/4241
                   NativeMethodsShared.IsMaxPathLegacyWindows();
        }

        [ConditionalFact(nameof(RunTestsThatDependOnWindowsShortPathBehavior_Workaround4241))]
        [Trait("Category", "mono-osx-failing")]
        public void FileExistsNoThrowTooLongWithDots()
        {
            int length = (Environment.SystemDirectory + @"\" + @"\..\..\..\" + Environment.SystemDirectory.Substring(3) + @"\..\explorer.exe").Length;

            string longPart = new string('x', 260 - length); // We want the shortest that is > max path.

            string inputPath = Environment.SystemDirectory + @"\" + longPart + @"\..\..\..\" + Environment.SystemDirectory.Substring(3) + @"\..\explorer.exe";
            Console.WriteLine(inputPath.Length);
            Console.WriteLine(inputPath);

            // "c:\windows\system32\<verylong>\..\..\windows\system32" exists
            Assert.True(FileUtilities.FileExistsNoThrow(inputPath));
        }

        [ConditionalFact(nameof(RunTestsThatDependOnWindowsShortPathBehavior_Workaround4241))]
        [Trait("Category", "mono-osx-failing")]
        public void FileExistsNoThrowTooLongWithDotsRelative()
        {
            int length = (Environment.SystemDirectory + @"\" + @"\..\..\..\" + Environment.SystemDirectory.Substring(3) + @"\..\explorer.exe").Length;

            string longPart = new string('x', 260 - length); // We want the shortest that is > max path.

            string inputPath = longPart + @"\..\..\..\" + Environment.SystemDirectory.Substring(3) + @"\..\explorer.exe";
            Console.WriteLine(inputPath.Length);

            // "c:\windows\system32\<verylong>\..\..\windows\system32" exists

            string currentDirectory = Directory.GetCurrentDirectory();

            try
            {
                Directory.SetCurrentDirectory(Environment.SystemDirectory);

                Assert.True(FileUtilities.FileExistsNoThrow(inputPath));
                Assert.False(FileUtilities.FileExistsNoThrow(inputPath.Replace('\\', 'X')));
            }
            finally
            {
                Directory.SetCurrentDirectory(currentDirectory);
            }
        }

        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void GetFileInfoNoThrowTooLongWithDots()
        {
            int length = (Environment.SystemDirectory + @"\" + @"\..\..\..\" + Environment.SystemDirectory.Substring(3) + @"\..\explorer.exe").Length;

            string longPart = new string('x', 260 - length); // We want the shortest that is > max path.

            string inputPath = Environment.SystemDirectory + @"\" + longPart + @"\..\..\..\" + Environment.SystemDirectory.Substring(3) + @"\..\explorer.exe";
            Console.WriteLine(inputPath.Length);

            // "c:\windows\system32\<verylong>\..\..\windows\system32" exists
            Assert.True(FileUtilities.GetFileInfoNoThrow(inputPath) != null);
            Assert.False(FileUtilities.GetFileInfoNoThrow(inputPath.Replace('\\', 'X')) != null);
        }

        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void GetFileInfoNoThrowTooLongWithDotsRelative()
        {
            int length = (Environment.SystemDirectory + @"\" + @"\..\..\..\" + Environment.SystemDirectory.Substring(3) + @"\..\explorer.exe").Length;

            string longPart = new string('x', 260 - length); // We want the shortest that is > max path.

            string inputPath = longPart + @"\..\..\..\" + Environment.SystemDirectory.Substring(3) + @"\..\explorer.exe";
            Console.WriteLine(inputPath.Length);

            // "c:\windows\system32\<verylong>\..\..\windows\system32" exists

            string currentDirectory = Directory.GetCurrentDirectory();

            try
            {
                Directory.SetCurrentDirectory(Environment.SystemDirectory);

                Assert.True(FileUtilities.GetFileInfoNoThrow(inputPath) != null);
                Assert.False(FileUtilities.GetFileInfoNoThrow(inputPath.Replace('\\', 'X')) != null);
            }
            finally
            {
                Directory.SetCurrentDirectory(currentDirectory);
            }
        }
#endif

        /// <summary>
        /// Simple test, neither the base file nor retry files exist
        /// </summary>
        [Fact]
        public void GenerateTempFileNameSimple()
        {
            string path = null;

            try
            {
                path = FileUtilities.GetTemporaryFile();

                Assert.EndsWith(".tmp", path);
                Assert.True(File.Exists(path));
                Assert.StartsWith(Path.GetTempPath(), path);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Choose an extension
        /// </summary>
        [Fact]
        public void GenerateTempFileNameWithExtension()
        {
            string path = null;

            try
            {
                path = FileUtilities.GetTemporaryFile(".bat");

                Assert.EndsWith(".bat", path);
                Assert.True(File.Exists(path));
                Assert.StartsWith(Path.GetTempPath(), path);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Choose a (missing) directory and extension
        /// </summary>
        [Fact]
        public void GenerateTempFileNameWithDirectoryAndExtension()
        {
            string path = null;
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "subfolder");

            try
            {
                path = FileUtilities.GetTemporaryFile(directory, ".bat");

                Assert.EndsWith(".bat", path);
                Assert.True(File.Exists(path));
                Assert.StartsWith(directory, path);
            }
            finally
            {
                File.Delete(path);
                FileUtilities.DeleteWithoutTrailingBackslash(directory);
            }
        }

        /// <summary>
        /// Extension without a period
        /// </summary>
        [Fact]
        public void GenerateTempFileNameWithExtensionNoPeriod()
        {
            string path = null;

            try
            {
                path = FileUtilities.GetTemporaryFile("bat");

                Assert.EndsWith(".bat", path);
                Assert.True(File.Exists(path));
                Assert.StartsWith(Path.GetTempPath(), path);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Extension is invalid
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void GenerateTempBatchFileWithBadExtension()
        {
            Assert.Throws<IOException>(() =>
            {
                FileUtilities.GetTemporaryFile("|");
            }
           );
        }
        /// <summary>
        /// No extension is given
        /// </summary>
        [Fact]
        public void GenerateTempBatchFileWithEmptyExtension()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                FileUtilities.GetTemporaryFile(String.Empty);
            }
           );
        }
        /// <summary>
        /// Directory is invalid
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void GenerateTempBatchFileWithBadDirectory()
        {
            Assert.Throws<IOException>(() =>
            {
                FileUtilities.GetTemporaryFile("|", ".tmp");
            }
           );
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.AnyUnix)]
        public void AbsolutePathLooksLikeUnixPathOnUnix()
        {
            var secondSlash = SystemSpecificAbsolutePath.Substring(1).IndexOf(Path.DirectorySeparatorChar) + 1;
            var rootLevelPath = SystemSpecificAbsolutePath.Substring(0, secondSlash);

            Assert.True(FileUtilities.LooksLikeUnixFilePath(SystemSpecificAbsolutePath));
            Assert.True(FileUtilities.LooksLikeUnixFilePath(rootLevelPath));
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void PathDoesNotLookLikeUnixPathOnWindows()
        {
            Assert.False(FileUtilities.LooksLikeUnixFilePath(SystemSpecificAbsolutePath));
            Assert.False(FileUtilities.LooksLikeUnixFilePath("/path/that/looks/unixy"));
            Assert.False(FileUtilities.LooksLikeUnixFilePath("/root"));
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.AnyUnix)]
        public void RelativePathLooksLikeUnixPathOnUnixWithBaseDirectory()
        {
            string filePath = ObjectModelHelpers.CreateFileInTempProjectDirectory("first/second/file.txt", String.Empty);
            string oldCWD = Directory.GetCurrentDirectory();

            try
            {
                // <tmp_dir>/first
                string firstDirectory = Path.GetDirectoryName(Path.GetDirectoryName(filePath));
                string tmpDirectory = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(filePath)));

                Directory.SetCurrentDirectory(tmpDirectory);

                // We are in <tmp_dir> and second is not under that, so this will be false
                Assert.False(FileUtilities.LooksLikeUnixFilePath("second/file.txt"));

                // .. but if we have baseDirectory:firstDirectory, then it will be true
                Assert.True(FileUtilities.LooksLikeUnixFilePath("second/file.txt", firstDirectory));
            }
            finally
            {
                if (filePath != null)
                {
                    File.Delete(filePath);
                }
                Directory.SetCurrentDirectory(oldCWD);
            }
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.AnyUnix)]
        public void RelativePathMaybeAdjustFilePathWithBaseDirectory()
        {
            // <tmp_dir>/first/second/file.txt
            string filePath = ObjectModelHelpers.CreateFileInTempProjectDirectory("first/second/file.txt", String.Empty);
            string oldCWD = Directory.GetCurrentDirectory();

            try
            {
                // <tmp_dir>/first
                string firstDirectory = Path.GetDirectoryName(Path.GetDirectoryName(filePath));
                string tmpDirectory = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(filePath)));

                Directory.SetCurrentDirectory(tmpDirectory);

                // We are in <tmp_dir> and second is not under that, so this won't convert
                Assert.Equal("second\\file.txt", FileUtilities.MaybeAdjustFilePath("second\\file.txt"));

                // .. but if we have baseDirectory:firstDirectory, then it will
                Assert.Equal("second/file.txt", FileUtilities.MaybeAdjustFilePath("second\\file.txt", firstDirectory));
            }
            finally
            {
                if (filePath != null)
                {
                    File.Delete(filePath);
                }
                Directory.SetCurrentDirectory(oldCWD);
            }
        }

        private static string SystemSpecificAbsolutePath => FileUtilities.ExecutingAssemblyPath;


        [Fact]
        public void GetFolderAboveTest()
        {
            string root = NativeMethodsShared.IsWindows ? @"c:\" : "/";
            string path = Path.Combine(root, "1", "2", "3", "4", "5");

            Assert.Equal(Path.Combine(root, "1", "2", "3", "4", "5"), FileUtilities.GetFolderAbove(path, 0));
            Assert.Equal(Path.Combine(root, "1", "2", "3", "4"), FileUtilities.GetFolderAbove(path));
            Assert.Equal(Path.Combine(root, "1", "2", "3"), FileUtilities.GetFolderAbove(path, 2));
            Assert.Equal(Path.Combine(root, "1", "2"), FileUtilities.GetFolderAbove(path, 3));
            Assert.Equal(Path.Combine(root, "1"), FileUtilities.GetFolderAbove(path, 4));
            Assert.Equal(root, FileUtilities.GetFolderAbove(path, 5));
            Assert.Equal(root, FileUtilities.GetFolderAbove(path, 99));

            Assert.Equal(root, FileUtilities.GetFolderAbove(root, 99));
        }

        [Fact]
        public void CombinePathsTest()
        {
            // These tests run in .NET 4+, so we can cheat
            var root = @"c:\";

            Assert.Equal(
                Path.Combine(root, "path1"),
                FileUtilities.CombinePaths(root, "path1"));

            Assert.Equal(
                Path.Combine(root, "path1", "path2", "file.txt"),
                FileUtilities.CombinePaths(root, "path1", "path2", "file.txt"));
        }

        [Theory]
        [InlineData(@"c:\a\.\b", true)]
        [InlineData(@"c:\a\..\b", true)]
        [InlineData(@"c:\a\..", true)]
        [InlineData(@"c:\a\.", true)]
        [InlineData(@".\a", true)]
        [InlineData(@"..\b", true)]
        [InlineData(@"..", true)]
        [InlineData(@".", true)]
        [InlineData(@"..\", true)]
        [InlineData(@".\", true)]
        [InlineData(@"\..", true)]
        [InlineData(@"\.", true)]
        [InlineData(@"..\..\a", true)]
        [InlineData(@"..\..\..\a", true)]
        [InlineData(@"b..\", false)]
        [InlineData(@"b.\", false)]
        [InlineData(@"\b..", false)]
        [InlineData(@"\b.", false)]
        [InlineData(@"\b..\", false)]
        [InlineData(@"\b.\", false)]
        [InlineData(@"...", false)]
        [InlineData(@"....", false)]
        public void ContainsRelativeSegmentsTest(string path, bool expectedResult)
        {
            FileUtilities.ContainsRelativePathSegments(path).ShouldBe(expectedResult);
        }

        [Theory]
        [InlineData("a/b/c/d", 0, "")]
        [InlineData("a/b/c/d", 1, "d")]
        [InlineData("a/b/c/d", 2, "c/d")]
        [InlineData("a/b/c/d", 3, "b/c/d")]
        [InlineData("a/b/c/d", 4, "a/b/c/d")]
        [InlineData("a/b/c/d", 5, "a/b/c/d")]
        [InlineData(@"a\/\/\//b/\/\/\//c//\/\/\/d/\//\/\/", 2, "c/d")]
        public static void TestTruncatePathToTrailingSegments(string path, int trailingSegments, string expectedTruncatedPath)
        {
            expectedTruncatedPath = expectedTruncatedPath.Replace('/', Path.DirectorySeparatorChar);

            FileUtilities.TruncatePathToTrailingSegments(path, trailingSegments).ShouldBe(expectedTruncatedPath);
        }
    }
}
