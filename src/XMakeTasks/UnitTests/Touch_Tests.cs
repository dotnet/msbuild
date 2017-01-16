// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    sealed public class Touch_Tests
    {
        internal static Microsoft.Build.Shared.FileExists fileExists = new Microsoft.Build.Shared.FileExists(FileExists);
        internal static Microsoft.Build.Shared.FileCreate fileCreate = new Microsoft.Build.Shared.FileCreate(FileCreate);
        internal static Microsoft.Build.Tasks.GetAttributes fileGetAttributes = new Microsoft.Build.Tasks.GetAttributes(GetAttributes);
        internal static Microsoft.Build.Tasks.SetAttributes fileSetAttributes = new Microsoft.Build.Tasks.SetAttributes(SetAttributes);
        internal static Microsoft.Build.Tasks.SetLastAccessTime setLastAccessTime = new Microsoft.Build.Tasks.SetLastAccessTime(SetLastAccessTime);
        internal static Microsoft.Build.Tasks.SetLastWriteTime setLastWriteTime = new Microsoft.Build.Tasks.SetLastWriteTime(SetLastWriteTime);

        private bool Execute(Touch t)
        {
            return t.ExecuteImpl
            (
                fileExists,
                fileCreate,
                fileGetAttributes,
                fileSetAttributes,
                setLastAccessTime,
                setLastWriteTime
            );
        }

        /// <summary>
        /// Mock file exists.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private static bool FileExists(string path)
        {
            if (path == @"c:\touch\myexisting.txt")
            {
                return true;
            }

            if (path == @"c:\touch\mynonexisting.txt")
            {
                return false;
            }

            if (path == @"c:\touch-nonexisting\file.txt")
            {
                return false;
            }

            if (path == @"c:\touch\myreadonly.txt")
            {
                return true;
            }
            Assert.True(false, "Unexpected file exists: " + path);

            return true;
        }

        /// <summary>
        /// Mock file create.
        /// </summary>
        /// <param name="path"></param>
        private static FileStream FileCreate(string path)
        {
            if (path == @"c:\touch\mynonexisting.txt")
            {
                return null;
            }

            if (path == @"c:\touch-nonexisting\file.txt")
            {
                throw new DirectoryNotFoundException();
            }


            Assert.True(false, "Unexpected file create: " + path);
            return null;
        }

        /// <summary>
        /// Mock get attributes.
        /// </summary>
        /// <param name="path"></param>
        private static FileAttributes GetAttributes(string path)
        {
            FileAttributes a = new FileAttributes();
            if (path == @"c:\touch\myexisting.txt")
            {
                return a;
            }

            if (path == @"c:\touch\mynonexisting.txt")
            {
                // Has attributes because Touch created it.
                return a;
            }

            if (path == @"c:\touch\myreadonly.txt")
            {
                a = System.IO.FileAttributes.ReadOnly;
                return a;
            }

            Assert.True(false, "Unexpected file attributes: " + path);
            return a;
        }

        /// <summary>
        /// Mock get attributes.
        /// </summary>
        /// <param name="path"></param>
        private static void SetAttributes(string path, FileAttributes attributes)
        {
            if (path == @"c:\touch\myreadonly.txt")
            {
                return;
            }
            Assert.True(false, "Unexpected set file attributes: " + path);
        }

        /// <summary>
        /// Mock SetLastAccessTime.
        /// </summary>
        /// <param name="path"></param>
        private static void SetLastAccessTime(string path, DateTime timestamp)
        {
            if (path == @"c:\touch\myexisting.txt")
            {
                return;
            }

            if (path == @"c:\touch\mynonexisting.txt")
            {
                return;
            }

            if (path == @"c:\touch\myreadonly.txt")
            {
                // Read-only so throw an exception
                throw new IOException();
            }

            Assert.True(false, "Unexpected set last access time: " + path);
        }

        /// <summary>
        /// Mock SetLastWriteTime.
        /// </summary>
        /// <param name="path"></param>
        private static void SetLastWriteTime(string path, DateTime timestamp)
        {
            if (path == @"c:\touch\myexisting.txt")
            {
                return;
            }

            if (path == @"c:\touch\mynonexisting.txt")
            {
                return;
            }

            if (path == @"c:\touch\myreadonly.txt")
            {
                return;
            }


            Assert.True(false, "Unexpected set last write time: " + path);
        }

        [Fact]
        public void TouchExisting()
        {
            Touch t = new Touch();
            MockEngine engine = new MockEngine();
            t.BuildEngine = engine;

            t.Files = new ITaskItem[]
            {
                new TaskItem(@"c:\touch\myexisting.txt")
            };

            bool success = Execute(t);

            Assert.True(success);

            Assert.Equal(1, t.TouchedFiles.Length);

            Assert.True(
                engine.Log.Contains
                (
                    String.Format(AssemblyResources.GetString("Touch.Touching"), "c:\\touch\\myexisting.txt")
                )
            );
        }

        [Fact]
        public void TouchNonExisting()
        {
            Touch t = new Touch();
            MockEngine engine = new MockEngine();
            t.BuildEngine = engine;

            t.Files = new ITaskItem[]
            {
                new TaskItem(@"c:\touch\mynonexisting.txt")
            };

            bool success = Execute(t);

            // Not success because the file doesn't exist
            Assert.False(success);

            Assert.True(
                engine.Log.Contains
                (
                    String.Format(AssemblyResources.GetString("Touch.FileDoesNotExist"), "c:\\touch\\mynonexisting.txt")
                )
            );
        }

        [Fact]
        public void TouchNonExistingAlwaysCreate()
        {
            Touch t = new Touch();
            MockEngine engine = new MockEngine();
            t.BuildEngine = engine;
            t.AlwaysCreate = true;

            t.Files = new ITaskItem[]
            {
                new TaskItem(@"c:\touch\mynonexisting.txt")
            };

            bool success = Execute(t);

            // Success because the file was created.
            Assert.True(success);

            Assert.True(
                engine.Log.Contains
                (
                    String.Format(AssemblyResources.GetString("Touch.CreatingFile"), "c:\\touch\\mynonexisting.txt", "AlwaysCreate")
                )
            );
        }

        [Fact]
        public void TouchNonExistingAlwaysCreateAndBadlyFormedTimestamp()
        {
            Touch t = new Touch();
            MockEngine engine = new MockEngine();
            t.BuildEngine = engine;
            t.AlwaysCreate = true;
            t.ForceTouch = false;
            t.Time = "Badly formed time String.";

            t.Files = new ITaskItem[]
            {
                new TaskItem(@"c:\touch\mynonexisting.txt")
            };

            bool success = Execute(t);

            // Failed because of badly formed time string.
            Assert.False(success);

            Assert.True(engine.Log.Contains("MSB3376"));
        }

        [Fact]
        public void TouchReadonly()
        {
            Touch t = new Touch();
            MockEngine engine = new MockEngine();
            t.BuildEngine = engine;
            t.AlwaysCreate = true;

            t.Files = new ITaskItem[]
            {
                new TaskItem(@"c:\touch\myreadonly.txt")
            };

            bool success = Execute(t);

            // Failed because file is readonly.
            Assert.False(success);

            Assert.True(engine.Log.Contains("MSB3374"));
            Assert.True(engine.Log.Contains(@"c:\touch\myreadonly.txt"));
        }

        [Fact]
        public void TouchReadonlyForce()
        {
            Touch t = new Touch();
            MockEngine engine = new MockEngine();
            t.BuildEngine = engine;
            t.ForceTouch = true;
            t.AlwaysCreate = true;

            t.Files = new ITaskItem[]
            {
                new TaskItem(@"c:\touch\myreadonly.txt")
            };

            bool success = Execute(t);
        }

        [Fact]
        public void TouchNonExistingDirectoryDoesntExist()
        {
            Touch t = new Touch();
            MockEngine engine = new MockEngine();
            t.BuildEngine = engine;
            t.AlwaysCreate = true;

            t.Files = new ITaskItem[]
            {
                new TaskItem(@"c:\touch-nonexisting\file.txt")
            };

            bool success = Execute(t);

            // Failed because the target directory didn't exist.
            Assert.False(success);

            Assert.True(engine.Log.Contains("MSB3371"));
            Assert.True(engine.Log.Contains(@"c:\touch-nonexisting\file.txt"));
        }
    }
}



