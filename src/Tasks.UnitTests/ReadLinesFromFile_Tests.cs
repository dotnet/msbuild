// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
#if FEATURE_SECURITY_PERMISSIONS
using System.Security.AccessControl;
#endif

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public sealed class ReadLinesFromFile_Tests
    {
        /// <summary>
        /// Write one line, read one line.
        /// </summary>
        [MSBuildTestMethod]
        public void Basic()
        {
            // Start with a missing file.
            var file = FileUtilities.GetTemporaryFileName();
            try
            {
                // Append one line to the file.
                var a = new WriteLinesToFile
                {
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                    File = new TaskItem(file),
                    Lines = new ITaskItem[] { new TaskItem("Line1") }
                };
                Assert.IsTrue(a.Execute());

                // Read the line from the file.
                var r = new ReadLinesFromFile
                {
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                    File = new TaskItem(file)
                };
                Assert.IsTrue(r.Execute());

                Assert.ContainsSingle(r.Lines);
                Assert.AreEqual("Line1", r.Lines[0].ItemSpec);

                // Write two more lines to the file.
                a.Lines = new ITaskItem[] { new TaskItem("Line2"), new TaskItem("Line3") };
                Assert.IsTrue(a.Execute());

                // Read all of the lines and verify them.
                Assert.IsTrue(r.Execute());
                Assert.AreEqual(3, r.Lines.Length);
                Assert.AreEqual("Line1", r.Lines[0].ItemSpec);
                Assert.AreEqual("Line2", r.Lines[1].ItemSpec);
                Assert.AreEqual("Line3", r.Lines[2].ItemSpec);
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Write one line, read one line, where the line contains MSBuild-escapable characters.
        /// The file should contain the *unescaped* lines, but no escaping information should be
        /// lost when read.
        /// </summary>
        [MSBuildTestMethod]
        public void Escaping()
        {
            // Start with a missing file.
            var file = FileUtilities.GetTemporaryFileName();
            try
            {
                // Append one line to the file.
                var a = new WriteLinesToFile
                {
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                    File = new TaskItem(file),
                    Lines = new ITaskItem[] { new TaskItem("Line1_%253b_") }
                };
                Assert.IsTrue(a.Execute());

                // Read the line from the file.
                var r = new ReadLinesFromFile
                {
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                    File = new TaskItem(file)
                };
                Assert.IsTrue(r.Execute());

                Assert.ContainsSingle(r.Lines);
                Assert.AreEqual("Line1_%3b_", r.Lines[0].ItemSpec);

                // Write two more lines to the file.
                a.Lines = new ITaskItem[] { new TaskItem("Line2"), new TaskItem("Line3") };
                Assert.IsTrue(a.Execute());

                // Read all of the lines and verify them.
                Assert.IsTrue(r.Execute());
                Assert.AreEqual(3, r.Lines.Length);
                Assert.AreEqual("Line1_%3b_", r.Lines[0].ItemSpec);
                Assert.AreEqual("Line2", r.Lines[1].ItemSpec);
                Assert.AreEqual("Line3", r.Lines[2].ItemSpec);
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Write a line that contains an ANSI character that is not ASCII.
        /// </summary>
        [MSBuildTestMethod]
        public void ANSINonASCII()
        {
            // Start with a missing file.
            var file = FileUtilities.GetTemporaryFileName();
            try
            {
                // Append one line to the file.
                var a = new WriteLinesToFile
                {
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                    File = new TaskItem(file),
                    Lines = new ITaskItem[] { new TaskItem("My special character is \u00C3") }
                };
                Assert.IsTrue(a.Execute());

                // Read the line from the file.
                var r = new ReadLinesFromFile
                {
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                    File = new TaskItem(file)
                };
                Assert.IsTrue(r.Execute());

                Assert.ContainsSingle(r.Lines);
                Assert.AreEqual("My special character is \u00C3", r.Lines[0].ItemSpec);
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Reading lines from an missing file should result in the empty list.
        /// </summary>
        [MSBuildTestMethod]
        public void ReadMissing()
        {
            var file = FileUtilities.GetTemporaryFileName();

            // Read the line from the file.
            var r = new ReadLinesFromFile
            {
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                    File = new TaskItem(file)
            };
            Assert.IsTrue(r.Execute());

            Assert.IsEmpty(r.Lines);
        }

        /// <summary>
        /// Reading blank lines from a file should be ignored.
        /// </summary>
        [MSBuildTestMethod]
        public void IgnoreBlankLines()
        {
            // Start with a missing file.
            var file = FileUtilities.GetTemporaryFileName();
            try
            {
                // Append one line to the file.
                var a = new WriteLinesToFile
                {
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                    File = new TaskItem(file),
                    Lines = new ITaskItem[]
                {
                    new TaskItem("Line1"),
                    new TaskItem("  "),
                    new TaskItem("Line2"),
                    new TaskItem(""),
                    new TaskItem("Line3"),
                    new TaskItem("\0\0\0\0\0\0\0\0\0")
                }
                };
                Assert.IsTrue(a.Execute());

                // Read the line from the file.
                var r = new ReadLinesFromFile
                {
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                    File = new TaskItem(file)
                };
                Assert.IsTrue(r.Execute());

                Assert.AreEqual(3, r.Lines.Length);
                Assert.AreEqual("Line1", r.Lines[0].ItemSpec);
                Assert.AreEqual("Line2", r.Lines[1].ItemSpec);
                Assert.AreEqual("Line3", r.Lines[2].ItemSpec);
            }
            finally
            {
                File.Delete(file);
            }
        }

#if FEATURE_SECURITY_PERMISSIONS
        /// <summary>
        /// Reading lines from a file that you have no access to.
        /// </summary>
        [MSBuildTestMethod]
        public void ReadNoAccess()
        {
            if (NativeMethodsShared.IsUnixLike)
            {
                return; // "The security API is not the same under Unix"
            }

            // Start with a missing file.
            var file = FileUtilities.GetTemporaryFileName();
            try
            {
                // Append one line to the file.
                var a = new WriteLinesToFile
                {
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                    File = new TaskItem(file),
                    Lines = new ITaskItem[] { new TaskItem("This is a new line") }
                };
                Assert.IsTrue(a.Execute());

                // Remove all File access to the file to current user
                var fSecurity = File.GetAccessControl(file);
                var userAccount = string.Format(@"{0}\{1}", System.Environment.UserDomainName, System.Environment.UserName);
                fSecurity.AddAccessRule(new FileSystemAccessRule(userAccount, FileSystemRights.ReadData, AccessControlType.Deny));
                File.SetAccessControl(file, fSecurity);

                // Attempt to Read lines from the file.
                var r = new ReadLinesFromFile();
                var mEngine = new MockEngine();
                r.BuildEngine = mEngine;
                r.TaskEnvironment = TaskEnvironmentHelper.CreateForTest();
                r.File = new TaskItem(file);
                Assert.IsFalse(r.Execute());
            }
            finally
            {
                var fSecurity = File.GetAccessControl(file);
                var userAccount = string.Format(@"{0}\{1}", System.Environment.UserDomainName, System.Environment.UserName);
                fSecurity.AddAccessRule(new FileSystemAccessRule(userAccount, FileSystemRights.ReadData, AccessControlType.Allow));
                File.SetAccessControl(file, fSecurity);

                // Delete file
                File.Delete(file);
            }
        }
#endif

    }
}
