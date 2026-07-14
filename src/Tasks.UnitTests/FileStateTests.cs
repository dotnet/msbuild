// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Test FileState utility class
    /// </summary>
    [TestClass]
    public class FileStateTests
    {
        /// <summary>
        /// Helper to create AbsolutePath for tests, bypassing rooted check for test paths.
        /// </summary>
        private static AbsolutePath TestPath(string path) => new AbsolutePath(path, ignoreRootedCheck: true);

        [MSBuildTestMethod]
        public void BadNoName()
        {
            Assert.ThrowsExactly<ArgumentException>(() =>
            {
                new FileState(TestPath(""));
            });
        }
        [MSBuildTestMethod]
        public void BadCharsCtorOK()
        {
            new FileState(TestPath("|"));
        }

        [MSBuildTestMethod]
        public void BadTooLongCtorOK()
        {
            new FileState(TestPath(new String('x', 5000)));
        }

        [WindowsFullFrameworkOnlyFact(additionalMessage: ".NET Core 2.1+ no longer validates paths: https://github.com/dotnet/corefx/issues/27779#issuecomment-371253486. On Unix there is no invalid file name characters.")]
        public void BadChars()
        {
            var state = new FileState(TestPath("|"));
            Assert.ThrowsExactly<ArgumentException>(() => { var time = state.LastWriteTime; });
        }

        [LongPathSupportDisabledTestMethod]
        public void BadTooLongLastWriteTime()
        {
            Helpers.VerifyAssertThrowsSameWay(
                delegate () { var x = new FileInfo(new String('x', 5000)).LastWriteTime; },
                delegate () { var x = new FileState(TestPath(new String('x', 5000))).LastWriteTime; });
        }

        [MSBuildTestMethod]
        public void Exists()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = new FileInfo(file);
                FileState state = new FileState(TestPath(file));

                Assert.AreEqual(info.Exists, state.FileExists);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [MSBuildTestMethod]
        public void Name()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = new FileInfo(file);
                FileState state = new FileState(TestPath(file));

                Assert.AreEqual(info.FullName, state.Path);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [MSBuildTestMethod]
        public void IsDirectoryTrue()
        {
            var state = new FileState(TestPath(Path.GetTempPath()));

            Assert.IsTrue(state.IsDirectory);
        }

        [MSBuildTestMethod]
        public void LastWriteTime()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = new FileInfo(file);
                FileState state = new FileState(TestPath(file));

                Assert.AreEqual(info.LastWriteTime, state.LastWriteTime);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [MSBuildTestMethod]
        public void LastWriteTimeUtc()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = new FileInfo(file);
                FileState state = new FileState(TestPath(file));

                Assert.AreEqual(info.LastWriteTimeUtc, state.LastWriteTimeUtcFast);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [MSBuildTestMethod]
        public void Length()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = new FileInfo(file);
                FileState state = new FileState(TestPath(file));

                Assert.AreEqual(info.Length, state.Length);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [MSBuildTestMethod]
        public void ReadOnly()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = new FileInfo(file);
                FileState state = new FileState(TestPath(file));

                Assert.AreEqual(info.IsReadOnly, state.IsReadOnly);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [MSBuildTestMethod]
        public void ExistsReset()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = new FileInfo(file);
                FileState state = new FileState(TestPath(file));

                Assert.AreEqual(info.Exists, state.FileExists);
                File.Delete(file);
                Assert.IsTrue(state.FileExists);
                state.Reset();
                Assert.IsFalse(state.FileExists);
            }
            finally
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }

        [MSBuildTestMethod]
        public void NameReset()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = new FileInfo(file);
                FileState state = new FileState(TestPath(file));

                Assert.AreEqual(info.FullName, state.Path);
                string originalName = info.FullName;
                string oldFile = file;
                file = oldFile + "2";
                File.Move(oldFile, file);
                Assert.AreEqual(originalName, state.Path);
                state.Reset();
                Assert.AreEqual(originalName, state.Path); // Name is from the constructor, didn't change
            }
            finally
            {
                File.Delete(file);
            }
        }

        [MSBuildTestMethod]
        public void LastWriteTimeReset()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = new FileInfo(file);
                FileState state = new FileState(TestPath(file));

                Assert.AreEqual(info.LastWriteTime, state.LastWriteTime);

                var time = new DateTime(2111, 1, 1);
                info.LastWriteTime = time;

                Assert.AreNotEqual(time, state.LastWriteTime);
                state.Reset();
                Assert.AreEqual(time, state.LastWriteTime);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [MSBuildTestMethod]
        public void LastWriteTimeUtcReset()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = new FileInfo(file);
                FileState state = new FileState(TestPath(file));

                Assert.AreEqual(info.LastWriteTimeUtc, state.LastWriteTimeUtcFast);

                var time = new DateTime(2111, 1, 1);
                info.LastWriteTime = time;

                Assert.AreNotEqual(time.ToUniversalTime(), state.LastWriteTimeUtcFast);
                state.Reset();
                Assert.AreEqual(time.ToUniversalTime(), state.LastWriteTimeUtcFast);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [MSBuildTestMethod]
        [TestCategory("netcore-osx-failing")]
        [TestCategory("netcore-linux-failing")]
        public void LengthReset()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = new FileInfo(file);
                FileState state = new FileState(TestPath(file));

                Assert.AreEqual(info.Length, state.Length);
                File.WriteAllText(file, "x");

                Assert.AreEqual(info.Length, state.Length);
                state.Reset();
                info.Refresh();
                Assert.AreEqual(info.Length, state.Length);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [MSBuildTestMethod]
        public void ReadOnlyReset()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = new FileInfo(file);
                FileState state = new FileState(TestPath(file));

                Assert.AreEqual(info.IsReadOnly, state.IsReadOnly);
                info.IsReadOnly = !info.IsReadOnly;
                state.Reset();
                Assert.IsTrue(state.IsReadOnly);
            }
            finally
            {
                (new FileInfo(file)).IsReadOnly = false;
                File.Delete(file);
            }
        }

        [MSBuildTestMethod]
        public void ExistsButDirectory()
        {
            Assert.AreEqual(new FileInfo(Path.GetTempPath()).Exists, new FileState(TestPath(Path.GetTempPath())).FileExists);
            Assert.IsTrue(new FileState(TestPath(Path.GetTempPath())).IsDirectory);
        }

        [MSBuildTestMethod]
        public void ReadOnlyOnDirectory()
        {
            Assert.AreEqual(new FileInfo(Path.GetTempPath()).IsReadOnly, new FileState(TestPath(Path.GetTempPath())).IsReadOnly);
        }

        [MSBuildTestMethod]
        public void LastWriteTimeOnDirectory()
        {
            Assert.AreEqual(new FileInfo(Path.GetTempPath()).LastWriteTime, new FileState(TestPath(Path.GetTempPath())).LastWriteTime);
        }

        [MSBuildTestMethod]
        public void LastWriteTimeUtcOnDirectory()
        {
            Assert.AreEqual(new FileInfo(Path.GetTempPath()).LastWriteTimeUtc, new FileState(TestPath(Path.GetTempPath())).LastWriteTimeUtcFast);
        }

        [MSBuildTestMethod]
        public void LengthOnDirectory()
        {
            Helpers.VerifyAssertThrowsSameWay(delegate () { var x = new FileInfo(Path.GetTempPath()).Length; }, delegate () { var x = new FileState(TestPath(Path.GetTempPath())).Length; });
        }

        [MSBuildTestMethod]
        [TestCategory("netcore-osx-failing")]
        [TestCategory("netcore-linux-failing")]
        public void DoesNotExistLastWriteTime()
        {
            string file = Guid.NewGuid().ToString("N");

            Assert.AreEqual(new FileInfo(file).LastWriteTime, new FileState(TestPath(file)).LastWriteTime);
        }

        [MSBuildTestMethod]
        [TestCategory("netcore-osx-failing")]
        [TestCategory("netcore-linux-failing")]
        public void DoesNotExistLastWriteTimeUtc()
        {
            string file = Guid.NewGuid().ToString("N");

            Assert.AreEqual(new FileInfo(file).LastWriteTimeUtc, new FileState(TestPath(file)).LastWriteTimeUtcFast);
        }

        [MSBuildTestMethod]
        public void DoesNotExistLength()
        {
            string file = Guid.NewGuid().ToString("N"); // presumably doesn't exist

            Helpers.VerifyAssertThrowsSameWay(delegate () { var x = new FileInfo(file).Length; }, delegate () { var x = new FileState(TestPath(file)).Length; });
        }

        [MSBuildTestMethod]
        public void DoesNotExistIsDirectory()
        {
            Assert.ThrowsExactly<FileNotFoundException>(() =>
            {
                string file = Guid.NewGuid().ToString("N"); // presumably doesn't exist

                var x = new FileState(TestPath(file)).IsDirectory;
            });
        }
        [MSBuildTestMethod]
        public void DoesNotExistDirectoryOrFileExists()
        {
            string file = Guid.NewGuid().ToString("N"); // presumably doesn't exist

            Assert.AreEqual(Directory.Exists(file), new FileState(TestPath(file)).DirectoryExists);
        }

        [MSBuildTestMethod]
        public void DoesNotExistParentFolderNotFound()
        {
            string file = Guid.NewGuid().ToString("N") + "\\x"; // presumably doesn't exist

            Assert.IsFalse(new FileState(TestPath(file)).FileExists);
            Assert.IsFalse(new FileState(TestPath(file)).DirectoryExists);
        }
    }
}
