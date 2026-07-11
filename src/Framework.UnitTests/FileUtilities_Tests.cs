// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Shouldly;

#nullable disable

namespace Microsoft.Build.Framework.UnitTests;

[TestClass]

public class FileUtilities_Tests
{
    /// <summary>
    /// Exercises ItemSpecModifiers.GetItemSpecModifier
    /// </summary>
    [MSBuildTestMethod]
    [TestCategory("netcore-osx-failing")]
    [TestCategory("netcore-linux-failing")]
    public void GetItemSpecModifier()
    {
        TestGetItemSpecModifier(Directory.GetCurrentDirectory());
        TestGetItemSpecModifier(null);
    }

    private static void TestGetItemSpecModifier(string currentDirectory)
    {
        ItemSpecModifiers.Cache cache = default;
        string modifier = ItemSpecModifiers.GetItemSpecModifier("foo", ItemSpecModifierKind.RecursiveDir, currentDirectory, String.Empty, ref cache);
        Assert.AreEqual(String.Empty, modifier);

        cache.Clear();
        modifier = ItemSpecModifiers.GetItemSpecModifier("foo", ItemSpecModifierKind.ModifiedTime, currentDirectory, String.Empty, ref cache);
        Assert.AreEqual(String.Empty, modifier);

        cache.Clear();
        modifier = ItemSpecModifiers.GetItemSpecModifier(@"foo\goo", ItemSpecModifierKind.RelativeDir, currentDirectory, String.Empty, ref cache);
        Assert.AreEqual(@"foo" + Path.DirectorySeparatorChar, modifier);

        // confirm we get the same thing back the second time
        modifier = ItemSpecModifiers.GetItemSpecModifier(@"foo\goo", ItemSpecModifierKind.RelativeDir, currentDirectory, String.Empty, ref cache);
        Assert.AreEqual(@"foo" + Path.DirectorySeparatorChar, modifier);

        cache.Clear();
        string itemSpec = NativeMethodsShared.IsWindows ? @"c:\foo.txt" : "/foo.txt";
        string itemSpecDir = NativeMethodsShared.IsWindows ? @"c:\" : "/";
        modifier = ItemSpecModifiers.GetItemSpecModifier(itemSpec, ItemSpecModifierKind.FullPath, currentDirectory, String.Empty, ref cache);
        Assert.AreEqual(itemSpec, modifier);
        Assert.AreEqual(itemSpec, cache.FullPath);

        modifier = ItemSpecModifiers.GetItemSpecModifier(itemSpec, ItemSpecModifierKind.RootDir, currentDirectory, String.Empty, ref cache);
        Assert.AreEqual(itemSpecDir, modifier);

        modifier = ItemSpecModifiers.GetItemSpecModifier(itemSpec, ItemSpecModifierKind.Filename, currentDirectory, String.Empty, ref cache);
        Assert.AreEqual(@"foo", modifier);

        modifier = ItemSpecModifiers.GetItemSpecModifier(itemSpec, ItemSpecModifierKind.Extension, currentDirectory, String.Empty, ref cache);
        Assert.AreEqual(@".txt", modifier);

        modifier = ItemSpecModifiers.GetItemSpecModifier(itemSpec, ItemSpecModifierKind.Directory, currentDirectory, String.Empty, ref cache);
        Assert.AreEqual(String.Empty, modifier);

        modifier = ItemSpecModifiers.GetItemSpecModifier(itemSpec, ItemSpecModifierKind.Identity, currentDirectory, String.Empty, ref cache);
        Assert.AreEqual(itemSpec, modifier);

        string projectPath = NativeMethodsShared.IsWindows ? @"c:\abc\goo.proj" : @"/abc/goo.proj";
        string projectPathDir = NativeMethodsShared.IsWindows ? @"c:\abc\" : @"/abc/";
        modifier = ItemSpecModifiers.GetItemSpecModifier(itemSpec, ItemSpecModifierKind.DefiningProjectDirectory, currentDirectory, projectPath, ref cache);
        Assert.AreEqual(projectPathDir, modifier);

        modifier = ItemSpecModifiers.GetItemSpecModifier(itemSpec, ItemSpecModifierKind.DefiningProjectExtension, currentDirectory, projectPath, ref cache);
        Assert.AreEqual(@".proj", modifier);

        modifier = ItemSpecModifiers.GetItemSpecModifier(itemSpec, ItemSpecModifierKind.DefiningProjectFullPath, currentDirectory, projectPath, ref cache);
        Assert.AreEqual(projectPath, modifier);

        modifier = ItemSpecModifiers.GetItemSpecModifier(itemSpec, ItemSpecModifierKind.DefiningProjectName, currentDirectory, projectPath, ref cache);
        Assert.AreEqual(@"goo", modifier);
    }

    [MSBuildTestMethod]
    public void MakeRelativeTests()
    {
        if (NativeMethodsShared.IsWindows)
        {
            Assert.AreEqual(@"foo.cpp", FileUtilities.MakeRelative(@"c:\abc\def", @"c:\abc\def\foo.cpp"));
            Assert.AreEqual(@"def\foo.cpp", FileUtilities.MakeRelative(@"c:\abc\", @"c:\abc\def\foo.cpp"));
            Assert.AreEqual(@"..\foo.cpp", FileUtilities.MakeRelative(@"c:\abc\def\xyz", @"c:\abc\def\foo.cpp"));
            Assert.AreEqual(@"..\ttt\foo.cpp", FileUtilities.MakeRelative(@"c:\abc\def\xyz\", @"c:\abc\def\ttt\foo.cpp"));
            Assert.AreEqual(@"e:\abc\def\foo.cpp", FileUtilities.MakeRelative(@"c:\abc\def", @"e:\abc\def\foo.cpp"));
            Assert.AreEqual(@"foo.cpp", FileUtilities.MakeRelative(@"\\aaa\abc\def", @"\\aaa\abc\def\foo.cpp"));
            Assert.AreEqual(@"foo.cpp", FileUtilities.MakeRelative(@"c:\abc\def", @"foo.cpp"));
            Assert.AreEqual(@"\\host\path\file", FileUtilities.MakeRelative(@"c:\abc\def", @"\\host\path\file"));
            Assert.AreEqual(@"\\host\d$\file", FileUtilities.MakeRelative(@"c:\abc\def", @"\\host\d$\file"));
            Assert.AreEqual(@"..\fff\ggg.hh", FileUtilities.MakeRelative(@"c:\foo\bar\..\abc\cde", @"c:\foo\bar\..\abc\fff\ggg.hh"));

            /* Directories */
            Assert.AreEqual(@"def\", FileUtilities.MakeRelative(@"c:\abc\", @"c:\abc\def\"));
            Assert.AreEqual(@"..\", FileUtilities.MakeRelative(@"c:\abc\def\xyz\", @"c:\abc\def\"));
            Assert.AreEqual(@"..\ttt\", FileUtilities.MakeRelative(@"c:\abc\def\xyz\", @"c:\abc\def\ttt\"));
            Assert.AreEqual(@".", FileUtilities.MakeRelative(@"c:\abc\def\", @"c:\abc\def\"));

            /* Directory + File */
            Assert.AreEqual(@"def", FileUtilities.MakeRelative(@"c:\abc\", @"c:\abc\def"));
            Assert.AreEqual(@"..\..\ghi", FileUtilities.MakeRelative(@"c:\abc\def\xyz\", @"c:\abc\ghi"));
            Assert.AreEqual(@"..\ghi", FileUtilities.MakeRelative(@"c:\abc\def\xyz\", @"c:\abc\def\ghi"));
            Assert.AreEqual(@"..\ghi", FileUtilities.MakeRelative(@"c:\abc\def\", @"c:\abc\ghi"));

            /* File + Directory */
            Assert.AreEqual(@"def\", FileUtilities.MakeRelative(@"c:\abc", @"c:\abc\def\"));
            Assert.AreEqual(@"..\", FileUtilities.MakeRelative(@"c:\abc\def\xyz", @"c:\abc\def\"));
            Assert.AreEqual(@"..\ghi\", FileUtilities.MakeRelative(@"c:\abc\def\xyz", @"c:\abc\def\ghi\"));
            Assert.AreEqual(@".", FileUtilities.MakeRelative(@"c:\abc\def", @"c:\abc\def\"));
        }
        else
        {
            Assert.AreEqual(@"bar.cpp", FileUtilities.MakeRelative(@"/abc/def", @"/abc/def/bar.cpp"));
            Assert.AreEqual(@"def/foo.cpp", FileUtilities.MakeRelative(@"/abc/", @"/abc/def/foo.cpp"));
            Assert.AreEqual(@"../foo.cpp", FileUtilities.MakeRelative(@"/abc/def/xyz", @"/abc/def/foo.cpp"));
            Assert.AreEqual(@"../ttt/foo.cpp", FileUtilities.MakeRelative(@"/abc/def/xyz/", @"/abc/def/ttt/foo.cpp"));
            Assert.AreEqual(@"foo.cpp", FileUtilities.MakeRelative(@"/abc/def", @"foo.cpp"));
            Assert.AreEqual(@"../fff/ggg.hh", FileUtilities.MakeRelative(@"/foo/bar/../abc/cde", @"/foo/bar/../abc/fff/ggg.hh"));

            /* Directories */
            Assert.AreEqual(@"def/", FileUtilities.MakeRelative(@"/abc/", @"/abc/def/"));
            Assert.AreEqual(@"../", FileUtilities.MakeRelative(@"/abc/def/xyz/", @"/abc/def/"));
            Assert.AreEqual(@"../ttt/", FileUtilities.MakeRelative(@"/abc/def/xyz/", @"/abc/def/ttt/"));
            Assert.AreEqual(@".", FileUtilities.MakeRelative(@"/abc/def/", @"/abc/def/"));

            /* Directory + File */
            Assert.AreEqual(@"def", FileUtilities.MakeRelative(@"/abc/", @"/abc/def"));
            Assert.AreEqual(@"../../ghi", FileUtilities.MakeRelative(@"/abc/def/xyz/", @"/abc/ghi"));
            Assert.AreEqual(@"../ghi", FileUtilities.MakeRelative(@"/abc/def/xyz/", @"/abc/def/ghi"));
            Assert.AreEqual(@"../ghi", FileUtilities.MakeRelative(@"/abc/def/", @"/abc/ghi"));

            /* File + Directory */
            Assert.AreEqual(@"def/", FileUtilities.MakeRelative(@"/abc", @"/abc/def/"));
            Assert.AreEqual(@"../", FileUtilities.MakeRelative(@"/abc/def/xyz", @"/abc/def/"));
            Assert.AreEqual(@"../ghi/", FileUtilities.MakeRelative(@"/abc/def/xyz", @"/abc/def/ghi/"));
            Assert.AreEqual(@".", FileUtilities.MakeRelative(@"/abc/def", @"/abc/def/"));
        }
    }

    /// <summary>
    /// Exercises ItemSpecModifiers.GetItemSpecModifier on a bad path.
    /// </summary>
    [WindowsFullFrameworkOnlyFact(additionalMessage: ".NET Core 2.1+ no longer validates paths: https://github.com/dotnet/corefx/issues/27779#issuecomment-371253486.")]
    public void GetItemSpecModifierOnBadPath()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            TestGetItemSpecModifierOnBadPath(Directory.GetCurrentDirectory());
        });
    }
    /// <summary>
    /// Exercises ItemSpecModifiers.GetItemSpecModifier on a bad path.
    /// </summary>
    [WindowsFullFrameworkOnlyFact(additionalMessage: ".NET Core 2.1+ no longer validates paths: https://github.com/dotnet/corefx/issues/27779#issuecomment-371253486.")]
    public void GetItemSpecModifierOnBadPath2()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            TestGetItemSpecModifierOnBadPath(null);
        });
    }

    private static void TestGetItemSpecModifierOnBadPath(string currentDirectory)
    {
        try
        {
            ItemSpecModifiers.GetItemSpecModifier(@"http://www.microsoft.com", ItemSpecModifiers.RootDir, currentDirectory, String.Empty);
        }
        catch (Exception e)
        {
            // so I can see the exception message in NUnit's "Standard Out" window
            Console.WriteLine(e.Message);
            throw;
        }
    }

    [MSBuildTestMethod]
    public void GetFileInfoNoThrowBasic()
    {
        string file = null;
        try
        {
            file = FileUtilities.GetTemporaryFile();
            FileInfo info = FileUtilities.GetFileInfoNoThrow(file);
            Assert.AreEqual(info.LastWriteTime, new FileInfo(file).LastWriteTime);
        }
        finally
        {
            if (file != null)
            {
                File.Delete(file);
            }
        }
    }

    [MSBuildTestMethod]
    public void GetFileInfoNoThrowNonexistent()
    {
        FileInfo info = FileUtilities.GetFileInfoNoThrow("this_file_is_nonexistent");
        Assert.IsNull(info);
    }

    /// <summary>
    /// Exercises FrameworkFileUtilities.EndsWithSlash
    /// </summary>
    [MSBuildTestMethod]
    [TestCategory("netcore-osx-failing")]
    [TestCategory("netcore-linux-failing")]
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
    [MSBuildTestMethod]
    [TestCategory("netcore-osx-failing")]
    [TestCategory("netcore-linux-failing")]
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

    [MSBuildTestMethod]
    [DataRow("foo.txt", new[] { ".txt" })]
    [DataRow("foo.txt", new[] { ".TXT" })]
    [DataRow("foo.txt", new[] { ".EXE", ".TXT" })]
    public void HasExtension_WhenFileNameHasExtension_ReturnsTrue(string fileName, string[] allowedExtensions)
    {
        var result = FileUtilities.HasExtension(fileName, allowedExtensions);

        if (!FileUtilities.IsFileSystemCaseSensitive || allowedExtensions.Any(extension => fileName.Contains(extension)))
        {
            result.ShouldBeTrue();
        }
    }

    [MSBuildTestMethod]
    [DataRow("foo.txt", new[] { ".DLL" })]
    [DataRow("foo.txt", new[] { ".EXE", ".DLL" })]
    [DataRow("foo.exec", new[] { ".exe", })]
    [DataRow("foo.exe", new[] { ".exec", })]
    [DataRow("foo", new[] { ".exe", })]
    [DataRow("", new[] { ".exe" })]
    [DataRow(null, new[] { ".exe" })]
    public void HasExtension_WhenFileNameDoesNotHaveExtension_ReturnsFalse(string fileName, string[] allowedExtensions)
    {
        var result = FileUtilities.HasExtension(fileName, allowedExtensions);

        Assert.IsFalse(result);
    }

    [WindowsFullFrameworkOnlyFact]
    public void HasExtension_WhenInvalidFileName_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
        {
            FileUtilities.HasExtension("|/", new[] { ".exe" });
        });
    }

    [MSBuildTestMethod]
    public void HasExtension_UsesOrdinalIgnoreCase()
    {
        var currentCulture = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("tr-TR"); // Turkish

            var result = FileUtilities.HasExtension("foo.ini", new string[] { ".INI" });

            result.ShouldBe(!FileUtilities.IsFileSystemCaseSensitive);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = currentCulture;
        }
    }

    /// <summary>
    /// Exercises FrameworkFileUtilities.EnsureTrailingSlash
    /// </summary>
    [MSBuildTestMethod]
    public void EnsureTrailingSlash()
    {
        // Doesn't have a trailing slash to start with.
        Assert.AreEqual(FileUtilities.FixFilePath(@"foo\bar\"), FileUtilities.EnsureTrailingSlash(@"foo\bar")); // "test 1"
        Assert.AreEqual(FileUtilities.FixFilePath(@"foo/bar\"), FileUtilities.EnsureTrailingSlash(@"foo/bar")); // "test 2"

        // Already has a trailing slash to start with.
        Assert.AreEqual(FileUtilities.FixFilePath(@"foo/bar/"), FileUtilities.EnsureTrailingSlash(@"foo/bar/")); // test 3"
        Assert.AreEqual(FileUtilities.FixFilePath(@"foo\bar\"), FileUtilities.EnsureTrailingSlash(@"foo\bar\")); // test 4"
        Assert.AreEqual(FileUtilities.FixFilePath(@"foo/bar\"), FileUtilities.EnsureTrailingSlash(@"foo/bar\")); // test 5"
        Assert.AreEqual(FileUtilities.FixFilePath(@"foo\bar/"), FileUtilities.EnsureTrailingSlash(@"foo\bar/")); // "test 5"
    }

    /// <summary>
    /// Exercises ItemSpecModifiers.IsItemSpecModifier
    /// </summary>
    [MSBuildTestMethod]
    public void IsItemSpecModifier()
    {
        // Positive matches using exact case.
        Assert.IsTrue(ItemSpecModifiers.IsItemSpecModifier("FullPath")); // "test 1"
        Assert.IsTrue(ItemSpecModifiers.IsItemSpecModifier("RootDir")); // "test 2"
        Assert.IsTrue(ItemSpecModifiers.IsItemSpecModifier("Filename")); // "test 3"
        Assert.IsTrue(ItemSpecModifiers.IsItemSpecModifier("Extension")); // "test 4"
        Assert.IsTrue(ItemSpecModifiers.IsItemSpecModifier("RelativeDir")); // "test 5"
        Assert.IsTrue(ItemSpecModifiers.IsItemSpecModifier("Directory")); // "test 6"
        Assert.IsTrue(ItemSpecModifiers.IsItemSpecModifier("RecursiveDir")); // "test 7"
        Assert.IsTrue(ItemSpecModifiers.IsItemSpecModifier("Identity")); // "test 8"
        Assert.IsTrue(ItemSpecModifiers.IsItemSpecModifier("ModifiedTime")); // "test 9"
        Assert.IsTrue(ItemSpecModifiers.IsItemSpecModifier("CreatedTime")); // "test 10"
        Assert.IsTrue(ItemSpecModifiers.IsItemSpecModifier("AccessedTime")); // "test 11"

        // Positive matches using different case.
        Assert.IsTrue(ItemSpecModifiers.IsItemSpecModifier("fullPath")); // "test 21"
        Assert.IsTrue(ItemSpecModifiers.IsItemSpecModifier("rootDir")); // "test 22"
        Assert.IsTrue(ItemSpecModifiers.IsItemSpecModifier("filename")); // "test 23"
        Assert.IsTrue(ItemSpecModifiers.IsItemSpecModifier("extension")); // "test 24"
        Assert.IsTrue(ItemSpecModifiers.IsItemSpecModifier("relativeDir")); // "test 25"
        Assert.IsTrue(ItemSpecModifiers.IsItemSpecModifier("directory")); // "test 26"
        Assert.IsTrue(ItemSpecModifiers.IsItemSpecModifier("recursiveDir")); // "test 27"
        Assert.IsTrue(ItemSpecModifiers.IsItemSpecModifier("identity")); // "test 28"
        Assert.IsTrue(ItemSpecModifiers.IsItemSpecModifier("modifiedTime")); // "test 29"
        Assert.IsTrue(ItemSpecModifiers.IsItemSpecModifier("createdTime")); // "test 30"
        Assert.IsTrue(ItemSpecModifiers.IsItemSpecModifier("accessedTime")); // "test 31"

        // Negative tests to get maximum code coverage inside the many different branches
        // of ItemSpecModifiers.IsItemSpecModifier.
        Assert.IsFalse(ItemSpecModifiers.IsItemSpecModifier("rootxxx")); // "test 41"
        Assert.IsFalse(ItemSpecModifiers.IsItemSpecModifier("Rootxxx")); // "test 42"
        Assert.IsFalse(ItemSpecModifiers.IsItemSpecModifier("xxxxxxx")); // "test 43"

        Assert.IsFalse(ItemSpecModifiers.IsItemSpecModifier("filexxxx")); // "test 44"
        Assert.IsFalse(ItemSpecModifiers.IsItemSpecModifier("Filexxxx")); // "test 45"
        Assert.IsFalse(ItemSpecModifiers.IsItemSpecModifier("idenxxxx")); // "test 46"
        Assert.IsFalse(ItemSpecModifiers.IsItemSpecModifier("Idenxxxx")); // "test 47"
        Assert.IsFalse(ItemSpecModifiers.IsItemSpecModifier("xxxxxxxx")); // "test 48"

        Assert.IsFalse(ItemSpecModifiers.IsItemSpecModifier("extenxxxx")); // "test 49"
        Assert.IsFalse(ItemSpecModifiers.IsItemSpecModifier("Extenxxxx")); // "test 50"
        Assert.IsFalse(ItemSpecModifiers.IsItemSpecModifier("direcxxxx")); // "test 51"
        Assert.IsFalse(ItemSpecModifiers.IsItemSpecModifier("Direcxxxx")); // "test 52"
        Assert.IsFalse(ItemSpecModifiers.IsItemSpecModifier("xxxxxxxxx")); // "test 53"

        Assert.IsFalse(ItemSpecModifiers.IsItemSpecModifier("xxxxxxxxxx")); // "test 54"

        Assert.IsFalse(ItemSpecModifiers.IsItemSpecModifier("relativexxx")); // "test 55"
        Assert.IsFalse(ItemSpecModifiers.IsItemSpecModifier("Relativexxx")); // "test 56"
        Assert.IsFalse(ItemSpecModifiers.IsItemSpecModifier("createdxxxx")); // "test 57"
        Assert.IsFalse(ItemSpecModifiers.IsItemSpecModifier("Createdxxxx")); // "test 58"
        Assert.IsFalse(ItemSpecModifiers.IsItemSpecModifier("xxxxxxxxxxx")); // "test 59"

        Assert.IsFalse(ItemSpecModifiers.IsItemSpecModifier("recursivexxx")); // "test 60"
        Assert.IsFalse(ItemSpecModifiers.IsItemSpecModifier("Recursivexxx")); // "test 61"
        Assert.IsFalse(ItemSpecModifiers.IsItemSpecModifier("accessedxxxx")); // "test 62"
        Assert.IsFalse(ItemSpecModifiers.IsItemSpecModifier("Accessedxxxx")); // "test 63"
        Assert.IsFalse(ItemSpecModifiers.IsItemSpecModifier("modifiedxxxx")); // "test 64"
        Assert.IsFalse(ItemSpecModifiers.IsItemSpecModifier("Modifiedxxxx")); // "test 65"
        Assert.IsFalse(ItemSpecModifiers.IsItemSpecModifier("xxxxxxxxxxxx")); // "test 66"

        Assert.IsFalse(ItemSpecModifiers.IsItemSpecModifier(null)); // "test 67"
    }

    [MSBuildTestMethod]
    public void CheckDerivableItemSpecModifiers()
    {
        Assert.IsTrue(ItemSpecModifiers.IsDerivableItemSpecModifier("Filename"));
        Assert.IsFalse(ItemSpecModifiers.IsDerivableItemSpecModifier("RecursiveDir"));
        Assert.IsFalse(ItemSpecModifiers.IsDerivableItemSpecModifier("recursivedir"));
    }

    [WindowsOnlyFact]
    public void NormalizePathThatFitsIntoMaxPath()
    {
        string currentDirectory = @"c:\aardvark\aardvark\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890";
        string filePath = @"..\..\..\..\..\..\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\a.cs";
        string fullPath = @"c:\aardvark\aardvark\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\a.cs";

        Assert.AreEqual(fullPath, FileUtilities.NormalizePath(Path.Combine(currentDirectory, filePath)));
    }

    [LongPathSupportDisabledTestMethod(fullFrameworkOnly: true, additionalMessage: "https://github.com/dotnet/msbuild/issues/4363")]
    public void NormalizePathThatDoesntFitIntoMaxPath()
    {
        Assert.ThrowsExactly<PathTooLongException>(() =>
        {
            string currentDirectory = @"c:\aardvark\aardvark\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890";
            string filePath = @"..\..\..\..\..\..\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\a.cs";

            // This path ends up over 420 characters long
            string fullPath = @"c:\aardvark\aardvark\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\a.cs";

            Assert.AreEqual(fullPath, FileUtilities.NormalizePath(Path.Combine(currentDirectory, filePath)));
        });
    }

    [WindowsOnlyFact]
    public void GetItemSpecModifierRootDirThatFitsIntoMaxPath()
    {
        string currentDirectory = @"c:\aardvark\aardvark\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890";
        string fullPath = @"c:\aardvark\aardvark\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\1234567890\a.cs";
        ItemSpecModifiers.Cache cache = new() { FullPath = fullPath };

        Assert.AreEqual(@"c:\", ItemSpecModifiers.GetItemSpecModifier(fullPath, ItemSpecModifierKind.RootDir, currentDirectory, String.Empty, ref cache));
    }

    [MSBuildTestMethod]
    public void NormalizePathNull()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
        {
            Assert.IsNull(FileUtilities.NormalizePath(null, null));
        });
    }

    [MSBuildTestMethod]
    public void NormalizePathEmpty()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
        {
            Assert.IsNull(FileUtilities.NormalizePath(String.Empty));
        });
    }

    [WindowsFullFrameworkOnlyFact(additionalMessage: ".NET Core 2.1+ no longer validates paths: https://github.com/dotnet/corefx/issues/27779#issuecomment-371253486.")]
    public void NormalizePathBadUNC1()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
        {
            Assert.IsNull(FileUtilities.NormalizePath(@"\\"));
        });
    }

    [WindowsFullFrameworkOnlyFact(additionalMessage: ".NET Core 2.1+ no longer validates paths: https://github.com/dotnet/corefx/issues/27779#issuecomment-371253486.")]
    public void NormalizePathBadUNC2()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
        {
            Assert.IsNull(FileUtilities.NormalizePath(@"\\XXX\"));
        });
    }

    [WindowsFullFrameworkOnlyFact(additionalMessage: ".NET Core 2.1+ no longer validates paths: https://github.com/dotnet/corefx/issues/27779#issuecomment-371253486.")]
    public void NormalizePathBadUNC3()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
        {
            Assert.AreEqual(@"\\localhost", FileUtilities.NormalizePath(@"\\localhost"));
        });
    }

    [WindowsOnlyFact]
    public void NormalizePathGoodUNC()
    {
        Assert.AreEqual(@"\\localhost\share", FileUtilities.NormalizePath(@"\\localhost\share"));
    }

    [WindowsOnlyFact]
    public void NormalizePathTooLongWithDots()
    {
        string longPart = new string('x', 300);
        Assert.AreEqual(@"c:\abc\def", FileUtilities.NormalizePath(@"c:\abc\" + longPart + @"\..\def"));
    }

    [WindowsFullFrameworkOnlyFact(additionalMessage: ".NET Core 2.1+ no longer validates paths: https://github.com/dotnet/corefx/issues/27779#issuecomment-371253486.")]
    public void NormalizePathInvalid()
    {
        string filePath = @"c:\aardvark\|||";

        Assert.ThrowsExactly<ArgumentException>(() =>
        {
            FileUtilities.NormalizePath(filePath);
        });
    }

    [WindowsOnlyFact]
    public void CannotNormalizePathWithNewLineAndSpace()
    {
        string filePath = "\r\n      C:\\work\\sdk3\\artifacts\\tmp\\Debug\\SimpleNamesWi---6143883E\\NETFrameworkLibrary\\bin\\Debug\\net462\\NETFrameworkLibrary.dll\r\n      ";

#if FEATURE_LEGACY_GETFULLPATH
        Assert.ThrowsExactly<ArgumentException>(() => FileUtilities.NormalizePath(filePath));
#else
        Assert.AreNotEqual("C:\\work\\sdk3\\artifacts\\tmp\\Debug\\SimpleNamesWi---6143883E\\NETFrameworkLibrary\\bin\\Debug\\net462\\NETFrameworkLibrary.dll", FileUtilities.NormalizePath(filePath));
#endif
    }

    [MSBuildTestMethod]
    public void FileOrDirectoryExistsNoThrow()
    {
        var isWindows = NativeMethodsShared.IsWindows;

        Assert.IsFalse(FileUtilities.FileOrDirectoryExistsNoThrow("||"));
        Assert.IsFalse(FileUtilities.FileOrDirectoryExistsNoThrow(isWindows ? @"c:\doesnot_exist" : "/doesnot_exist"));
        Assert.IsTrue(FileUtilities.FileOrDirectoryExistsNoThrow(isWindows ? @"c:\" : "/"));
        Assert.IsTrue(FileUtilities.FileOrDirectoryExistsNoThrow(Path.GetTempPath()));

        string path = null;

        try
        {
            path = FileUtilities.GetTemporaryFile();
            Assert.IsTrue(FileUtilities.FileOrDirectoryExistsNoThrow(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

#if FEATURE_ENVIRONMENT_SYSTEMDIRECTORY
    [MSBuildTestMethod]
    public void FileOrDirectoryExistsNoThrowTooLongWithDots()
    {
        if (!RunTestsThatDependOnWindowsShortPathBehavior_Workaround4241())
        {
            Assert.Skip("These tests will need to be redesigned for Linux");
        }

        int length = (Environment.SystemDirectory + @"\" + @"\..\..\..\" + Environment.SystemDirectory.Substring(3)).Length;

        string longPart = new string('x', 260 - length); // We want the shortest that is > max path.

        string inputPath = Environment.SystemDirectory + @"\" + longPart + @"\..\..\..\" + Environment.SystemDirectory.Substring(3);
        Console.WriteLine(inputPath.Length);

        // "c:\windows\system32\<verylong>\..\..\windows\system32" exists
        Assert.IsTrue(FileUtilities.FileOrDirectoryExistsNoThrow(inputPath));
        Assert.IsFalse(FileUtilities.FileOrDirectoryExistsNoThrow(inputPath.Replace('\\', 'X')));
    }

    [MSBuildTestMethod]
    public void FileOrDirectoryExistsNoThrowTooLongWithDotsRelative()
    {
        if (!RunTestsThatDependOnWindowsShortPathBehavior_Workaround4241())
        {
            Assert.Skip("These tests will need to be redesigned for Linux");
        }

        int length = (Environment.SystemDirectory + @"\" + @"\..\..\..\" + Environment.SystemDirectory.Substring(3)).Length;

        string longPart = new string('x', 260 - length); // We want the shortest that is > max path.

        string inputPath = longPart + @"\..\..\..\" + Environment.SystemDirectory.Substring(3);
        Console.WriteLine(inputPath.Length);

        // "c:\windows\system32\<verylong>\..\..\windows\system32" exists

        string currentDirectory = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(Environment.SystemDirectory);

            Assert.IsTrue(FileUtilities.FileOrDirectoryExistsNoThrow(inputPath));
            Assert.IsFalse(FileUtilities.FileOrDirectoryExistsNoThrow(inputPath.Replace('\\', 'X')));
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }
    }

    [MSBuildTestMethod]
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
        Assert.IsTrue(FileUtilities.DirectoryExistsNoThrow(inputPath));
    }

    [MSBuildTestMethod]
    public void DirectoryExistsNoThrowTooLongWithDotsRelative()
    {
        if (!RunTestsThatDependOnWindowsShortPathBehavior_Workaround4241())
        {
            Assert.Skip("These tests will need to be redesigned for Linux");
        }

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
        // https://github.com/dotnet/msbuild/issues/4241
               NativeMethodsShared.IsMaxPathLegacyWindows();
    }

    [MSBuildTestMethod]
    public void FileExistsNoThrowTooLongWithDots()
    {
        if (!RunTestsThatDependOnWindowsShortPathBehavior_Workaround4241())
        {
            Assert.Skip("These tests will need to be redesigned for Linux");
        }

        int length = (Environment.SystemDirectory + @"\" + @"\..\..\..\" + Environment.SystemDirectory.Substring(3) + @"\..\explorer.exe").Length;

        string longPart = new string('x', 260 - length); // We want the shortest that is > max path.

        string inputPath = Environment.SystemDirectory + @"\" + longPart + @"\..\..\..\" + Environment.SystemDirectory.Substring(3) + @"\..\explorer.exe";
        Console.WriteLine(inputPath.Length);
        Console.WriteLine(inputPath);

        // "c:\windows\system32\<verylong>\..\..\windows\system32" exists
        Assert.IsTrue(FileUtilities.FileExistsNoThrow(inputPath));
    }

    [MSBuildTestMethod]
    public void FileExistsNoThrowTooLongWithDotsRelative()
    {
        if (!RunTestsThatDependOnWindowsShortPathBehavior_Workaround4241())
        {
            Assert.Skip("These tests will need to be redesigned for Linux");
        }

        int length = (Environment.SystemDirectory + @"\" + @"\..\..\..\" + Environment.SystemDirectory.Substring(3) + @"\..\explorer.exe").Length;

        string longPart = new string('x', 260 - length); // We want the shortest that is > max path.

        string inputPath = longPart + @"\..\..\..\" + Environment.SystemDirectory.Substring(3) + @"\..\explorer.exe";
        Console.WriteLine(inputPath.Length);

        // "c:\windows\system32\<verylong>\..\..\windows\system32" exists

        string currentDirectory = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(Environment.SystemDirectory);

            Assert.IsTrue(FileUtilities.FileExistsNoThrow(inputPath));
            Assert.IsFalse(FileUtilities.FileExistsNoThrow(inputPath.Replace('\\', 'X')));
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }
    }

    [MSBuildTestMethod]
    public void GetFileInfoNoThrowTooLongWithDots()
    {
        int length = (Environment.SystemDirectory + @"\" + @"\..\..\..\" + Environment.SystemDirectory.Substring(3) + @"\..\explorer.exe").Length;

        string longPart = new string('x', 260 - length); // We want the shortest that is > max path.

        string inputPath = Environment.SystemDirectory + @"\" + longPart + @"\..\..\..\" + Environment.SystemDirectory.Substring(3) + @"\..\explorer.exe";
        Console.WriteLine(inputPath.Length);

        // "c:\windows\system32\<verylong>\..\..\windows\system32" exists
        Assert.IsTrue(FileUtilities.GetFileInfoNoThrow(inputPath) != null);
        Assert.IsFalse(FileUtilities.GetFileInfoNoThrow(inputPath.Replace('\\', 'X')) != null);
    }

    [MSBuildTestMethod]
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

            Assert.IsTrue(FileUtilities.GetFileInfoNoThrow(inputPath) != null);
            Assert.IsFalse(FileUtilities.GetFileInfoNoThrow(inputPath.Replace('\\', 'X')) != null);
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
    [MSBuildTestMethod]
    public void GenerateTempFileNameSimple()
    {
        string path = null;

        try
        {
            path = FileUtilities.GetTemporaryFile();

            Assert.EndsWith(".tmp", path);
            Assert.IsTrue(File.Exists(path));
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
    [MSBuildTestMethod]
    public void GenerateTempFileNameWithExtension()
    {
        string path = null;

        try
        {
            path = FileUtilities.GetTemporaryFile(".bat");

            Assert.EndsWith(".bat", path);
            Assert.IsTrue(File.Exists(path));
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
    [MSBuildTestMethod]
    public void GenerateTempFileNameWithDirectoryAndExtension()
    {
        string path = null;
        string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "subfolder");

        try
        {
            path = FileUtilities.GetTemporaryFile(directory, null, ".bat");

            Assert.EndsWith(".bat", path);
            Assert.IsTrue(File.Exists(path));
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
    [MSBuildTestMethod]
    public void GenerateTempFileNameWithExtensionNoPeriod()
    {
        string path = null;

        try
        {
            path = FileUtilities.GetTemporaryFile("bat");

            Assert.EndsWith(".bat", path);
            Assert.IsTrue(File.Exists(path));
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
    [MSBuildTestMethod]
    [TestCategory("netcore-osx-failing")]
    [TestCategory("netcore-linux-failing")]
    public void GenerateTempBatchFileWithBadExtension()
    {
        Assert.ThrowsExactly<IOException>(() =>
        {
            FileUtilities.GetTemporaryFile("|");
        });
    }

    /// <summary>
    /// Directory is invalid
    /// </summary>
    [MSBuildTestMethod]
    [TestCategory("netcore-osx-failing")]
    [TestCategory("netcore-linux-failing")]
    public void GenerateTempBatchFileWithBadDirectory()
    {
        Assert.ThrowsExactly<IOException>(() =>
        {
            FileUtilities.GetTemporaryFile("|", null, ".tmp");
        });
    }

    [UnixOnlyFact]
    public void AbsolutePathLooksLikeUnixPathOnUnix()
    {
        var secondSlash = SystemSpecificAbsolutePath.Substring(1).IndexOf(Path.DirectorySeparatorChar) + 1;
        var rootLevelPath = SystemSpecificAbsolutePath.Substring(0, secondSlash);

        Assert.IsTrue(FileUtilities.LooksLikeUnixFilePath(SystemSpecificAbsolutePath));
        Assert.IsTrue(FileUtilities.LooksLikeUnixFilePath(rootLevelPath));
    }

    [WindowsOnlyFact]
    public void PathDoesNotLookLikeUnixPathOnWindows()
    {
        Assert.IsFalse(FileUtilities.LooksLikeUnixFilePath(SystemSpecificAbsolutePath));
        Assert.IsFalse(FileUtilities.LooksLikeUnixFilePath("/path/that/looks/unixy"));
        Assert.IsFalse(FileUtilities.LooksLikeUnixFilePath("/root"));
    }

    [UnixOnlyFact]
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
            Assert.IsFalse(FileUtilities.LooksLikeUnixFilePath("second/file.txt"));

            // .. but if we have baseDirectory:firstDirectory, then it will be true
            Assert.IsTrue(FileUtilities.LooksLikeUnixFilePath("second/file.txt", firstDirectory));
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

    [UnixOnlyFact]
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
            Assert.AreEqual("second\\file.txt", FileUtilities.MaybeAdjustFilePath("second\\file.txt"));

            // .. but if we have baseDirectory:firstDirectory, then it will
            Assert.AreEqual("second/file.txt", FileUtilities.MaybeAdjustFilePath("second\\file.txt", firstDirectory));
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

    /// <summary>
    /// MaybeAdjustFilePath should correctly convert backslashes to forward slashes
    /// when CurrentThreadWorkingDirectory is set and baseDirectory is omitted.
    /// </summary>
    [UnixOnlyFact]
    public void MaybeAdjustFilePath_UsesCurrentThreadWorkingDirectory_WhenBaseDirectoryOmitted()
    {
        string filePath = ObjectModelHelpers.CreateFileInTempProjectDirectory("obj/Debug/file.txt", string.Empty);
        string projectDirectory = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(filePath)));
        string unrelatedDirectory = Path.GetTempPath();
        string oldCWD = Directory.GetCurrentDirectory();

        try
        {
            // Set process CWD somewhere unrelated (simulating stale CWD in MT mode)
            Directory.SetCurrentDirectory(unrelatedDirectory);

            // Simulate MT mode: set thread-local working directory to the project dir
            FileUtilities.CurrentThreadWorkingDirectory = projectDirectory;
            FileUtilities.MaybeAdjustFilePath("obj\\Debug\\file.txt").ShouldBe("obj/Debug/file.txt");
        }
        finally
        {
            FileUtilities.CurrentThreadWorkingDirectory = null;
            Directory.SetCurrentDirectory(oldCWD);
            File.Delete(filePath);
        }
    }

    private static string SystemSpecificAbsolutePath => typeof(BuildEnvironmentHelper).GetAssemblyPath();

    [MSBuildTestMethod]
    public void GetFolderAboveTest()
    {
        string root = NativeMethodsShared.IsWindows ? @"c:\" : "/";
        string path = Path.Combine(root, "1", "2", "3", "4", "5");

        Assert.AreEqual(Path.Combine(root, "1", "2", "3", "4", "5"), FileUtilities.GetFolderAbove(path, 0));
        Assert.AreEqual(Path.Combine(root, "1", "2", "3", "4"), FileUtilities.GetFolderAbove(path));
        Assert.AreEqual(Path.Combine(root, "1", "2", "3"), FileUtilities.GetFolderAbove(path, 2));
        Assert.AreEqual(Path.Combine(root, "1", "2"), FileUtilities.GetFolderAbove(path, 3));
        Assert.AreEqual(Path.Combine(root, "1"), FileUtilities.GetFolderAbove(path, 4));
        Assert.AreEqual(root, FileUtilities.GetFolderAbove(path, 5));
        Assert.AreEqual(root, FileUtilities.GetFolderAbove(path, 99));

        Assert.AreEqual(root, FileUtilities.GetFolderAbove(root, 99));
    }

    [MSBuildTestMethod]
    public void CombinePathsTest()
    {
        // These tests run in .NET 4+, so we can cheat
        var root = @"c:\";

        Assert.AreEqual(
            Path.Combine(root, "path1"),
            FileUtilities.CombinePaths(root, "path1"));

        Assert.AreEqual(
            Path.Combine(root, "path1", "path2", "file.txt"),
            FileUtilities.CombinePaths(root, "path1", "path2", "file.txt"));
    }

    [MSBuildTestMethod]
    [DataRow(@"c:\a\.\b", true)]
    [DataRow(@"c:\a\..\b", true)]
    [DataRow(@"c:\a\..", true)]
    [DataRow(@"c:\a\.", true)]
    [DataRow(@".\a", true)]
    [DataRow(@"..\b", true)]
    [DataRow(@"..", true)]
    [DataRow(@".", true)]
    [DataRow(@"..\", true)]
    [DataRow(@".\", true)]
    [DataRow(@"\..", true)]
    [DataRow(@"\.", true)]
    [DataRow(@"..\..\a", true)]
    [DataRow(@"..\..\..\a", true)]
    [DataRow(@"b..\", false)]
    [DataRow(@"b.\", false)]
    [DataRow(@"\b..", false)]
    [DataRow(@"\b.", false)]
    [DataRow(@"\b..\", false)]
    [DataRow(@"\b.\", false)]
    [DataRow(@"...", false)]
    [DataRow(@"....", false)]
    public void ContainsRelativeSegmentsTest(string path, bool expectedResult)
    {
        FileUtilities.ContainsRelativePathSegments(path).ShouldBe(expectedResult);
    }

    [MSBuildTestMethod]
    [DataRow("a/b/c/d", 0, "")]
    [DataRow("a/b/c/d", 1, "d")]
    [DataRow("a/b/c/d", 2, "c/d")]
    [DataRow("a/b/c/d", 3, "b/c/d")]
    [DataRow("a/b/c/d", 4, "a/b/c/d")]
    [DataRow("a/b/c/d", 5, "a/b/c/d")]
    [DataRow(@"a\/\/\//b/\/\/\//c//\/\/\/d/\//\/\/", 2, "c/d")]
    public void TestTruncatePathToTrailingSegments(string path, int trailingSegments, string expectedTruncatedPath)
    {
        expectedTruncatedPath = expectedTruncatedPath.Replace('/', Path.DirectorySeparatorChar);

        FileUtilities.TruncatePathToTrailingSegments(path, trailingSegments).ShouldBe(expectedTruncatedPath);
    }

    /// <summary>
    /// Exercises FileUtilities.EnsureSingleQuotes
    /// </summary>
    [MSBuildTestMethod]
    [DataRow(null, null)] // Null test
    [DataRow("", "")] // Empty string test
    [DataRow(@" ", @"' '")] // One character which is a space
    [DataRow(@"'", @"'''")] // One character which is a single quote
    [DataRow(@"""", @"'""'")] // One character which is a double quote
    [DataRow(@"example", @"'example'")] // Unquoted string
    [DataRow(@"'example'", @"'example'")] // Single quoted string
    [DataRow(@"""example""", @"'example'")] // Double quoted string
    [DataRow(@"'example""", @"''example""'")] // Mixed Quotes - Leading Single
    [DataRow(@"""example'", @"'""example''")] // Mixed Quotes - Leading Double
    [DataRow(@"ex""am'ple", @"'ex""am'ple'")] // Interior Quotes
    public void EnsureSingleQuotesTest(string path, string expectedResult)
    {
        FileUtilities.EnsureSingleQuotes(path).ShouldBe(expectedResult);
    }

    /// <summary>
    /// Exercises FileUtilities.EnsureDoubleQuotes
    /// </summary>
    [MSBuildTestMethod]
    [DataRow(null, null)] // Null test
    [DataRow("", "")] // Empty string test
    [DataRow(@" ", @""" """)] // One character which is a space
    [DataRow(@"'", @"""'""")] // One character which is a single quote
    [DataRow(@"""", @"""""""")] // One character which is a double quote
    [DataRow(@"example", @"""example""")] // Unquoted string
    [DataRow(@"'example'", @"""example""")] // Single quoted string
    [DataRow(@"""example""", @"""example""")] // Double quoted string
    [DataRow(@"'example""", @"""'example""""")] // Mixed Quotes - Leading Single
    [DataRow(@"""example'", @"""""example'""")] // Mixed Quotes - Leading Double
    [DataRow(@"ex""am'ple", @"""ex""am'ple""")] // Interior Quotes
    public void EnsureDoubleQuotesTest(string path, string expectedResult)
    {
        FileUtilities.EnsureDoubleQuotes(path).ShouldBe(expectedResult);
    }
}
