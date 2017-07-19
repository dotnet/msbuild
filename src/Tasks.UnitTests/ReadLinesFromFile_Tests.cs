// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Threading;
#if FEATURE_SECURITY_PERMISSIONS
using System.Security.AccessControl;
#endif

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    sealed public class ReadLinesFromFile_Tests
    {
        /// <summary>
        /// Write one line, read one line.
        /// </summary>
        [Fact]
        public void Basic()
        {
            string file = FileUtilities.GetTemporaryFile();
            try
            {
                // Start with a missing file.
                File.Delete(file);

                // Append one line to the file.
                WriteLinesToFile a = new WriteLinesToFile();
                a.File = new TaskItem(file);
                a.Lines = new ITaskItem[] { new TaskItem("Line1") };
                Assert.True(a.Execute());

                // Read the line from the file.
                ReadLinesFromFile r = new ReadLinesFromFile();
                r.File = new TaskItem(file);
                Assert.True(r.Execute());

                Assert.Equal(1, r.Lines.Length);
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
            string file = FileUtilities.GetTemporaryFile();
            try
            {
                // Start with a missing file.
                File.Delete(file);

                // Append one line to the file.
                WriteLinesToFile a = new WriteLinesToFile();
                a.File = new TaskItem(file);
                a.Lines = new ITaskItem[] { new TaskItem("Line1_%253b_") };
                Assert.True(a.Execute());

                // Read the line from the file.
                ReadLinesFromFile r = new ReadLinesFromFile();
                r.File = new TaskItem(file);
                Assert.True(r.Execute());

                Assert.Equal(1, r.Lines.Length);
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
            string file = FileUtilities.GetTemporaryFile();
            try
            {
                // Start with a missing file.
                File.Delete(file);

                // Append one line to the file.
                WriteLinesToFile a = new WriteLinesToFile();
                a.File = new TaskItem(file);
                a.Lines = new ITaskItem[] { new TaskItem("My special character is \u00C3") };
                Assert.True(a.Execute());

                // Read the line from the file.
                ReadLinesFromFile r = new ReadLinesFromFile();
                r.File = new TaskItem(file);
                Assert.True(r.Execute());

                Assert.Equal(1, r.Lines.Length);
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
            string file = FileUtilities.GetTemporaryFile();
            File.Delete(file);

            // Read the line from the file.
            ReadLinesFromFile r = new ReadLinesFromFile();
            r.File = new TaskItem(file);
            Assert.True(r.Execute());

            Assert.Equal(0, r.Lines.Length);
        }

        /// <summary>
        /// Reading blank lines from a file should be ignored.
        /// </summary>
        [Fact]
        public void IgnoreBlankLines()
        {
            string file = FileUtilities.GetTemporaryFile();
            try
            {
                // Start with a missing file.
                File.Delete(file);

                // Append one line to the file.
                WriteLinesToFile a = new WriteLinesToFile();
                a.File = new TaskItem(file);
                a.Lines = new ITaskItem[]
                {
                    new TaskItem("Line1"),
                    new TaskItem("  "),
                    new TaskItem("Line2"),
                    new TaskItem(""),
                    new TaskItem("Line3"),
                    new TaskItem("\0\0\0\0\0\0\0\0\0")
                };
                Assert.True(a.Execute());

                // Read the line from the file.
                ReadLinesFromFile r = new ReadLinesFromFile();
                r.File = new TaskItem(file);
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

            string file = FileUtilities.GetTemporaryFile();
            try
            {
                // Start with a missing file.
                File.Delete(file);

                // Append one line to the file.
                WriteLinesToFile a = new WriteLinesToFile();
                a.File = new TaskItem(file);
                a.Lines = new ITaskItem[] { new TaskItem("This is a new line") };
                Assert.True(a.Execute());

                // Remove all File access to the file to current user
                FileSecurity fSecurity = File.GetAccessControl(file);
                string userAccount = string.Format(@"{0}\{1}", System.Environment.UserDomainName, System.Environment.UserName);
                fSecurity.AddAccessRule(new FileSystemAccessRule(userAccount, FileSystemRights.ReadData, AccessControlType.Deny));
                File.SetAccessControl(file, fSecurity);

                // Attempt to Read lines from the file.
                ReadLinesFromFile r = new ReadLinesFromFile();
                MockEngine mEngine = new MockEngine();
                r.BuildEngine = mEngine;
                r.File = new TaskItem(file);
                Assert.False(r.Execute());
            }
            finally
            {
                FileSecurity fSecurity = File.GetAccessControl(file);
                string userAccount = string.Format(@"{0}\{1}", System.Environment.UserDomainName, System.Environment.UserName);
                fSecurity.AddAccessRule(new FileSystemAccessRule(userAccount, FileSystemRights.ReadData, AccessControlType.Allow));
                File.SetAccessControl(file, fSecurity);

                // Delete file
                File.Delete(file);
            }
        }
#endif

        /// <summary>
        /// Invalid encoding
        /// </summary>
        [Fact]
        public void InvalidEncoding()
        {
            WriteLinesToFile a = new WriteLinesToFile();
            a.BuildEngine = new MockEngine();
            a.Encoding = "||invalid||";
            a.File = new TaskItem("c:\\" + Guid.NewGuid().ToString());
            a.Lines = new TaskItem[] { new TaskItem("x") };

            Assert.Equal(false, a.Execute());
            ((MockEngine)a.BuildEngine).AssertLogContains("MSB3098");
            Assert.Equal(false, File.Exists(a.File.ItemSpec));
        }

        /// <summary>
        /// Reading blank lines from a file should be ignored.
        /// </summary>
        [Fact]
        public void Encoding()
        {
            string file = FileUtilities.GetTemporaryFile();
            try
            {
                // Write default encoding: UTF8
                WriteLinesToFile a = new WriteLinesToFile();
                a.BuildEngine = new MockEngine();
                a.File = new TaskItem(file);
                a.Lines = new ITaskItem[] { new TaskItem("\uBDEA") };
                Assert.True(a.Execute());

                ReadLinesFromFile r = new ReadLinesFromFile();
                r.File = new TaskItem(file);
                Assert.True(r.Execute());

                Assert.Equal("\uBDEA", r.Lines[0].ItemSpec);

                File.Delete(file);

                // Write ANSI .. that won't work! 
                a = new WriteLinesToFile();
                a.BuildEngine = new MockEngine();
                a.File = new TaskItem(file);
                a.Lines = new ITaskItem[] { new TaskItem("\uBDEA") };
                a.Encoding = "ASCII";
                Assert.True(a.Execute());

                // Read the line from the file.
                r = new ReadLinesFromFile();
                r.File = new TaskItem(file);
                Assert.True(r.Execute());

                Assert.NotEqual("\uBDEA", r.Lines[0].ItemSpec);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Fact]
        public void WriteLinesWriteOnlyWhenDifferentTest()
        {
            string file = FileUtilities.GetTemporaryFile();
            try
            {
                // Write an initial file.
                WriteLinesToFile a = new WriteLinesToFile
                {
                    Overwrite = true,
                    BuildEngine = new MockEngine(),
                    File = new TaskItem(file),
                    WriteOnlyWhenDifferent = true,
                    Lines = new ITaskItem[] {new TaskItem("File contents1")}
                };

                a.Execute().ShouldBeTrue();

                // Verify contents
                ReadLinesFromFile r = new ReadLinesFromFile {File = new TaskItem(file)};
                r.Execute().ShouldBeTrue();
                r.Lines[0].ItemSpec.ShouldBe("File contents1");

                DateTime writeTime = DateTime.Now.AddHours(-1);

                File.SetLastWriteTime(file, writeTime);

                // Write the same contents to the file, timestamps should match.
                WriteLinesToFile a2 = new WriteLinesToFile
                {
                    Overwrite = true,
                    BuildEngine = new MockEngine(),
                    File = new TaskItem(file),
                    WriteOnlyWhenDifferent = true,
                    Lines = new ITaskItem[] {new TaskItem("File contents1")}
                };
                a2.Execute().ShouldBeTrue();
                File.GetLastWriteTime(file).ShouldBe(writeTime, tolerance: TimeSpan.FromSeconds(1));

                // Write different contents to the file, last write time should differ.
                WriteLinesToFile a3 = new WriteLinesToFile
                {
                    Overwrite = true,
                    BuildEngine = new MockEngine(),
                    File = new TaskItem(file),
                    WriteOnlyWhenDifferent = true,
                    Lines = new ITaskItem[] {new TaskItem("File contents2")}
                };

                a3.Execute().ShouldBeTrue();
                File.GetLastWriteTime(file).ShouldBeGreaterThan(writeTime.AddSeconds(1));
            }
            finally
            {
                File.Delete(file);
            }
        }
    }
}
