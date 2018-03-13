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
    sealed public class CombinePath_Tests
    {
        /// <summary>
        /// Base path is relative.  Paths are relative.
        /// </summary>
        [Fact]
        public void RelativeRelative1()
        {
            CombinePath t = new CombinePath();
            t.BuildEngine = new MockEngine();

            t.BasePath = Path.Combine("abc", "def");
            string path1 = "ghi.txt";
            string fullPath1 = Path.Combine(t.BasePath, path1);
            string path2 = Path.Combine("jkl", "mno.txt");
            string fullPath2 = Path.Combine(t.BasePath, path2);
            t.Paths = new ITaskItem[] { new TaskItem(path1), new TaskItem(path2) };
            Assert.True(t.Execute()); // "success"

            ObjectModelHelpers.AssertItemsMatch(string.Format("{0}\r\n{1}", fullPath1, fullPath2), t.CombinedPaths, true);
        }

        /// <summary>
        /// Base path is relative.  Paths are absolute.
        /// </summary>
        [Fact]
        public void RelativeAbsolute1()
        {
            CombinePath t = new CombinePath();
            t.BuildEngine = new MockEngine();

            t.BasePath = Path.Combine("abc", "def");
            string path1 = NativeMethodsShared.IsWindows ? @"c:\ghi.txt" : "/ghi.txt";
            string path2 = NativeMethodsShared.IsWindows ? @"d:\jkl\mno.txt" : "/jkl/mno.txt";
            string path3 = @"\\myserver\myshare";
            string pathsToMatch = string.Format(NativeMethodsShared.IsWindows ? @"
                {0}
                {1}
                {2}
                " : @"
                {0}
                {1}
                ", path1, path2, path3);

            t.Paths = NativeMethodsShared.IsWindows
                          ? new ITaskItem[] { new TaskItem(path1), new TaskItem(path2), new TaskItem(path3) }
                          : new ITaskItem[] { new TaskItem(path1), new TaskItem(path2) };
            Assert.True(t.Execute()); // "success"

            ObjectModelHelpers.AssertItemsMatch(pathsToMatch, t.CombinedPaths, true);
        }

        /// <summary>
        /// Base path is absolute.  Paths are relative.
        /// </summary>
        [Fact]
        public void AbsoluteRelative1()
        {
            CombinePath t = new CombinePath();
            t.BuildEngine = new MockEngine();

            t.BasePath = NativeMethodsShared.IsWindows ? @"c:\abc\def" : "/abc/def";
            string path1 = Path.DirectorySeparatorChar + Path.Combine("ghi", "jkl.txt");
            string path2 = Path.Combine("mno", "qrs.txt");
            string fullPath2 = Path.Combine(t.BasePath, path2);

            t.Paths = new ITaskItem[] { new TaskItem(path1), new TaskItem(path2) };
            Assert.True(t.Execute()); // "success"

            ObjectModelHelpers.AssertItemsMatch(string.Format("{0}\r\n{1}", path1, fullPath2), t.CombinedPaths, true);
        }

        /// <summary>
        /// Base path is absolute.  Paths are absolute.
        /// </summary>
        [Fact]
        public void AbsoluteAbsolute1()
        {
            CombinePath t = new CombinePath();
            t.BuildEngine = new MockEngine();

            t.BasePath = NativeMethodsShared.IsWindows ? @"\\fileserver\public" : "/rootdir/public";
            string path1 = NativeMethodsShared.IsWindows ? @"c:\ghi.txt" : "/ghi.txt";
            string path2 = NativeMethodsShared.IsWindows ? @"d:\jkl\mno.txt" : "/jkl/mno.txt";
            string path3 = @"\\myserver\myshare";
            string pathsToMatch = string.Format(NativeMethodsShared.IsWindows ? @"
                {0}
                {1}
                {2}
                " : @"
                {0}
                {1}
                ", path1, path2, path3);
            t.Paths = NativeMethodsShared.IsWindows
                          ? new ITaskItem[] { new TaskItem(path1), new TaskItem(path2), new TaskItem(path3) }
                          : new ITaskItem[] { new TaskItem(path1), new TaskItem(path2) };
            Assert.True(t.Execute()); // "success"

            ObjectModelHelpers.AssertItemsMatch(pathsToMatch, t.CombinedPaths, true);
        }

        /// <summary>
        /// All item metadata from the paths should be preserved when producing the output items.
        /// </summary>
        [Fact]
        public void MetadataPreserved()
        {
            CombinePath t = new CombinePath();
            t.BuildEngine = new MockEngine();
            string expected;

            if (NativeMethodsShared.IsWindows)
            {
                t.BasePath = @"c:\abc\def\";
                t.Paths = new ITaskItem[] { new TaskItem(@"jkl\mno.txt") };
                expected = @"
                c:\abc\def\jkl\mno.txt : Culture=english
                ";
            }
            else
            {
                t.BasePath = "/abc/def/";
                t.Paths = new ITaskItem[] { new TaskItem("jkl/mno.txt") };
                expected = @"
                /abc/def/jkl/mno.txt : Culture=english
                ";
            }
            t.Paths[0].SetMetadata("Culture", "english");
            Assert.True(t.Execute()); // "success"

            ObjectModelHelpers.AssertItemsMatch(expected, t.CombinedPaths, true);
        }

        /// <summary>
        /// No base path passed in should be treated as a blank base path, which means that
        /// the original paths are returned untouched.
        /// </summary>
        [Fact]
        public void NoBasePath()
        {
            CombinePath t = new CombinePath();
            t.BuildEngine = new MockEngine();

            t.Paths = new ITaskItem[] { new TaskItem(@"jkl\mno.txt"), new TaskItem(@"c:\abc\def\ghi.txt") };
            Assert.True(t.Execute()); // "success"

            ObjectModelHelpers.AssertItemsMatch(@"
                jkl\mno.txt
                c:\abc\def\ghi.txt
                ", t.CombinedPaths, true);
        }

        /// <summary>
        /// Passing in an array of zero paths.  Task should succeed and return zero paths.
        /// </summary>
        [Fact]
        public void NoPaths()
        {
            CombinePath t = new CombinePath();
            t.BuildEngine = new MockEngine();

            t.BasePath = @"c:\abc\def";
            t.Paths = new ITaskItem[0];
            Assert.True(t.Execute()); // "success"

            ObjectModelHelpers.AssertItemsMatch(@"
                ", t.CombinedPaths, true);
        }

        /// <summary>
        /// Passing in a (blank) path.  Task should simply return the base path.
        /// </summary>
        [Fact]
        public void BlankPath()
        {
            CombinePath t = new CombinePath();
            t.BuildEngine = new MockEngine();

            t.BasePath = @"c:\abc\def";
            t.Paths = new ITaskItem[] { new TaskItem("") };
            Assert.True(t.Execute()); // "success"

            ObjectModelHelpers.AssertItemsMatch(@"
                c:\abc\def
                ", t.CombinedPaths, true);
        }

        /// <summary>
        /// Specified paths contain invalid characters.  Task should continue processing remaining items.
        /// </summary>
        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)] // No invalid characters on Unix
        [SkipOnTargetFramework(TargetFrameworkMonikers.Netcoreapp)]
        public void InvalidPath()
        {
            CombinePath t = new CombinePath();
            t.BuildEngine = new MockEngine(true);

            t.BasePath = @"c:\abc\def";
            t.Paths = new ITaskItem[] { new TaskItem("ghi.txt"), new TaskItem("|.txt"), new TaskItem("jkl.txt") };
            Assert.False(t.Execute()); // "should have failed"
            ((MockEngine)t.BuildEngine).AssertLogContains("MSB3095");

            ObjectModelHelpers.AssertItemsMatch(@"
                c:\abc\def\ghi.txt
                c:\abc\def\jkl.txt
                ", t.CombinedPaths, true);
        }
    }
}
