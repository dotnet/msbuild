// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using System.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using System.Text.RegularExpressions;
using System.Globalization;

using NUnit.Framework;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Test FileState utility class
    /// </summary>
    [TestFixture]
    public class FileStateTests
    {
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void BadNoName()
        {
            new FileState("");
        }

        [Test]
        public void BadCharsCtorOK()
        {
            new FileState("|");
        }

        [Test]
        public void BadTooLongCtorOK()
        {
            new FileState(new String('x', 5000));
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void BadChars()
        {
            var state = new FileState("|");
            var time = state.LastWriteTime;
        }

        [Test]
        public void BadTooLongLastWriteTime()
        {
            Helpers.VerifyAssertThrowsSameWay(delegate () { var x = new FileInfo(new String('x', 5000)).LastWriteTime; }, delegate () { var x = new FileState(new String('x', 5000)).LastWriteTime; });
        }

        [Test]
        public void Exists()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = new FileInfo(file);
                FileState state = new FileState(file);

                Assert.AreEqual(info.Exists, state.FileExists);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Test]
        public void Name()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = new FileInfo(file);
                FileState state = new FileState(file);

                Assert.AreEqual(info.FullName, state.Name);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Test]
        public void IsDirectoryTrue()
        {
            var state = new FileState(Path.GetTempPath());

            Assert.AreEqual(true, state.IsDirectory);
        }

        [Test]
        public void LastWriteTime()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = new FileInfo(file);
                FileState state = new FileState(file);

                Assert.AreEqual(info.LastWriteTime, state.LastWriteTime);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Test]
        public void LastWriteTimeUtc()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = new FileInfo(file);
                FileState state = new FileState(file);

                Assert.AreEqual(info.LastWriteTimeUtc, state.LastWriteTimeUtcFast);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Test]
        public void Length()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = new FileInfo(file);
                FileState state = new FileState(file);

                Assert.AreEqual(info.Length, state.Length);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Test]
        public void AccessDenied()
        {
            string locked = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "system32\\notepad.exe");

            FileInfo info = new FileInfo(locked);
            FileState state = new FileState(locked);

            Assert.AreEqual(info.Exists, state.FileExists);

            if (!info.Exists)
            {
                // meh, somewhere else
                return;
            }

            Assert.AreEqual(info.Length, state.Length);
            Assert.AreEqual(info.LastWriteTime, state.LastWriteTime);
            Assert.AreEqual(info.LastWriteTimeUtc, state.LastWriteTimeUtcFast);
        }

#if CHECKING4GBFILESWORK
        [Test]
        public void LengthHuge()
        {
            var bigFile = @"d:\proj\hugefile";
            //var dummy = new string('x', 10000000);
            //using (StreamWriter w = new StreamWriter(bigFile))
            //{
            //    for (int i = 0; i < 450; i++)
            //    {
            //        w.Write(dummy);
            //    }
            //}

            Console.WriteLine((new FileState(bigFile)).Length);

            FileInfo info = new FileInfo(bigFile);
            FileState state = new FileState(bigFile);

            Assert.AreEqual(info.Exists, state.Exists);

            if (!info.Exists)
            {
                // meh, somewhere else  
                return;
            }

            Assert.AreEqual(info.Length, state.Length);
        }
#endif

        [Test]
        public void ReadOnly()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = new FileInfo(file);
                FileState state = new FileState(file);

                Assert.AreEqual(info.IsReadOnly, state.IsReadOnly);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Test]
        public void ExistsReset()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = new FileInfo(file);
                FileState state = new FileState(file);

                Assert.AreEqual(info.Exists, state.FileExists);
                File.Delete(file);
                Assert.AreEqual(true, state.FileExists);
                state.Reset();
                Assert.AreEqual(false, state.FileExists);
            }
            finally
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }

        [Test]
        public void NameReset()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = new FileInfo(file);
                FileState state = new FileState(file);

                Assert.AreEqual(info.FullName, state.Name);
                string originalName = info.FullName;
                string oldFile = file;
                file = oldFile + "2";
                File.Move(oldFile, file);
                Assert.AreEqual(originalName, state.Name);
                state.Reset();
                Assert.AreEqual(originalName, state.Name); // Name is from the constructor, didn't change
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Test]
        public void LastWriteTimeReset()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = new FileInfo(file);
                FileState state = new FileState(file);

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

        [Test]
        public void LastWriteTimeUtcReset()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = new FileInfo(file);
                FileState state = new FileState(file);

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

        [Test]
        public void LengthReset()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = new FileInfo(file);
                FileState state = new FileState(file);

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

        [Test]
        public void ReadOnlyReset()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = new FileInfo(file);
                FileState state = new FileState(file);

                Assert.AreEqual(info.IsReadOnly, state.IsReadOnly);
                info.IsReadOnly = !info.IsReadOnly;
                state.Reset();
                Assert.AreEqual(true, state.IsReadOnly);
            }
            finally
            {
                (new FileInfo(file)).IsReadOnly = false;
                File.Delete(file);
            }
        }

        [Test]
        public void ExistsButDirectory()
        {
            Assert.AreEqual(new FileInfo(Path.GetTempPath()).Exists, new FileState(Path.GetTempPath()).FileExists);
            Assert.AreEqual(true, (new FileState(Path.GetTempPath()).IsDirectory));
        }

        [Test]
        public void ReadOnlyOnDirectory()
        {
            Assert.AreEqual(new FileInfo(Path.GetTempPath()).IsReadOnly, new FileState(Path.GetTempPath()).IsReadOnly);
        }

        [Test]
        public void LastWriteTimeOnDirectory()
        {
            Assert.AreEqual(new FileInfo(Path.GetTempPath()).LastWriteTime, new FileState(Path.GetTempPath()).LastWriteTime);
        }

        [Test]
        public void LastWriteTimeUtcOnDirectory()
        {
            Assert.AreEqual(new FileInfo(Path.GetTempPath()).LastWriteTimeUtc, new FileState(Path.GetTempPath()).LastWriteTimeUtcFast);
        }

        [Test]
        public void LengthOnDirectory()
        {
            Helpers.VerifyAssertThrowsSameWay(delegate () { var x = new FileInfo(Path.GetTempPath()).Length; }, delegate () { var x = new FileState(Path.GetTempPath()).Length; });
        }

        [Test]
        public void DoesNotExistLastWriteTime()
        {
            string file = Guid.NewGuid().ToString("N");

            Assert.AreEqual(new FileInfo(file).LastWriteTime, new FileState(file).LastWriteTime);
        }

        [Test]
        public void DoesNotExistLastWriteTimeUtc()
        {
            string file = Guid.NewGuid().ToString("N");

            Assert.AreEqual(new FileInfo(file).LastWriteTimeUtc, new FileState(file).LastWriteTimeUtcFast);
        }

        [Test]
        public void DoesNotExistLength()
        {
            string file = Guid.NewGuid().ToString("N"); // presumably doesn't exist

            Helpers.VerifyAssertThrowsSameWay(delegate () { var x = new FileInfo(file).Length; }, delegate () { var x = new FileState(file).Length; });
        }

        [Test]
        [ExpectedException(typeof(FileNotFoundException))]
        public void DoesNotExistIsDirectory()
        {
            string file = Guid.NewGuid().ToString("N"); // presumably doesn't exist

            var x = new FileState(file).IsDirectory;
        }

        [Test]
        public void DoesNotExistDirectoryOrFileExists()
        {
            string file = Guid.NewGuid().ToString("N"); // presumably doesn't exist

            Assert.AreEqual(Directory.Exists(file), new FileState(file).DirectoryExists);
        }

        [Test]
        public void DoesNotExistParentFolderNotFound()
        {
            string file = Guid.NewGuid().ToString("N") + "\\x"; // presumably doesn't exist

            Assert.AreEqual(false, new FileState(file).FileExists);
            Assert.AreEqual(false, new FileState(file).DirectoryExists);
        }
    }
}





