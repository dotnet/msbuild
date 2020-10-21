// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
#if FEATURE_SECURITY_PERMISSIONS
using System.Security.AccessControl;
#endif

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public sealed class ReadLinesFromFile_Tests
    {
        /// <summary>
        /// Write one line, read one line.
        /// </summary>
        [Fact]
        public void Basic()
        {
            var file = FileUtilities.GetTemporaryFile();
            try
            {
                // Start with a missing file.
                File.Delete(file);

                // Append one line to the file.
                var a = new WriteLinesToFile
                {
                    File = new TaskItem(file),
                    Lines = new ITaskItem[] { new TaskItem("Line1") }
                };
                Assert.True(a.Execute());

                // Read the line from the file.
                var r = new ReadLinesFromFile
                {
                    File = new TaskItem(file)
                };
                Assert.True(r.Execute());

                Assert.Single(r.Lines);
                Assert.Equal("Line1", r.Lines[0].ItemSpec);

                // Write two more lines to the file.
                a.Lines = new ITaskItem[] { new TaskItem("Line2"), new TaskItem("Line3") };
                Assert.True(a.Execute());

                // Read all of the lines and verify them.
                Assert.True(r.Execute());
                Assert.Equal(3, r.Lines.Length);
                Assert.Equal("Line1", r.Lines[0].ItemSpec);
                Assert.Equal("Line2", r.Lines[1].ItemSpec);
                Assert.Equal("Line3", r.Lines[2].ItemSpec);
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
        [Fact]
        public void Escaping()
        {
            var file = FileUtilities.GetTemporaryFile();
            try
            {
                // Start with a missing file.
                File.Delete(file);

                // Append one line to the file.
                var a = new WriteLinesToFile
                {
                    File = new TaskItem(file),
                    Lines = new ITaskItem[] { new TaskItem("Line1_%253b_") }
                };
                Assert.True(a.Execute());

                // Read the line from the file.
                var r = new ReadLinesFromFile
                {
                    File = new TaskItem(file)
                };
                Assert.True(r.Execute());

                Assert.Single(r.Lines);
                Assert.Equal("Line1_%3b_", r.Lines[0].ItemSpec);

                // Write two more lines to the file.
                a.Lines = new ITaskItem[] { new TaskItem("Line2"), new TaskItem("Line3") };
                Assert.True(a.Execute());

                // Read all of the lines and verify them.
                Assert.True(r.Execute());
                Assert.Equal(3, r.Lines.Length);
                Assert.Equal("Line1_%3b_", r.Lines[0].ItemSpec);
                Assert.Equal("Line2", r.Lines[1].ItemSpec);
                Assert.Equal("Line3", r.Lines[2].ItemSpec);
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Write a line that contains an ANSI character that is not ASCII.
        /// </summary>
        [Fact]
        public void ANSINonASCII()
        {
            var file = FileUtilities.GetTemporaryFile();
            try
            {
                // Start with a missing file.
                File.Delete(file);

                // Append one line to the file.
                var a = new WriteLinesToFile
                {
                    File = new TaskItem(file),
                    Lines = new ITaskItem[] { new TaskItem("My special character is \u00C3") }
                };
                Assert.True(a.Execute());

                // Read the line from the file.
                var r = new ReadLinesFromFile
                {
                    File = new TaskItem(file)
                };
                Assert.True(r.Execute());

                Assert.Single(r.Lines);
                Assert.Equal("My special character is \u00C3", r.Lines[0].ItemSpec);
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Reading lines from an missing file should result in the empty list.
        /// </summary>
        [Fact]
        public void ReadMissing()
        {
            var file = FileUtilities.GetTemporaryFile();
            File.Delete(file);

            // Read the line from the file.
            var r = new ReadLinesFromFile
            {
                File = new TaskItem(file)
            };
            Assert.True(r.Execute());

            Assert.Empty(r.Lines);
        }

        /// <summary>
        /// Reading blank lines from a file should be ignored.
        /// </summary>
        [Fact]
        public void IgnoreBlankLines()
        {
            var file = FileUtilities.GetTemporaryFile();
            try
            {
                // Start with a missing file.
                File.Delete(file);

                // Append one line to the file.
                var a = new WriteLinesToFile
                {
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
                Assert.True(a.Execute());

                // Read the line from the file.
                var r = new ReadLinesFromFile
                {
                    File = new TaskItem(file)
                };
                Assert.True(r.Execute());

                Assert.Equal(3, r.Lines.Length);
                Assert.Equal("Line1", r.Lines[0].ItemSpec);
                Assert.Equal("Line2", r.Lines[1].ItemSpec);
                Assert.Equal("Line3", r.Lines[2].ItemSpec);
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
        [Fact]
        public void ReadNoAccess()
        {
            if (NativeMethodsShared.IsUnixLike)
            {
                return; // "The security API is not the same under Unix"
            }

            var file = FileUtilities.GetTemporaryFile();
            try
            {
                // Start with a missing file.
                File.Delete(file);

                // Append one line to the file.
                var a = new WriteLinesToFile
                {
                    File = new TaskItem(file),
                    Lines = new ITaskItem[] { new TaskItem("This is a new line") }
                };
                Assert.True(a.Execute());

                // Remove all File access to the file to current user
                var fSecurity = File.GetAccessControl(file);
                var userAccount = string.Format(@"{0}\{1}", System.Environment.UserDomainName, System.Environment.UserName);
                fSecurity.AddAccessRule(new FileSystemAccessRule(userAccount, FileSystemRights.ReadData, AccessControlType.Deny));
                File.SetAccessControl(file, fSecurity);

                // Attempt to Read lines from the file.
                var r = new ReadLinesFromFile();
                var mEngine = new MockEngine();
                r.BuildEngine = mEngine;
                r.File = new TaskItem(file);
                Assert.False(r.Execute());
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
