// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
#if FEATURE_SECURITY_PERMISSIONS
using System.Security.AccessControl;
#endif

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Xunit;
using PlatformID = Xunit.PlatformID;
using System.Security.Principal;

namespace Microsoft.Build.UnitTests
{
    public abstract class Copy_Tests : IDisposable
    {
        public bool useHardLinks = false;

        public bool useSymbolicLinks = false;
        /// <summary>
        /// Temporarily save off the value of MSBUILDALWAYSOVERWRITEREADONLYFILES, so that we can run 
        /// the tests isolated from the current state of the environment, but put it back how it belongs
        /// once we're done. 
        /// </summary>
        private string _alwaysOverwriteReadOnlyFiles = null;

        /// <summary>
        /// Temporarily save off the value of MSBUILDALWAYSRETRY, so that we can run 
        /// the tests isolated from the current state of the environment, but put it back how it belongs
        /// once we're done. 
        /// </summary>
        private string _alwaysRetry = null;

        /// <summary>
        /// There are a couple of environment variables that can affect the operation of the Copy
        /// task.  Make sure none of them are set. 
        /// </summary>
        public Copy_Tests()
        {
            _alwaysOverwriteReadOnlyFiles = Environment.GetEnvironmentVariable("MSBUILDALWAYSOVERWRITEREADONLYFILES");
            _alwaysRetry = Environment.GetEnvironmentVariable("MSBUILDALWAYSRETRY");

            Environment.SetEnvironmentVariable("MSBUILDALWAYSOVERWRITEREADONLYFILES", String.Empty);
            Environment.SetEnvironmentVariable("MSBUILDALWAYSRETRY", String.Empty);

            Copy.RefreshInternalEnvironmentValues();
        }

        /// <summary>
        /// Restore the environment variables we cleared out at the beginning of the test. 
        /// </summary>
        public void Dispose()
        {
            Environment.SetEnvironmentVariable("MSBUILDALWAYSOVERWRITEREADONLYFILES", _alwaysOverwriteReadOnlyFiles);
            Environment.SetEnvironmentVariable("MSBUILDALWAYSRETRY", _alwaysRetry);

            Copy.RefreshInternalEnvironmentValues();
        }

        /*
        * Method:   DontCopyOverSameFile
        *
        * If OnlyCopyIfDifferent is set to "true" then we shouldn't copy over files that
        * have the same date and time.
        */
        [Fact]
        public void DontCopyOverSameFile()
        {
            string file = FileUtilities.GetTemporaryFile();
            try
            {
                using (StreamWriter sw = FileUtilities.OpenWrite(file, true))   // HIGHCHAR: Test writes in UTF8 without preamble.
                    sw.Write("This is a temp file.");

                ITaskItem f = new TaskItem(file);
                ITaskItem[] sourceFiles = new ITaskItem[] { f };
                ITaskItem[] destinationFiles = new ITaskItem[] { f };

                CopyMonitor m = new CopyMonitor();
                Copy t = new Copy();
                t.RetryDelayMilliseconds = 1; // speed up tests!

                // Allow the task's default (false) to have a chance
                if (useHardLinks)
                {
                    t.UseHardlinksIfPossible = useHardLinks;
                }

                t.BuildEngine = new MockEngine();
                t.SourceFiles = sourceFiles;
                t.DestinationFiles = destinationFiles;
                t.SkipUnchangedFiles = true;

                t.Execute
                (
                    new Microsoft.Build.Tasks.CopyFileWithState(m.CopyFile)
                );

                // Expect for there to have been no copies.
                Assert.Equal(0, m.copyCount);

                ((MockEngine)t.BuildEngine).AssertLogDoesntContain("MSB3026"); // Didn't do retries
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Unless ignore readonly attributes is set, we should not copy over readonly files.
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void DoNotNormallyCopyOverReadOnlyFile()
        {
            string source = FileUtilities.GetTemporaryFile();
            string destination = FileUtilities.GetTemporaryFile();
            try
            {
                using (StreamWriter sw = FileUtilities.OpenWrite(source, true))
                    sw.Write("This is a source file.");
                using (StreamWriter sw = FileUtilities.OpenWrite(destination, true))
                    sw.Write("This is a destination file.");

                File.SetAttributes(destination, FileAttributes.ReadOnly);

                ITaskItem sourceItem = new TaskItem(source);
                ITaskItem destinationItem = new TaskItem(destination);
                ITaskItem[] sourceFiles = new ITaskItem[] { sourceItem };
                ITaskItem[] destinationFiles = new ITaskItem[] { destinationItem };

                Copy t = new Copy();
                t.RetryDelayMilliseconds = 1; // speed up tests!
                // Allow the task's default (false) to have a chance
                if (useHardLinks)
                {
                    t.UseHardlinksIfPossible = useHardLinks;
                }
                t.BuildEngine = new MockEngine();
                t.SourceFiles = sourceFiles;
                t.DestinationFiles = destinationFiles;
                t.SkipUnchangedFiles = true;
                // OverwriteReadOnlyFiles defaults to false

                // Should fail: target is readonly
                Assert.False(t.Execute());

                // Expect for there to have been no copies.
                Assert.Equal(0, t.CopiedFiles.Length);

                string destinationContent = File.ReadAllText(destination);
                Assert.Equal("This is a destination file.", destinationContent);

                ((MockEngine)t.BuildEngine).AssertLogDoesntContain("MSB3026"); // did not do retries as it was r/o
            }
            finally
            {
                File.SetAttributes(source, FileAttributes.Normal);
                File.SetAttributes(destination, FileAttributes.Normal);
                File.Delete(source);
                File.Delete(destination);
            }
        }

        /// <summary>
        /// If MSBUILDALWAYSOVERWRITEREADONLYFILES is set, then overwrite read-only even when 
        /// OverwriteReadOnlyFiles is false
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void CopyOverReadOnlyFileEnvironmentOverride()
        {
            string source = FileUtilities.GetTemporaryFile();
            string destination = FileUtilities.GetTemporaryFile();
            string oldAlwaysOverwriteValue = Environment.GetEnvironmentVariable("MSBUILDALWAYSOVERWRITEREADONLYFILES");

            try
            {
                Environment.SetEnvironmentVariable("MSBUILDALWAYSOVERWRITEREADONLYFILES", "1   ");

                using (StreamWriter sw = FileUtilities.OpenWrite(source, true))
                    sw.Write("This is a source file.");
                using (StreamWriter sw = FileUtilities.OpenWrite(destination, true))
                    sw.Write("This is a destination file.");

                File.SetAttributes(destination, FileAttributes.ReadOnly);

                ITaskItem sourceItem = new TaskItem(source);
                ITaskItem destinationItem = new TaskItem(destination);
                ITaskItem[] sourceFiles = new ITaskItem[] { sourceItem };
                ITaskItem[] destinationFiles = new ITaskItem[] { destinationItem };

                Copy t = new Copy();
                t.RetryDelayMilliseconds = 1; // speed up tests!
                // Allow the task's default (false) to have a chance
                if (useHardLinks)
                {
                    t.UseHardlinksIfPossible = useHardLinks;
                }
                t.BuildEngine = new MockEngine();
                t.SourceFiles = sourceFiles;
                t.DestinationFiles = destinationFiles;
                t.SkipUnchangedFiles = true;
                t.OverwriteReadOnlyFiles = false;

                // Should not fail although target is readonly
                Assert.True(t.Execute());

                // Should have copied file anyway
                Assert.Equal(1, t.CopiedFiles.Length);

                string destinationContent = File.ReadAllText(destination);
                Assert.Equal("This is a source file.", destinationContent);

                ((MockEngine)t.BuildEngine).AssertLogDoesntContain("MSB3026"); // Didn't do retries
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDALWAYSOVERWRITEREADONLYFILES", oldAlwaysOverwriteValue);

                File.Delete(source);
                File.Delete(destination);
            }
        }

        /// <summary>
        /// If MSBUILDALWAYSRETRY is set, keep retrying the copy. 
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void AlwaysRetryCopyEnvironmentOverride()
        {
            string source = FileUtilities.GetTemporaryFile();
            string destination = FileUtilities.GetTemporaryFile();
            string oldAlwaysOverwriteValue = Environment.GetEnvironmentVariable("MSBUILDALWAYSRETRY");

            try
            {
                Environment.SetEnvironmentVariable("MSBUILDALWAYSRETRY", "1   ");
                Copy.RefreshInternalEnvironmentValues();

                using (StreamWriter sw = FileUtilities.OpenWrite(source, true))
                    sw.Write("This is a source file.");
                using (StreamWriter sw = FileUtilities.OpenWrite(destination, true))
                    sw.Write("This is a destination file.");

                File.SetAttributes(destination, FileAttributes.ReadOnly);

                ITaskItem sourceItem = new TaskItem(source);
                ITaskItem destinationItem = new TaskItem(destination);
                ITaskItem[] sourceFiles = new ITaskItem[] { sourceItem };
                ITaskItem[] destinationFiles = new ITaskItem[] { destinationItem };

                Copy t = new Copy();
                t.RetryDelayMilliseconds = 1; // speed up tests!
                // Allow the task's default (false) to have a chance
                if (useHardLinks)
                {
                    t.UseHardlinksIfPossible = useHardLinks;
                }
                t.BuildEngine = new MockEngine();
                t.SourceFiles = sourceFiles;
                t.DestinationFiles = destinationFiles;
                t.SkipUnchangedFiles = true;
                t.OverwriteReadOnlyFiles = false;
                t.Retries = 5;

                // The file is read-only, so the retries will all fail. 
                Assert.False(t.Execute());

                // 3 warnings per retry, except the last one which has only two. 
                ((MockEngine)t.BuildEngine).AssertLogContains("MSB3026");
                Assert.Equal(((t.Retries + 1) * 3) - 1, ((MockEngine)t.BuildEngine).Warnings);

                // One error for "retrying failed", one error for "copy failed" 
                ((MockEngine)t.BuildEngine).AssertLogContains("MSB3027");
                ((MockEngine)t.BuildEngine).AssertLogContains("MSB3021");
                Assert.Equal(2, ((MockEngine)t.BuildEngine).Errors);
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDALWAYSRETRY", oldAlwaysOverwriteValue);
                Copy.RefreshInternalEnvironmentValues();

                File.SetAttributes(destination, FileAttributes.Normal);

                File.Delete(source);
                File.Delete(destination);
            }
        }

        /// <summary>
        /// Unless ignore readonly attributes is set, we should not copy over readonly files.
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void CopyOverReadOnlyFileParameterIsSet()
        {
            string source = FileUtilities.GetTemporaryFile();
            string destination = FileUtilities.GetTemporaryFile();
            try
            {
                using (StreamWriter sw = FileUtilities.OpenWrite(source, true))
                    sw.Write("This is a source file.");
                using (StreamWriter sw = FileUtilities.OpenWrite(destination, true))
                    sw.Write("This is a destination file.");

                File.SetAttributes(destination, FileAttributes.ReadOnly);

                ITaskItem sourceItem = new TaskItem(source);
                ITaskItem destinationItem = new TaskItem(destination);
                ITaskItem[] sourceFiles = new ITaskItem[] { sourceItem };
                ITaskItem[] destinationFiles = new ITaskItem[] { destinationItem };

                Copy t = new Copy();
                t.RetryDelayMilliseconds = 1; // speed up tests!
                // Allow the task's default (false) to have a chance
                if (useHardLinks)
                {
                    t.UseHardlinksIfPossible = useHardLinks;
                }
                t.BuildEngine = new MockEngine();
                t.SourceFiles = sourceFiles;
                t.DestinationFiles = destinationFiles;
                t.SkipUnchangedFiles = true;
                t.OverwriteReadOnlyFiles = true;

                // Should not fail although target is readonly
                Assert.True(t.Execute());

                // Should have copied file anyway
                Assert.Equal(1, t.CopiedFiles.Length);

                string destinationContent = File.ReadAllText(destination);
                Assert.Equal("This is a source file.", destinationContent);

                ((MockEngine)t.BuildEngine).AssertLogDoesntContain("MSB3026"); // Didn't do retries
            }
            finally
            {
                File.Delete(source);
                File.Delete(destination);
            }
        }

        /// <summary>
        /// Unless ignore readonly attributes is set, we should not copy over readonly files.
        /// </summary>
        [Fact]
        public void CopyOverReadOnlyFileParameterIsSetWithDestinationFolder()
        {
            string source1 = FileUtilities.GetTemporaryFile();
            string source2 = FileUtilities.GetTemporaryFile();
            string destinationFolder = Path.Combine(Path.GetTempPath(), "2A333ED756AF4dc392E728D0F874A398");
            string destination1 = Path.Combine(destinationFolder, Path.GetFileName(source1));
            string destination2 = Path.Combine(destinationFolder, Path.GetFileName(source2));
            try
            {
                Directory.CreateDirectory(destinationFolder);

                using (StreamWriter sw = FileUtilities.OpenWrite(source1, true))
                    sw.Write("This is a source file1.");
                using (StreamWriter sw = FileUtilities.OpenWrite(source2, true))
                    sw.Write("This is a source file2.");
                using (StreamWriter sw = FileUtilities.OpenWrite(destination1, true))
                    sw.Write("This is a destination file1.");
                using (StreamWriter sw = FileUtilities.OpenWrite(destination2, true))
                    sw.Write("This is a destination file2.");

                // Set one destination readonly.
                File.SetAttributes(destination1, FileAttributes.ReadOnly);

                ITaskItem sourceItem1 = new TaskItem(source1);
                ITaskItem sourceItem2 = new TaskItem(source2);
                ITaskItem[] sourceFiles = new ITaskItem[] { sourceItem1, sourceItem2 };

                Copy t = new Copy();
                t.RetryDelayMilliseconds = 1; // speed up tests!
                // Allow the task's default (false) to have a chance
                if (useHardLinks)
                {
                    t.UseHardlinksIfPossible = useHardLinks;
                }
                t.BuildEngine = new MockEngine();
                t.SourceFiles = sourceFiles;
                t.DestinationFolder = new TaskItem(destinationFolder);
                t.OverwriteReadOnlyFiles = true;

                // Should not fail although one target is readonly
                Assert.True(t.Execute());

                // Should have copied files anyway
                Assert.Equal(2, t.CopiedFiles.Length);

                string destinationContent1 = File.ReadAllText(destination1);
                Assert.Equal("This is a source file1.", destinationContent1);
                string destinationContent2 = File.ReadAllText(destination2);
                Assert.Equal("This is a source file2.", destinationContent2);

                Assert.NotEqual((File.GetAttributes(destination1) & FileAttributes.ReadOnly), FileAttributes.ReadOnly);
                Assert.NotEqual((File.GetAttributes(destination2) & FileAttributes.ReadOnly), FileAttributes.ReadOnly);

                ((MockEngine)t.BuildEngine).AssertLogDoesntContain("MSB3026"); // Didn't do retries
            }
            finally
            {
                File.SetAttributes(destination1, FileAttributes.Normal); // just in case
                File.SetAttributes(destination2, FileAttributes.Normal); // just in case
                File.Delete(source1);
                File.Delete(source2);
                File.Delete(destination1);
                File.Delete(destination2);
                FileUtilities.DeleteWithoutTrailingBackslash(destinationFolder, true);
            }
        }

        /*
         * Method:   DoCopyOverDifferentFile
         *
         * If OnlyCopyIfDifferent is set to "true" then we should still copy over files that
         * have different dates or sizes.
         */
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void DoCopyOverDifferentFile()
        {
            string sourceFile = FileUtilities.GetTemporaryFile();
            string destinationFile = FileUtilities.GetTemporaryFile();
            try
            {
                using (StreamWriter sw = FileUtilities.OpenWrite(sourceFile, true))    // HIGHCHAR: Test writes in UTF8 without preamble.
                    sw.Write("This is a source temp file.");

                using (StreamWriter sw = FileUtilities.OpenWrite(destinationFile, true))    // HIGHCHAR: Test writes in UTF8 without preamble.
                    sw.Write("This is a destination temp file.");

                ITaskItem[] sourceFiles = new ITaskItem[] { new TaskItem(sourceFile) };
                ITaskItem[] destinationFiles = new ITaskItem[] { new TaskItem(destinationFile) };

                Copy t = new Copy();
                t.RetryDelayMilliseconds = 1; // speed up tests!
                // Allow the task's default (false) to have a chance
                if (useHardLinks)
                {
                    t.UseHardlinksIfPossible = useHardLinks;
                }
                t.BuildEngine = new MockEngine();
                t.SourceFiles = sourceFiles;
                t.DestinationFiles = destinationFiles;
                t.SkipUnchangedFiles = true;

                t.Execute();

                string destinationFileContents;
                using (StreamReader sr = FileUtilities.OpenRead(destinationFile)) // HIGHCHAR: Test reads ASCII (not ANSI).
                    destinationFileContents = sr.ReadToEnd();

                Assert.Equal(destinationFileContents, "This is a source temp file."); //                     "Expected the destination file to contain the contents of source file."

                ((MockEngine)t.BuildEngine).AssertLogDoesntContain("MSB3026"); // Didn't do retries
            }
            finally
            {
                File.Delete(sourceFile);
                File.Delete(destinationFile);
            }
        }

        /*
         * Method:   DoCopyOverNonExistentFile
         *
         * If OnlyCopyIfDifferent is set to "true" then we should still copy over files that
         * don't exist.
         */
        [Fact]
        public void DoCopyOverNonExistentFile()
        {
            string sourceFile = FileUtilities.GetTemporaryFile();
            string destinationFile = FileUtilities.GetTemporaryFile();

            try
            {
                using (StreamWriter sw = FileUtilities.OpenWrite(sourceFile, true))    // HIGHCHAR: Test writes in UTF8 without preamble.
                    sw.Write("This is a source temp file.");

                using (StreamWriter sw = FileUtilities.OpenWrite(destinationFile, true))   // HIGHCHAR: Test writes in UTF8 without preamble.
                    sw.Write("This is a destination temp file.");

                ITaskItem[] sourceFiles = new ITaskItem[] { new TaskItem(sourceFile) };
                ITaskItem[] destinationFiles = new ITaskItem[] { new TaskItem(destinationFile) };

                File.Delete(destinationFile);

                Copy t = new Copy();
                t.RetryDelayMilliseconds = 1; // speed up tests!
                // Allow the task's default (false) to have a chance
                if (useHardLinks)
                {
                    t.UseHardlinksIfPossible = useHardLinks;
                }
                t.BuildEngine = new MockEngine();
                t.SourceFiles = sourceFiles;
                t.DestinationFiles = destinationFiles;
                t.SkipUnchangedFiles = true;

                t.Execute();

                Assert.True(File.Exists(destinationFile)); // "Expected the destination file to exist."
                ((MockEngine)t.BuildEngine).AssertLogDoesntContain("MSB3026"); // Didn't do retries
            }
            finally
            {
                File.Delete(sourceFile);
                File.Delete(destinationFile);
            }
        }

        /// <summary>
        /// Make sure we do not retry when the source file has a misplaced colon
        /// </summary>
        [Fact]
        public void DoNotRetryCopyNotSupportedException()
        {
            if (!NativeMethodsShared.IsWindows)
            {
                // Colon is special only on Windows
                return;
            }

            string sourceFile = FileUtilities.GetTemporaryFile();
            string destinationFile = "foo:bar";

            try
            {
                ITaskItem[] sourceFiles = new ITaskItem[] { new TaskItem(sourceFile) };
                ITaskItem[] destinationFiles = new ITaskItem[] { new TaskItem(destinationFile) };

                Copy t = new Copy();
                t.RetryDelayMilliseconds = 1; // speed up tests!
                // Allow the task's default (false) to have a chance
                if (useHardLinks)
                {
                    t.UseHardlinksIfPossible = useHardLinks;
                }
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;
                t.SourceFiles = sourceFiles;
                t.DestinationFiles = destinationFiles;
                t.SkipUnchangedFiles = true;

                bool result = t.Execute();
                Assert.False(result);
                Assert.Equal(1, engine.Errors);
                Assert.Equal(0, engine.Warnings);
                engine.AssertLogContains("MSB3021");
                ((MockEngine)t.BuildEngine).AssertLogDoesntContain("MSB3026"); // Didn't do retries
            }
            finally
            {
                File.Delete(sourceFile);
            }
        }

        /// <summary>
        /// Make sure we do not retry when the source file does not exist
        /// </summary>
        [Fact]
        public void DoNotRetryCopyNonExistentSourceFile()
        {
            string sourceFile = "Nannanacat";
            string destinationFile = FileUtilities.GetTemporaryFile();

            try
            {
                using (StreamWriter sw = FileUtilities.OpenWrite(destinationFile, true))   // HIGHCHAR: Test writes in UTF8 without preamble.
                    sw.Write("This is a destination temp file.");

                ITaskItem[] sourceFiles = new ITaskItem[] { new TaskItem(sourceFile) };
                ITaskItem[] destinationFiles = new ITaskItem[] { new TaskItem(destinationFile) };

                File.Delete(destinationFile);

                Copy t = new Copy();
                t.RetryDelayMilliseconds = 1; // speed up tests!
                // Allow the task's default (false) to have a chance
                if (useHardLinks)
                {
                    t.UseHardlinksIfPossible = useHardLinks;
                }
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;
                t.SourceFiles = sourceFiles;
                t.DestinationFiles = destinationFiles;
                t.SkipUnchangedFiles = true;

                bool result = t.Execute();
                Assert.False(result);
                Assert.Equal(1, engine.Errors);
                Assert.Equal(0, engine.Warnings);
                engine.AssertLogContains("MSB3030");
                ((MockEngine)t.BuildEngine).AssertLogDoesntContain("MSB3026"); // Didn't do retries
            }
            finally
            {
                File.Delete(destinationFile);
            }
        }

        /// <summary>
        /// Make sure we do not retry when the source file is a folder
        /// </summary>
        [Fact]
        public void DoNotRetryCopyWhenSourceIsFolder()
        {
            string sourceFile = Path.GetTempPath();
            string destinationFile = FileUtilities.GetTemporaryFile();

            try
            {
                using (StreamWriter sw = FileUtilities.OpenWrite(destinationFile, true))   // HIGHCHAR: Test writes in UTF8 without preamble.
                    sw.Write("This is a destination temp file.");

                ITaskItem[] sourceFiles = new ITaskItem[] { new TaskItem(sourceFile) };
                ITaskItem[] destinationFiles = new ITaskItem[] { new TaskItem(destinationFile) };

                File.Delete(destinationFile);

                Copy t = new Copy();
                t.RetryDelayMilliseconds = 1; // speed up tests!
                // Allow the task's default (false) to have a chance
                if (useHardLinks)
                {
                    t.UseHardlinksIfPossible = useHardLinks;
                }
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;
                t.SourceFiles = sourceFiles;
                t.DestinationFiles = destinationFiles;
                t.SkipUnchangedFiles = true;

                bool result = t.Execute();
                Assert.False(result);
                Assert.Equal(1, engine.Errors);
                Assert.Equal(0, engine.Warnings);
                engine.AssertLogContains("MSB3025");
                engine.AssertLogDoesntContain("MSB3026"); // Didn't do retries
            }
            finally
            {
                File.Delete(destinationFile);
            }
        }

        /// <summary>
        /// Most important case is when destination is locked
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void DoRetryWhenDestinationLocked()
        {
            string destinationFile = Path.GetTempFileName();
            string sourceFile = Path.GetTempFileName();

            try
            {
                using (StreamWriter sw = FileUtilities.OpenWrite(destinationFile, true)) // Keep it locked
                {
                    ITaskItem[] sourceFiles = new ITaskItem[] { new TaskItem(sourceFile) };

                    Copy t = new Copy();
                    t.RetryDelayMilliseconds = 1; // speed up tests!
                    // Allow the task's default (false) to have a chance
                    if (useHardLinks)
                    {
                        t.UseHardlinksIfPossible = useHardLinks;
                    }
                    MockEngine engine = new MockEngine();
                    t.BuildEngine = engine;
                    t.SourceFiles = sourceFiles;
                    t.DestinationFiles = new TaskItem[] { new TaskItem(destinationFile) };

                    bool result = t.Execute();
                    Assert.False(result);

                    engine.AssertLogContains("MSB3021"); // copy failed
                    engine.AssertLogContains("MSB3026"); // DID retry

                    Assert.Equal(2, engine.Errors); // retries failed, and actual failure
                    Assert.Equal(10, engine.Warnings);
                }
            }
            finally
            {
                File.Delete(sourceFile);
                File.Delete(destinationFile);
            }
        }

#if FEATURE_SECURITY_PERMISSIONS
        /// <summary>
        /// When destination is inaccessible due to ACL, do NOT retry
        /// </summary>
        [Fact]
        public void DoNotRetryWhenDestinationLockedDueToAcl()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), "DoNotRetryWhenDestinationLockedDueToAcl");
            string destinationFile = Path.Combine(tempDirectory, "DestinationFile.txt");
            string sourceFile = Path.Combine(tempDirectory, "SourceFile.txt");

            if (Directory.Exists(tempDirectory))
            {
                FileUtilities.DeleteDirectoryNoThrow(tempDirectory, true);
            }

            Directory.CreateDirectory(tempDirectory);

            File.WriteAllText(destinationFile, "Destination");
            File.WriteAllText(sourceFile, "SourceFile");

            string userAccount = string.Format(@"{0}\{1}", System.Environment.UserDomainName, System.Environment.UserName);

            FileSystemAccessRule denyFile = new FileSystemAccessRule(userAccount, FileSystemRights.Write | FileSystemRights.Delete | FileSystemRights.DeleteSubdirectoriesAndFiles | FileSystemRights.WriteData, AccessControlType.Deny);
            FileSystemAccessRule denyDirectory = new FileSystemAccessRule(userAccount, FileSystemRights.DeleteSubdirectoriesAndFiles, AccessControlType.Deny);

            FileSecurity fSecurity = File.GetAccessControl(destinationFile);
            DirectorySecurity dSecurity = Directory.GetAccessControl(tempDirectory);

            try
            {
                fSecurity.AddAccessRule(denyFile);
                File.SetAccessControl(destinationFile, fSecurity);

                dSecurity.AddAccessRule(denyDirectory);
                Directory.SetAccessControl(tempDirectory, dSecurity);

                Copy t = new Copy();
                t.RetryDelayMilliseconds = 1; // speed up tests!
                // Allow the task's default (false) to have a chance
                if (useHardLinks)
                {
                    t.UseHardlinksIfPossible = useHardLinks;
                }
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;
                t.SourceFiles = new TaskItem[] { new TaskItem(sourceFile) };
                t.DestinationFiles = new TaskItem[] { new TaskItem(destinationFile) };

                bool result = t.Execute();
                Assert.False(result);

                engine.AssertLogContains("MSB3021"); // copy failed
                engine.AssertLogDoesntContain("MSB3026"); // Didn't retry

                Assert.Equal(1, engine.Errors);
                Assert.Equal(0, engine.Warnings);
            }
            finally
            {
                fSecurity.RemoveAccessRule(denyFile);
                File.SetAccessControl(destinationFile, fSecurity);

                dSecurity.RemoveAccessRule(denyDirectory);
                Directory.SetAccessControl(tempDirectory, dSecurity);

                if (Directory.Exists(tempDirectory))
                {
                    FileUtilities.DeleteDirectoryNoThrow(tempDirectory, true);
                }
            }
        }
#endif

        /// <summary>
        /// Make sure we do not retry when the destination file is a folder
        /// </summary>
        [Fact]
        public void DoNotRetryCopyWhenDestinationFolderIsFile()
        {
            string destinationFile = FileUtilities.GetTemporaryFile();
            string sourceFile = FileUtilities.GetTemporaryFile();

            try
            {
                using (StreamWriter sw = FileUtilities.OpenWrite(sourceFile, true))
                    sw.Write("This is a destination temp file.");

                ITaskItem[] sourceFiles = new ITaskItem[] { new TaskItem(sourceFile) };

                Copy t = new Copy();
                t.RetryDelayMilliseconds = 1; // speed up tests!
                // Allow the task's default (false) to have a chance
                if (useHardLinks)
                {
                    t.UseHardlinksIfPossible = useHardLinks;
                }
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;
                t.SourceFiles = sourceFiles;
                t.DestinationFolder = new TaskItem(destinationFile);
                t.SkipUnchangedFiles = true;

                bool result = t.Execute();
                Assert.False(result);

                engine.AssertLogContains("MSB3021"); // copy failed
                engine.AssertLogDoesntContain("MSB3026"); // Didn't retry

                Assert.Equal(1, engine.Errors);
                Assert.Equal(0, engine.Warnings);
            }
            finally
            {
                File.Delete(sourceFile);
            }
        }

        /// <summary>
        /// Make sure we do not retry when the destination file is a folder
        /// </summary>
        [Fact]
        public void DoNotRetryCopyWhenDestinationFileIsFolder()
        {
            string destinationFile = Path.GetTempPath();
            string sourceFile = FileUtilities.GetTemporaryFile();

            try
            {
                using (StreamWriter sw = FileUtilities.OpenWrite(sourceFile, true))   // HIGHCHAR: Test writes in UTF8 without preamble.
                    sw.Write("This is a destination temp file.");

                ITaskItem[] sourceFiles = new ITaskItem[] { new TaskItem(sourceFile) };
                ITaskItem[] destinationFiles = new ITaskItem[] { new TaskItem(destinationFile) };

                Copy t = new Copy();
                t.RetryDelayMilliseconds = 1; // speed up tests!
                // Allow the task's default (false) to have a chance
                if (useHardLinks)
                {
                    t.UseHardlinksIfPossible = useHardLinks;
                }
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;
                t.SourceFiles = sourceFiles;
                t.DestinationFiles = destinationFiles;
                t.SkipUnchangedFiles = true;

                bool result = t.Execute();
                Assert.False(result);
                Assert.Equal(1, engine.Errors);
                Assert.Equal(0, engine.Warnings);
                engine.AssertLogContains("MSB3024");
                engine.AssertLogDoesntContain("MSB3026");
            }
            finally
            {
                File.Delete(sourceFile);
            }
        }

        internal class CopyMonitor
        {
            internal int copyCount = 0;

            /*
            * Method:   CopyFile
            *
            * Don't really copy the file, just count how many times this was called.
            */
            internal bool? CopyFile(FileState source, FileState destination)
            {
                ++copyCount;
                return true;
            }
        }

        /// <summary>
        /// CopiedFiles should only include files that were successfully copied 
        /// (or skipped), not files for which there was an error.
        /// </summary>
        [Fact]
        [PlatformSpecific(Xunit.PlatformID.Windows)] // "Under Unix all filenames are valid and this test is not useful"
        public void OutputsOnlyIncludeSuccessfulCopies()
        {
            string temp = Path.GetTempPath();
            string inFile1 = Path.Combine(temp, "2A333ED756AF4dc392E728D0F864A392");
            string inFile2 = Path.Combine(temp, "2A333ED756AF4dc392E728D0F864A393");
            string invalidFile = "!@#$%^&*()|";
            string validOutFile = Path.Combine(temp, "2A333ED756AF4dc392E728D0F864A394");

            try
            {
                FileStream fs = null;
                FileStream fs2 = null;

                try
                {
                    fs = File.Create(inFile1);
                    fs2 = File.Create(inFile2);
                }
                finally
                {
                    fs.Dispose();
                    fs2.Dispose();
                }

                Copy t = new Copy();
                t.RetryDelayMilliseconds = 1; // speed up tests!
                // Allow the task's default (false) to have a chance
                if (useHardLinks)
                {
                    t.UseHardlinksIfPossible = useHardLinks;
                }
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;

                ITaskItem i1 = new TaskItem(inFile1);
                i1.SetMetadata("Locale", "en-GB");
                i1.SetMetadata("Color", "taupe");
                t.SourceFiles = new ITaskItem[] { new TaskItem(inFile2), i1 };

                ITaskItem o1 = new TaskItem(validOutFile);
                o1.SetMetadata("Locale", "fr");
                o1.SetMetadata("Flavor", "Pumpkin");
                t.DestinationFiles = new ITaskItem[] { new TaskItem(invalidFile), o1 };

                bool success = t.Execute();

                Assert.False(success);
                Assert.Equal(1, t.CopiedFiles.Length);
                Assert.Equal(validOutFile, t.CopiedFiles[0].ItemSpec);
                Assert.Equal(2, t.DestinationFiles.Length);
                Assert.Equal("fr", t.DestinationFiles[1].GetMetadata("Locale"));

                // Output ItemSpec should not be overwritten.
                Assert.Equal(invalidFile, t.DestinationFiles[0].ItemSpec);
                Assert.Equal(validOutFile, t.DestinationFiles[1].ItemSpec);
                Assert.Equal(validOutFile, t.CopiedFiles[0].ItemSpec);

                // Sources attributes should be left untouched.
                Assert.Equal("en-GB", t.SourceFiles[1].GetMetadata("Locale"));
                Assert.Equal("taupe", t.SourceFiles[1].GetMetadata("Color"));

                // Attributes not on Sources should be left untouched.
                Assert.Equal("Pumpkin", t.DestinationFiles[1].GetMetadata("Flavor"));
                Assert.Equal("Pumpkin", t.CopiedFiles[0].GetMetadata("Flavor"));

                // Attribute should have been forwarded
                Assert.Equal("taupe", t.DestinationFiles[1].GetMetadata("Color"));
                Assert.Equal("taupe", t.CopiedFiles[0].GetMetadata("Color"));

                // Attribute should not have been updated if it already existed on destination
                Assert.Equal("fr", t.DestinationFiles[1].GetMetadata("Locale"));
                Assert.Equal("fr", t.CopiedFiles[0].GetMetadata("Locale"));

                ((MockEngine)t.BuildEngine).AssertLogDoesntContain("MSB3026"); // Didn't do retries
            }
            finally
            {
                File.Delete(inFile1);
                File.Delete(inFile2);
                File.Delete(validOutFile);
            }
        }

        /// <summary>
        /// Copying a file on top of itself should be a success (no-op) whether
        /// or not skipUnchangedFiles is true or false.
        /// </summary>
        [Fact]
        public void CopyFileOnItself()
        {
            string temp = Path.GetTempPath();
            string file = Path.Combine(temp, "2A333ED756AF4dc392E728D0F864A395");

            try
            {
                FileStream fs = null;

                try
                {
                    fs = File.Create(file);
                }
                finally
                {
                    fs.Dispose();
                }

                Copy t = new Copy();
                t.RetryDelayMilliseconds = 1; // speed up tests!
                // Allow the task's default (false) to have a chance
                if (useHardLinks)
                {
                    t.UseHardlinksIfPossible = useHardLinks;
                }
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;
                t.SourceFiles = new ITaskItem[] { new TaskItem(file) };
                t.DestinationFiles = new ITaskItem[] { new TaskItem(file) };
                t.SkipUnchangedFiles = true;
                bool success = t.Execute();

                Assert.True(success);
                Assert.Equal(1, t.DestinationFiles.Length);
                Assert.Equal(file, t.DestinationFiles[0].ItemSpec);

                ((MockEngine)t.BuildEngine).AssertLogDoesntContain("MSB3026"); // Didn't do retries, nothing to do

                t = new Copy();
                // Allow the task's default (false) to have a chance
                if (useHardLinks)
                {
                    t.UseHardlinksIfPossible = useHardLinks;
                }
                engine = new MockEngine();
                t.BuildEngine = engine;
                t.SourceFiles = new ITaskItem[] { new TaskItem(file) };
                t.DestinationFiles = new ITaskItem[] { new TaskItem(file) };
                t.SkipUnchangedFiles = false;

                success = t.Execute();

                Assert.True(success);
                Assert.Equal(1, t.DestinationFiles.Length);
                Assert.Equal(file, t.DestinationFiles[0].ItemSpec);
                Assert.Equal(1, t.CopiedFiles.Length);

                ((MockEngine)t.BuildEngine).AssertLogDoesntContain("MSB3026"); // Didn't do retries, nothing to do
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Copying a file on top of itself should be a success (no-op) whether
        /// or not skipUnchangedFiles is true or false. Variation with different casing/relativeness.
        /// </summary>
        [Fact]
        [PlatformSpecific(Xunit.PlatformID.Windows)] // "File names under Unix are case-sensitive and this test is not useful"
        public void CopyFileOnItself2()
        {
            string currdir = Directory.GetCurrentDirectory();
            string filename = "2A333ED756AF4dc392E728D0F864A396";
            string file = Path.Combine(currdir, filename);

            try
            {
                FileStream fs = null;

                try
                {
                    fs = File.Create(file);
                }
                finally
                {
                    fs.Dispose();
                }

                Copy t = new Copy();
                t.RetryDelayMilliseconds = 1; // speed up tests!
                // Allow the task's default (false) to have a chance
                if (useHardLinks)
                {
                    t.UseHardlinksIfPossible = useHardLinks;
                }
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;
                t.SourceFiles = new ITaskItem[] { new TaskItem(file) };
                t.DestinationFiles = new ITaskItem[] { new TaskItem(filename.ToLowerInvariant()) };
                t.SkipUnchangedFiles = false;
                bool success = t.Execute();

                Assert.True(success);
                Assert.Equal(1, t.DestinationFiles.Length);
                Assert.Equal(filename.ToLowerInvariant(), t.DestinationFiles[0].ItemSpec);

                ((MockEngine)t.BuildEngine).AssertLogDoesntContain("MSB3026"); // Didn't do retries, nothing to do
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Copying a file on top of itself should be a success (no-op) whether
        /// or not skipUnchangedFiles is true or false. Variation with a second copy failure.
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void CopyFileOnItselfAndFailACopy()
        {
            string temp = Path.GetTempPath();
            string file = Path.Combine(temp, "2A333ED756AF4dc392E728D0F864A395");
            string invalidFile = NativeMethodsShared.IsUnixLike ? Path.Combine(temp, "!@#$%^&*()|") : "!@#$%^&*()|";
            string dest2 = "whatever";

            try
            {
                FileStream fs = null;

                try
                {
                    fs = File.Create(file);
                }
                finally
                {
                    fs.Dispose();
                }

                Copy t = new Copy();
                t.RetryDelayMilliseconds = 1; // speed up tests!
                // Allow the task's default (false) to have a chance
                if (useHardLinks)
                {
                    t.UseHardlinksIfPossible = useHardLinks;
                }
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;
                t.SourceFiles = new ITaskItem[] { new TaskItem(file), new TaskItem(invalidFile) };
                t.DestinationFiles = new ITaskItem[] { new TaskItem(file), new TaskItem(dest2) };
                t.SkipUnchangedFiles = false;
                bool success = t.Execute();

                // Since on Unix there are no invalid file names, the copy will succeed
                Assert.False(NativeMethodsShared.IsUnixLike ? !success : success);
                Assert.Equal(2, t.DestinationFiles.Length);
                Assert.Equal(file, t.DestinationFiles[0].ItemSpec);
                Assert.Equal(dest2, t.DestinationFiles[1].ItemSpec);
                Assert.Equal(file, t.CopiedFiles[0].ItemSpec);

                if (NativeMethodsShared.IsUnixLike)
                {
                    Assert.Equal(2, t.CopiedFiles.Length);
                }
                else
                {
                    Assert.Equal(1, t.CopiedFiles.Length);

                    ((MockEngine)t.BuildEngine).AssertLogDoesntContain("MSB3026");
                    // Didn't do retries, no op then invalid
                }
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// DestinationFolder should work.
        /// </summary>
        [Fact]
        public void CopyToDestinationFolder()
        {
            string sourceFile = FileUtilities.GetTemporaryFile();
            string temp = Path.GetTempPath();
            string destFolder = Path.Combine(temp, "2A333ED756AF4dc392E728D0F864A398");
            string destFile = Path.Combine(destFolder, Path.GetFileName(sourceFile));
            try
            {
                using (StreamWriter sw = FileUtilities.OpenWrite(sourceFile, true))    // HIGHCHAR: Test writes in UTF8 without preamble.
                    sw.Write("This is a source temp file.");

                // Don't create the dest folder, let task do that

                ITaskItem[] sourceFiles = new ITaskItem[] { new TaskItem(sourceFile) };

                Copy t = new Copy();
                t.RetryDelayMilliseconds = 1; // speed up tests!
                // Allow the task's default (false) to have a chance
                if (useHardLinks)
                {
                    t.UseHardlinksIfPossible = useHardLinks;
                }
                MockEngine me = new MockEngine();

                t.BuildEngine = me;
                t.SourceFiles = sourceFiles;
                t.DestinationFolder = new TaskItem(destFolder);
                t.SkipUnchangedFiles = true;

                bool success = t.Execute();

                Assert.True(success); // "success"
                Assert.True(File.Exists(destFile)); // "destination exists"

                string destinationFileContents;
                using (StreamReader sr = FileUtilities.OpenRead(destFile))
                    destinationFileContents = sr.ReadToEnd();

                if (!useHardLinks)
                {
                    Microsoft.Build.UnitTests.MockEngine.GetStringDelegate resourceDelegate = new Microsoft.Build.UnitTests.MockEngine.GetStringDelegate(AssemblyResources.GetString);
                    me.AssertLogDoesntContainMessageFromResource(resourceDelegate, "Copy.HardLinkComment", sourceFile, destFile);
                }
                else
                {
                    Microsoft.Build.UnitTests.MockEngine.GetStringDelegate resourceDelegate = new Microsoft.Build.UnitTests.MockEngine.GetStringDelegate(AssemblyResources.GetString);
                    me.AssertLogContainsMessageFromResource(resourceDelegate, "Copy.HardLinkComment", sourceFile, destFile);
                }

                Assert.Equal(destinationFileContents, "This is a source temp file."); //                     "Expected the destination file to contain the contents of source file."

                Assert.Equal(1, t.DestinationFiles.Length);
                Assert.Equal(1, t.CopiedFiles.Length);
                Assert.Equal(destFile, t.DestinationFiles[0].ItemSpec);
                Assert.Equal(destFile, t.CopiedFiles[0].ItemSpec);

                ((MockEngine)t.BuildEngine).AssertLogDoesntContain("MSB3026"); // Didn't do retries
            }
            finally
            {
                Helpers.DeleteFiles(sourceFile, destFile);
            }
        }

        /// <summary>
        /// DestinationFolder should work.
        /// </summary>
        [Fact]
        public void CopyDoubleEscapableFileToDestinationFolder()
        {
            string sourceFileEscaped = Path.GetTempPath() + "a%253A_" + Guid.NewGuid().ToString("N") + ".txt";
            string sourceFile = EscapingUtilities.UnescapeAll(sourceFileEscaped);
            string temp = Path.GetTempPath();
            string destFolder = Path.Combine(temp, "2A333ED756AF4dc392E728D0F864A398");
            string destFile = Path.Combine(destFolder, Path.GetFileName(sourceFile));

            try
            {
                using (StreamWriter sw = FileUtilities.OpenWrite(sourceFile, true))    // HIGHCHAR: Test writes in UTF8 without preamble.
                {
                    sw.Write("This is a source temp file.");
                }

                // Don't create the dest folder, let task do that

                ITaskItem[] sourceFiles = new ITaskItem[] { new TaskItem(sourceFileEscaped) };

                Copy t = new Copy();
                t.RetryDelayMilliseconds = 1; // speed up tests!
                // Allow the task's default (false) to have a chance
                if (useHardLinks)
                {
                    t.UseHardlinksIfPossible = useHardLinks;
                }
                t.BuildEngine = new MockEngine();
                t.SourceFiles = sourceFiles;
                t.DestinationFolder = new TaskItem(destFolder);
                t.SkipUnchangedFiles = true;

                bool success = t.Execute();

                Assert.True(success); // "success"
                Assert.True(File.Exists(destFile)); // "destination exists"

                string destinationFileContents;
                using (StreamReader sr = FileUtilities.OpenRead(destFile))
                {
                    destinationFileContents = sr.ReadToEnd();
                }

                Assert.Equal(destinationFileContents, "This is a source temp file."); //                     "Expected the destination file to contain the contents of source file."

                Assert.Equal(1, t.DestinationFiles.Length);
                Assert.Equal(1, t.CopiedFiles.Length);
                Assert.Equal(destFile, t.DestinationFiles[0].ItemSpec);
                Assert.Equal(destFile, t.CopiedFiles[0].ItemSpec);

                ((MockEngine)t.BuildEngine).AssertLogDoesntContain("MSB3026"); // Didn't do retries
            }
            finally
            {
                Helpers.DeleteFiles(sourceFile, destFile);
            }
        }

        /// <summary>
        /// Copying duplicates should only perform the actual copy once for each unique source/destination pair
        /// but should still produce outputs for all specified source/destination pairs.
        /// </summary>
        [Fact]
        public void CopyWithDuplicatesUsingFolder()
        {
            string tempPath = Path.GetTempPath();

            ITaskItem[] sourceFiles = new ITaskItem[]
            {
                new TaskItem(Path.Combine(tempPath, "a.cs")),
                new TaskItem(Path.Combine(tempPath, "b.cs")),
                new TaskItem(Path.Combine(tempPath, "a.cs")),
                new TaskItem(Path.Combine(tempPath, "a.cs")),
            };

            foreach (ITaskItem item in sourceFiles)
            {
                using (StreamWriter sw = FileUtilities.OpenWrite(item.ItemSpec, false))    // HIGHCHAR: Test writes in UTF8 without preamble.
                {
                    sw.Write("This is a source temp file.");
                }
            }

            var filesActuallyCopied = new List<KeyValuePair<FileState, FileState>>();

            Copy t = new Copy();
            t.RetryDelayMilliseconds = 1; // speed up tests!
            // Allow the task's default (false) to have a chance
            if (useHardLinks)
            {
                t.UseHardlinksIfPossible = useHardLinks;
            }
            t.BuildEngine = new MockEngine();
            t.SourceFiles = sourceFiles;
            t.DestinationFolder = new TaskItem(Path.Combine(tempPath, "foo"));

            bool success = t.Execute(delegate (FileState source, FileState dest)
            {
                filesActuallyCopied.Add(new KeyValuePair<FileState, FileState>(source, dest));
                return true;
            });

            Assert.True(success);
            Assert.Equal(2, filesActuallyCopied.Count);
            Assert.Equal(4, t.CopiedFiles.Length);
            Assert.Equal(Path.Combine(tempPath, "a.cs"), filesActuallyCopied[0].Key.Name);
            Assert.Equal(Path.Combine(tempPath, "b.cs"), filesActuallyCopied[1].Key.Name);

            ((MockEngine)t.BuildEngine).AssertLogDoesntContain("MSB3026"); // Didn't do retries
        }

        /// <summary>
        /// Copying duplicates should only perform the actual copy once for each unique source/destination pair
        /// but should still produce outputs for all specified source/destination pairs.
        /// </summary>
        [Fact]
        public void CopyWithDuplicatesUsingFiles()
        {
            string tempPath = Path.GetTempPath();

            ITaskItem[] sourceFiles = new ITaskItem[]
            {
                new TaskItem(Path.Combine(tempPath, "a.cs")),
                new TaskItem(Path.Combine(tempPath, "b.cs")),
                new TaskItem(Path.Combine(tempPath, "a.cs")),
                new TaskItem(Path.Combine(tempPath, "a.cs")),
                new TaskItem(Path.Combine(tempPath, "a.cs")),
            };

            foreach (ITaskItem item in sourceFiles)
            {
                using (StreamWriter sw = FileUtilities.OpenWrite(item.ItemSpec, false))    // HIGHCHAR: Test writes in UTF8 without preamble.
                {
                    sw.Write("This is a source temp file.");
                }
            }

            ITaskItem[] destFiles = new ITaskItem[]
            {
                new TaskItem(Path.Combine(tempPath, @"xa.cs")), // a.cs -> xa.cs
                new TaskItem(Path.Combine(tempPath, @"xa.cs")), // b.cs -> xa.cs should copy because it's a different source
                new TaskItem(Path.Combine(tempPath, @"xb.cs")), // a.cs -> xb.cs should copy because it's a different destination
                new TaskItem(Path.Combine(tempPath, @"xa.cs")), // a.cs -> xa.cs should copy because it's a different source
                new TaskItem(Path.Combine(tempPath, @"xa.cs")), // a.cs -> xa.cs should not copy because it's the same source
            };

            var filesActuallyCopied = new List<KeyValuePair<FileState, FileState>>();

            Copy t = new Copy();
            t.RetryDelayMilliseconds = 1; // speed up tests!
            // Allow the task's default (false) to have a chance
            if (useHardLinks)
            {
                t.UseHardlinksIfPossible = useHardLinks;
            }
            t.BuildEngine = new MockEngine();
            t.SourceFiles = sourceFiles;
            t.DestinationFiles = destFiles;

            bool success = t.Execute(delegate (FileState source, FileState dest)
            {
                filesActuallyCopied.Add(new KeyValuePair<FileState, FileState>(source, dest));
                return true;
            });

            Assert.True(success);
            Assert.Equal(4, filesActuallyCopied.Count);
            Assert.Equal(5, t.CopiedFiles.Length);
            Assert.Equal(Path.Combine(tempPath, "a.cs"), filesActuallyCopied[0].Key.Name);
            Assert.Equal(Path.Combine(tempPath, "b.cs"), filesActuallyCopied[1].Key.Name);
            Assert.Equal(Path.Combine(tempPath, "a.cs"), filesActuallyCopied[2].Key.Name);
            Assert.Equal(Path.Combine(tempPath, "a.cs"), filesActuallyCopied[3].Key.Name);
            Assert.Equal(Path.Combine(tempPath, "xa.cs"), filesActuallyCopied[0].Value.Name);
            Assert.Equal(Path.Combine(tempPath, "xa.cs"), filesActuallyCopied[1].Value.Name);
            Assert.Equal(Path.Combine(tempPath, "xb.cs"), filesActuallyCopied[2].Value.Name);
            Assert.Equal(Path.Combine(tempPath, "xa.cs"), filesActuallyCopied[3].Value.Name);

            ((MockEngine)t.BuildEngine).AssertLogDoesntContain("MSB3026"); // Didn't do retries
        }

        /// <summary>
        /// DestinationFiles should only include files that were successfully copied 
        /// (or skipped), not files for which there was an error.
        /// </summary>
        [Fact]
        public void DestinationFilesLengthNotEqualSourceFilesLength()
        {
            string temp = Path.GetTempPath();
            string inFile1 = Path.Combine(temp, "2A333ED756AF4dc392E728D0F864A398");
            string inFile2 = Path.Combine(temp, "2A333ED756AF4dc392E728D0F864A399");
            string outFile1 = Path.Combine(temp, "2A333ED756AF4dc392E728D0F864A400");

            try
            {
                FileStream fs = null;
                FileStream fs2 = null;

                try
                {
                    fs = File.Create(inFile1);
                    fs2 = File.Create(inFile2);
                }
                finally
                {
                    fs.Dispose();
                    fs2.Dispose();
                }

                Copy t = new Copy();
                t.RetryDelayMilliseconds = 1; // speed up tests!
                // Allow the task's default (false) to have a chance
                if (useHardLinks)
                {
                    t.UseHardlinksIfPossible = useHardLinks;
                }
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;

                t.SourceFiles = new ITaskItem[] { new TaskItem(inFile1), new TaskItem(inFile2) };
                t.DestinationFiles = new ITaskItem[] { new TaskItem(outFile1) };

                bool success = t.Execute();

                Assert.False(success);
                Assert.Equal(1, t.DestinationFiles.Length);
                Assert.Null(t.CopiedFiles);
                Assert.False(File.Exists(outFile1));

                ((MockEngine)t.BuildEngine).AssertLogDoesntContain("MSB3026"); // Didn't do retries
            }
            finally
            {
                File.Delete(inFile1);
                File.Delete(inFile2);
                File.Delete(outFile1);
            }
        }

        /// <summary>
        /// If the destination path is too long, the task should not bubble up
        /// the System.IO.PathTooLongException 
        /// </summary>
        [Fact]
        public void Regress451057_ExitGracefullyIfPathNameIsTooLong()
        {
            string sourceFile = FileUtilities.GetTemporaryFile();
            string destinationFile = "ABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZ";

            try
            {
                using (StreamWriter sw = FileUtilities.OpenWrite(sourceFile, true))    // HIGHCHAR: Test writes in UTF8 without preamble.
                    sw.Write("This is a source temp file.");

                ITaskItem[] sourceFiles = new ITaskItem[] { new TaskItem(sourceFile) };
                ITaskItem[] destinationFiles = new ITaskItem[] { new TaskItem(destinationFile) };

                Copy t = new Copy();
                t.RetryDelayMilliseconds = 1; // speed up tests!
                // Allow the task's default (false) to have a chance
                if (useHardLinks)
                {
                    t.UseHardlinksIfPossible = useHardLinks;
                }
                t.BuildEngine = new MockEngine();
                t.SourceFiles = sourceFiles;
                t.DestinationFiles = destinationFiles;

                bool result = t.Execute();

                // Expect for there to have been no copies.
                Assert.Equal(false, result);

                ((MockEngine)t.BuildEngine).AssertLogDoesntContain("MSB3026"); // Didn't do retries
            }
            finally
            {
                File.Delete(sourceFile);
            }
        }

        /// <summary>
        /// If the source path is too long, the task should not bubble up
        /// the System.IO.PathTooLongException 
        /// </summary>
        [Fact]
        public void Regress451057_ExitGracefullyIfPathNameIsTooLong2()
        {
            string sourceFile = "ABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZ";
            string destinationFile = FileUtilities.GetTemporaryFile();
            File.Delete(destinationFile);

            ITaskItem[] sourceFiles = new ITaskItem[] { new TaskItem(sourceFile) };
            ITaskItem[] destinationFiles = new ITaskItem[] { new TaskItem(destinationFile) };

            Copy t = new Copy();
            t.RetryDelayMilliseconds = 1; // speed up tests!
            // Allow the task's default (false) to have a chance
            if (useHardLinks)
            {
                t.UseHardlinksIfPossible = useHardLinks;
            }
            t.BuildEngine = new MockEngine();
            t.SourceFiles = sourceFiles;
            t.DestinationFiles = destinationFiles;

            bool result = t.Execute();

            // Expect for there to have been no copies.
            Assert.Equal(false, result);
            Assert.False(File.Exists(destinationFile));

            ((MockEngine)t.BuildEngine).AssertLogDoesntContain("MSB3026"); // Didn't do retries
        }

        /// <summary>
        /// If the SourceFiles parameter is given invalid path characters, make sure the task exits gracefully.
        /// </summary>
        [Fact]
        public void ExitGracefullyOnInvalidPathCharacters()
        {
            Copy t = new Copy();
            t.RetryDelayMilliseconds = 1; // speed up tests!
            // Allow the task's default (false) to have a chance
            if (useHardLinks)
            {
                t.UseHardlinksIfPossible = useHardLinks;
            }
            t.BuildEngine = new MockEngine();
            t.SourceFiles = new ITaskItem[] { new TaskItem("foo | bar") }; ;
            t.DestinationFolder = new TaskItem("dest");

            bool result = t.Execute();

            // Expect for there to have been no copies.
            Assert.Equal(false, result);
            ((MockEngine)t.BuildEngine).AssertLogDoesntContain("MSB3026"); // Didn't do retries
        }

        /// <summary>
        /// Verifies that we error for retries less than 0
        /// </summary>
        [Fact]
        public void InvalidRetryCount()
        {
            Copy t = new Copy();
            t.RetryDelayMilliseconds = 1; // speed up tests!
            // Allow the task's default (false) to have a chance
            if (useHardLinks)
            {
                t.UseHardlinksIfPossible = useHardLinks;
            }
            MockEngine engine = new MockEngine(true /* log to console */);
            t.BuildEngine = engine;
            t.SourceFiles = new ITaskItem[] { new TaskItem("c:\\source") };
            t.DestinationFiles = new ITaskItem[] { new TaskItem("c:\\destination") };
            t.Retries = -1;

            bool result = t.Execute();

            Assert.Equal(false, result);
            engine.AssertLogContains("MSB3028");
        }

        /// <summary>
        /// Verifies that we error for retry delay less than 0
        /// </summary>
        [Fact]
        public void InvalidRetryDelayCount()
        {
            Copy t = new Copy();
            t.RetryDelayMilliseconds = 1; // speed up tests!
            // Allow the task's default (false) to have a chance
            if (useHardLinks)
            {
                t.UseHardlinksIfPossible = useHardLinks;
            }
            MockEngine engine = new MockEngine(true /* log to console */);
            t.BuildEngine = engine;
            t.SourceFiles = new ITaskItem[] { new TaskItem("c:\\source") };
            t.DestinationFiles = new ITaskItem[] { new TaskItem("c:\\destination") };
            t.Retries = 1;
            t.RetryDelayMilliseconds = -1;

            bool result = t.Execute();

            Assert.Equal(false, result);
            engine.AssertLogContains("MSB3029");
        }

        /// <summary>
        /// Verifies that we do not log the retrying warning if we didn't request
        /// retries.
        /// </summary>
        [Fact]
        public void FailureWithNoRetries()
        {
            Copy t = new Copy();
            t.RetryDelayMilliseconds = 1; // speed up tests!
            // Allow the task's default (false) to have a chance
            if (useHardLinks)
            {
                t.UseHardlinksIfPossible = useHardLinks;
            }
            MockEngine engine = new MockEngine(true /* log to console */);
            t.BuildEngine = engine;
            t.SourceFiles = new ITaskItem[] { new TaskItem("c:\\source") };
            t.DestinationFiles = new ITaskItem[] { new TaskItem("c:\\destination") };
            t.Retries = 0;

            CopyFunctor copyFunctor = new CopyFunctor(2, false /* do not throw on failure */);
            bool result = t.Execute(copyFunctor.Copy);

            Assert.Equal(false, result);
            engine.AssertLogDoesntContain("MSB3026");
            engine.AssertLogDoesntContain("MSB3027");
        }

        /// <summary>
        /// Retrying default
        /// </summary>
        [Fact]
        public void DefaultRetriesIs10()
        {
            Copy t = new Copy();
            t.RetryDelayMilliseconds = 1; // speed up tests!

            Assert.Equal(10, t.Retries);
        }

        /// <summary>
        /// Delay default
        /// </summary>
        [Fact]
        public void DefaultRetryDelayIs1000()
        {
            Copy t = new Copy();

            Assert.Equal(1000, t.RetryDelayMilliseconds);
        }

        /// <summary>
        /// Hardlink default
        /// </summary>
        [Fact]
        public void DefaultNoHardlink()
        {
            Copy t = new Copy();
            t.RetryDelayMilliseconds = 1; // speed up tests!

            Assert.Equal(false, t.UseHardlinksIfPossible);
        }

        /// <summary>
        /// Verifies that we get the one retry we ask for after the first attempt fails,
        /// and we get appropriate messages.
        /// </summary>
        [Fact]
        public void SuccessAfterOneRetry()
        {
            Copy t = new Copy();
            t.RetryDelayMilliseconds = 1; // speed up tests!
            // Allow the task's default (false) to have a chance
            if (useHardLinks)
            {
                t.UseHardlinksIfPossible = useHardLinks;
            }
            MockEngine engine = new MockEngine(true /* log to console */);
            t.BuildEngine = engine;
            t.SourceFiles = new ITaskItem[] { new TaskItem("c:\\source") };
            t.DestinationFiles = new ITaskItem[] { new TaskItem("c:\\destination") };
            t.Retries = 1;
            t.RetryDelayMilliseconds = 0; // Can't really test the delay, but at least try passing in a value

            CopyFunctor copyFunctor = new CopyFunctor(2, false /* do not throw on failure */);
            bool result = t.Execute(copyFunctor.Copy);

            Assert.Equal(true, result);
            engine.AssertLogContains("MSB3026");
            engine.AssertLogDoesntContain("MSB3027");
        }

        /// <summary>
        /// Verifies that after a successful retry we continue to the next file
        /// </summary>
        [Fact]
        public void SuccessAfterOneRetryContinueToNextFile()
        {
            Copy t = new Copy();
            t.RetryDelayMilliseconds = 1; // speed up tests!
            // Allow the task's default (false) to have a chance
            if (useHardLinks)
            {
                t.UseHardlinksIfPossible = useHardLinks;
            }
            MockEngine engine = new MockEngine(true /* log to console */);
            t.BuildEngine = engine;
            t.SourceFiles = new ITaskItem[] { new TaskItem("c:\\source"), new TaskItem("c:\\source2") };
            t.DestinationFiles = new ITaskItem[] { new TaskItem("c:\\destination"), new TaskItem("c:\\destination2") };
            t.Retries = 1;
            t.RetryDelayMilliseconds = 1; // Can't really test the delay, but at least try passing in a value

            CopyFunctor copyFunctor = new CopyFunctor(2, false /* do not throw on failure */);
            bool result = t.Execute(copyFunctor.Copy);

            Assert.Equal(true, result);
            engine.AssertLogContains("MSB3026");
            engine.AssertLogDoesntContain("MSB3027");
            Assert.Equal(copyFunctor.FilesCopiedSuccessfully[0].Name, FileUtilities.FixFilePath("c:\\source"));
            Assert.Equal(copyFunctor.FilesCopiedSuccessfully[1].Name, FileUtilities.FixFilePath("c:\\source2"));
        }

        /// <summary>
        /// The copy delegate can return false, or throw on failure.
        /// This test tests returning false.
        /// </summary>
        [Fact]
        public void TooFewRetriesReturnsFalse()
        {
            Copy t = new Copy();
            t.RetryDelayMilliseconds = 1; // speed up tests!
            // Allow the task's default (false) to have a chance
            if (useHardLinks)
            {
                t.UseHardlinksIfPossible = useHardLinks;
            }
            MockEngine engine = new MockEngine(true /* log to console */);
            t.BuildEngine = engine;
            t.SourceFiles = new ITaskItem[] { new TaskItem("c:\\source") };
            t.DestinationFiles = new ITaskItem[] { new TaskItem("c:\\destination") };
            t.Retries = 2;

            CopyFunctor copyFunctor = new CopyFunctor(4, false /* do not throw */);
            bool result = t.Execute(copyFunctor.Copy);

            Assert.Equal(false, result);
            engine.AssertLogContains("MSB3026");
            engine.AssertLogContains("MSB3027");
        }

        /// <summary>
        /// The copy delegate can return false, or throw on failure.
        /// This test tests the throw case.
        /// </summary>
        [Fact]
        public void TooFewRetriesThrows()
        {
            Copy t = new Copy();
            t.RetryDelayMilliseconds = 1; // speed up tests!
            // Allow the task's default (false) to have a chance
            if (useHardLinks)
            {
                t.UseHardlinksIfPossible = useHardLinks;
            }
            MockEngine engine = new MockEngine(true /* log to console */);
            t.BuildEngine = engine;
            t.SourceFiles = new ITaskItem[] { new TaskItem("c:\\source") };
            t.DestinationFiles = new ITaskItem[] { new TaskItem("c:\\destination") };
            t.Retries = 1;

            CopyFunctor copyFunctor = new CopyFunctor(3, true /* throw */);
            bool result = t.Execute(copyFunctor.Copy);

            Assert.Equal(false, result);
            engine.AssertLogContains("MSB3026");
            engine.AssertLogContains("MSB3027");
        }

        /// <summary>
        /// Helper functor for retry tests.
        /// Simulates the File.Copy method without touching the disk.
        /// First copy fails as requested, subsequent copies succeed.
        /// </summary>
        private class CopyFunctor
        {
            /// <summary>
            /// On what attempt count should we stop failing?
            /// </summary>
            private int _countOfSuccess;

            /// <summary>
            /// Should we throw when we fail, instead of just returning false?
            /// </summary>
            private bool _throwOnFailure;

            /// <summary>
            /// How many tries have we done so far
            /// </summary>
            private int _tries;

            /// <summary>
            /// Which files we actually copied.
            /// </summary>
            private List<FileState> _filesCopiedSuccessfully;

            /// <summary>
            /// Which files we actually copied
            /// </summary>
            internal List<FileState> FilesCopiedSuccessfully
            {
                get { return _filesCopiedSuccessfully; }
            }

            /// <summary>
            /// Constructor
            /// </summary>
            internal CopyFunctor(int countOfSuccess, bool throwOnFailure)
            {
                _countOfSuccess = countOfSuccess;
                _throwOnFailure = throwOnFailure;
                _tries = 0;
                _filesCopiedSuccessfully = new List<FileState>();
            }

            /// <summary>
            /// Pretend to be File.Copy.
            /// </summary>
            internal bool? Copy(FileState source, FileState destination)
            {
                _tries++;

                // 2nd and subsequent copies always succeed
                if (_filesCopiedSuccessfully.Count > 0 || _countOfSuccess == _tries)
                {
                    Console.WriteLine("Copied {0} to {1} OK", source, destination);
                    _filesCopiedSuccessfully.Add(source);
                    return true;
                }

                if (_throwOnFailure)
                {
                    throw new IOException("oops");
                }
                else
                {
                    return null;
                }
            }
        }
    }

    public class CopyNotHardLink_Tests : Copy_Tests
    {
        public CopyNotHardLink_Tests()
        {
            this.useHardLinks = false;
        }
    }

    public class CopyHardAndSymbolicLink_Tests
    {
        [Fact]
        public void CopyWithHardAndSymbolicLinks()
        {
            string sourceFile = FileUtilities.GetTemporaryFile();
            string temp = Path.GetTempPath();
            string destFolder = Path.Combine(temp, "2A333ED756AF4dc392E728D0F864A398");
            string destFile = Path.Combine(destFolder, Path.GetFileName(sourceFile));

            try
            {
                ITaskItem[] sourceFiles = { new TaskItem(sourceFile) };

                MockEngine me = new MockEngine(true);
                Copy t = new Copy
                {
                    RetryDelayMilliseconds = 1, // speed up tests!
                    UseHardlinksIfPossible = true,
                    UseSymboliclinksIfPossible = true,
                    BuildEngine = me,
                    SourceFiles = sourceFiles,
                    DestinationFolder = new TaskItem(destFolder),
                    SkipUnchangedFiles = true
                };

                bool success = t.Execute();

                Assert.False(success);

                MockEngine.GetStringDelegate resourceDelegate = AssemblyResources.GetString;
                me.AssertLogContainsMessageFromResource(resourceDelegate, "Copy.ExactlyOneTypeOfLink", "UseHardlinksIfPossible", "UseSymboliclinksIfPossible");
            }
            finally
            {
                Helpers.DeleteFiles(sourceFile, destFile);
            }
        }
    }

    public class CopyHardLink_Tests : Copy_Tests
    {
        public CopyHardLink_Tests()
        {
            this.useHardLinks = true;
        }

        /// <summary>
        /// DestinationFolder should work.
        /// </summary>
        [Fact]
        public void CopyToDestinationFolderWithHardLinkCheck()
        {
            string sourceFile = FileUtilities.GetTemporaryFile();
            string temp = Path.GetTempPath();
            string destFolder = Path.Combine(temp, "2A333ED756AF4dc392E728D0F864A398");
            string destFile = Path.Combine(destFolder, Path.GetFileName(sourceFile));
            try
            {
                using (StreamWriter sw = FileUtilities.OpenWrite(sourceFile, true))    // HIGHCHAR: Test writes in UTF8 without preamble.
                    sw.Write("This is a source temp file.");

                // Don't create the dest folder, let task do that

                ITaskItem[] sourceFiles = new ITaskItem[] { new TaskItem(sourceFile) };

                Copy t = new Copy();
                t.RetryDelayMilliseconds = 1; // speed up tests!

                // Allow the task's default (false) to have a chance
                t.UseHardlinksIfPossible = true;

                MockEngine me = new MockEngine(true);
                t.BuildEngine = me;
                t.SourceFiles = sourceFiles;
                t.DestinationFolder = new TaskItem(destFolder);
                t.SkipUnchangedFiles = true;

                bool success = t.Execute();

                Assert.True(success); // "success"
                Assert.True(File.Exists(destFile)); // "destination exists"
                Microsoft.Build.UnitTests.MockEngine.GetStringDelegate resourceDelegate = new Microsoft.Build.UnitTests.MockEngine.GetStringDelegate(AssemblyResources.GetString);

                me.AssertLogContainsMessageFromResource(resourceDelegate, "Copy.HardLinkComment", sourceFile, destFile);

                string destinationFileContents;
                using (StreamReader sr = FileUtilities.OpenRead(destFile))
                    destinationFileContents = sr.ReadToEnd();

                Assert.Equal("This is a source temp file.", destinationFileContents); //"Expected the destination hard linked file to contain the contents of source file."

                Assert.Equal(1, t.DestinationFiles.Length);
                Assert.Equal(1, t.CopiedFiles.Length);
                Assert.Equal(destFile, t.DestinationFiles[0].ItemSpec);
                Assert.Equal(destFile, t.CopiedFiles[0].ItemSpec);

                // Now we will write new content to the source file
                // we'll then check that the destination file automatically
                // has the same content (i.e. it's been hard linked)
                using (StreamWriter sw = FileUtilities.OpenWrite(sourceFile, false))    // HIGHCHAR: Test writes in UTF8 without preamble.
                    sw.Write("This is another source temp file.");

                // Read the destination file (it should have the same modified content as the source)
                using (StreamReader sr = FileUtilities.OpenRead(destFile))
                    destinationFileContents = sr.ReadToEnd();

                Assert.Equal("This is another source temp file.", destinationFileContents); //"Expected the destination hard linked file to contain the contents of source file. Even after modification of the source"

                ((MockEngine)t.BuildEngine).AssertLogDoesntContain("MSB3026"); // Didn't do retries
            }
            finally
            {
                Helpers.DeleteFiles(sourceFile, destFile);
            }
        }

        /// <summary>
        /// DestinationFolder should work.
        /// </summary>
        [Fact]
        [PlatformSpecific(Xunit.PlatformID.Windows)] // SMB share paths only work on Windows
        public void CopyToDestinationFolderWithHardLinkFallbackNetwork()
        {
            // Workaround: For some reason when this test runs with all other tests we are getting
            // the incorrect result from CreateHardLink error message (a message associated with
            // another test). Calling GetHRForLastWin32Error / GetExceptionForHR seems to clear
            // out the previous message and allow us to get the right message in the Copy task.
            int errorCode = Marshal.GetHRForLastWin32Error();
            Marshal.GetExceptionForHR(errorCode);

            string sourceFile = FileUtilities.GetTemporaryFile();
            string temp = @"\\localhost\c$\temp";
            string destFolder = Path.Combine(temp, "2A333ED756AF4dc392E728D0F864A398");
            string destFile = Path.Combine(destFolder, Path.GetFileName(sourceFile));

            try
            {
                if (!Directory.Exists(destFolder))
                {
                    Directory.CreateDirectory(destFolder);
                }

                string nothingFile = Path.Combine(destFolder, "nothing.txt");
                File.WriteAllText(nothingFile, "nothing");
                File.Delete(nothingFile);
            }
            catch (Exception)
            {
                Console.WriteLine("CopyToDestinationFolderWithHardLinkFallbackNetwork test could not access the network.");
                // Something caused us to not be able to access our "network" share, don't fail.
                return;
            }

            try
            {
                using (StreamWriter sw = FileUtilities.OpenWrite(sourceFile, true))    // HIGHCHAR: Test writes in UTF8 without preamble.
                    sw.Write("This is a source temp file.");

                ITaskItem[] sourceFiles = { new TaskItem(sourceFile) };

                MockEngine me = new MockEngine(true);
                Copy t = new Copy
                {
                    RetryDelayMilliseconds = 1, // speed up tests!
                    UseHardlinksIfPossible = true,
                    BuildEngine = me,
                    SourceFiles = sourceFiles,
                    DestinationFolder = new TaskItem(destFolder),
                    SkipUnchangedFiles = true
                };

                bool success = t.Execute();

                Assert.True(success); // "success"
                Assert.True(File.Exists(destFile)); // "destination exists"
                MockEngine.GetStringDelegate resourceDelegate = AssemblyResources.GetString;

                me.AssertLogContainsMessageFromResource(resourceDelegate, "Copy.HardLinkComment", sourceFile, destFile);

                // Can't do this below, because the real message doesn't end with String.Empty, it ends with a CLR exception string, and so matching breaks in PLOC.
                // Instead look for the HRESULT that CLR unfortunately puts inside its exception string. Something like this
                // The system cannot move the file to a different disk drive. (Exception from HRESULT: 0x80070011)
                // me.AssertLogContainsMessageFromResource(resourceDelegate, "Copy.RetryingAsFileCopy", sourceFile, destFile, String.Empty);
                me.AssertLogContains("0x80070011");

                string destinationFileContents;
                using (StreamReader sr = FileUtilities.OpenRead(destFile))
                    destinationFileContents = sr.ReadToEnd();

                Assert.Equal("This is a source temp file.", destinationFileContents); //"Expected the destination file to contain the contents of source file."

                Assert.Equal(1, t.DestinationFiles.Length);
                Assert.Equal(1, t.CopiedFiles.Length);
                Assert.Equal(destFile, t.DestinationFiles[0].ItemSpec);
                Assert.Equal(destFile, t.CopiedFiles[0].ItemSpec);

                // Now we will write new content to the source file
                // we'll then check that the destination file automatically
                // has the same content (i.e. it's been hard linked)
                using (StreamWriter sw = FileUtilities.OpenWrite(sourceFile, false))    // HIGHCHAR: Test writes in UTF8 without preamble.
                    sw.Write("This is another source temp file.");

                // Read the destination file (it should have the same modified content as the source)
                using (StreamReader sr = FileUtilities.OpenRead(destFile))
                    destinationFileContents = sr.ReadToEnd();

                Assert.Equal("This is a source temp file.", destinationFileContents); //"Expected the destination copied file to contain the contents of original source file only."

                ((MockEngine)t.BuildEngine).AssertLogDoesntContain("MSB3026"); // Didn't do retries
            }
            finally
            {
                File.Delete(sourceFile);
                File.Delete(destFile);
                FileUtilities.DeleteWithoutTrailingBackslash(destFolder, true);
            }
        }

        /// <summary>
        /// DestinationFolder should work.
        /// </summary>
        [Fact]
        [PlatformSpecific(PlatformID.Windows)] // Only Windows has a (small) link limit, and this tests for an HRESULT
        public void CopyToDestinationFolderWithHardLinkFallbackTooManyLinks()
        {
            // Workaround: For some reason when this test runs with all other tests we are getting
            // the incorrect result from CreateHardLink error message (a message associated with
            // another test). Calling GetHRForLastWin32Error / GetExceptionForHR seems to clear
            // out the previous message and allow us to get the right message in the Copy task.
            int errorCode = Marshal.GetHRForLastWin32Error();
            Marshal.GetExceptionForHR(errorCode);

            string sourceFile = FileUtilities.GetTemporaryFile();
            string temp = Path.GetTempPath();
            string destFolder = Path.Combine(temp, "2A333ED756AF4dc392E728D0F864A398");
            string destFile = Path.Combine(destFolder, Path.GetFileName(sourceFile));

            try
            {
                using (StreamWriter sw = FileUtilities.OpenWrite(sourceFile, true))    // HIGHCHAR: Test writes in UTF8 without preamble.
                    sw.Write("This is a source temp file.");

                if (!Directory.Exists(destFolder))
                {
                    Directory.CreateDirectory(destFolder);
                }

                // Exhaust the number (1024) of directory entries that can be created for a file
                // This is 1 + (1 x hard links)
                // We need to test the fallback code path when we're out of directory entries for a file..
                for (int n = 0; n < 1025 /* make sure */; n++)
                {
                    string destLink = Path.Combine(destFolder, Path.GetFileNameWithoutExtension(sourceFile) + "." + n);
                    string linkError = String.Empty;
                    NativeMethods.MakeHardLink(destLink, sourceFile, ref linkError);
                }

                ITaskItem[] sourceFiles = { new TaskItem(sourceFile) };

                MockEngine me = new MockEngine(true);
                Copy t = new Copy
                {
                    RetryDelayMilliseconds = 1, // speed up tests!
                    UseHardlinksIfPossible = true,
                    BuildEngine = me,
                    SourceFiles = sourceFiles,
                    DestinationFolder = new TaskItem(destFolder),
                    SkipUnchangedFiles = true
                };

                bool success = t.Execute();

                Assert.True(success); // "success"
                Assert.True(File.Exists(destFile)); // "destination exists"
                MockEngine.GetStringDelegate resourceDelegate = AssemblyResources.GetString;

                me.AssertLogContainsMessageFromResource(resourceDelegate, "Copy.HardLinkComment", sourceFile, destFile);

                // Can't do this below, because the real message doesn't end with String.Empty, it ends with a CLR exception string, and so matching breaks in PLOC.
                // Instead look for the HRESULT that CLR unfortunately puts inside its exception string. Something like this
                // Tried to create more than a few links to a file that is supported by the file system. (! yhMcE! Exception from HRESULT: Table c?! 0x80070476)
                // me.AssertLogContainsMessageFromResource(resourceDelegate, "Copy.RetryingAsFileCopy", sourceFile, destFile, String.Empty);
                me.AssertLogContains("0x80070476");

                string destinationFileContents;
                using (StreamReader sr = FileUtilities.OpenRead(destFile))
                    destinationFileContents = sr.ReadToEnd();

                Assert.Equal("This is a source temp file.", destinationFileContents); //"Expected the destination file to contain the contents of source file."

                Assert.Equal(1, t.DestinationFiles.Length);
                Assert.Equal(1, t.CopiedFiles.Length);
                Assert.Equal(destFile, t.DestinationFiles[0].ItemSpec);
                Assert.Equal(destFile, t.CopiedFiles[0].ItemSpec);

                // Now we will write new content to the source file
                // we'll then check that the destination file automatically
                // has the same content (i.e. it's been hard linked)
                using (StreamWriter sw = FileUtilities.OpenWrite(sourceFile, false))    // HIGHCHAR: Test writes in UTF8 without preamble.
                    sw.Write("This is another source temp file.");

                // Read the destination file (it should have the same modified content as the source)
                using (StreamReader sr = FileUtilities.OpenRead(destFile))
                    destinationFileContents = sr.ReadToEnd();

                Assert.Equal("This is a source temp file.", destinationFileContents); //"Expected the destination copied file to contain the contents of original source file only."

                ((MockEngine)t.BuildEngine).AssertLogDoesntContain("MSB3026"); // Didn't do retries
            }
            finally
            {
                File.Delete(sourceFile);
                File.Delete(destFile);
                FileUtilities.DeleteWithoutTrailingBackslash(destFolder, true);
            }
        }
    }

    public class CopySymbolicLink_Tests : Copy_Tests
    {
        public CopySymbolicLink_Tests()
        {
            useSymbolicLinks = true;
        }

        /// <summary>
        /// DestinationFolder should work.
        /// </summary>
        [Fact]
        public void CopyToDestinationFolderWithSymbolicLinkCheck()
        {
            var isPrivileged = true;

            if (NativeMethodsShared.IsWindows)
            {
                if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null)))
                {
                    isPrivileged = false;
                    Assert.True(true, "It seems that you don't have the permission to create symbolic links. Try to run this test again with higher privileges");
                }
            }

            if (isPrivileged)
            {
                string sourceFile = FileUtilities.GetTemporaryFile();
                string temp = Path.GetTempPath();
                string destFolder = Path.Combine(temp, "2A333ED756AF4dc392E728D0F864A398");
                string destFile = Path.Combine(destFolder, Path.GetFileName(sourceFile));
                try
                {
                    using (StreamWriter sw = FileUtilities.OpenWrite(sourceFile, true))    // HIGHCHAR: Test writes in UTF8 without preamble.
                        sw.Write("This is a source temp file.");

                    // Don't create the dest folder, let task do that

                    ITaskItem[] sourceFiles = new ITaskItem[] { new TaskItem(sourceFile) };

                    Copy t = new Copy();
                    t.RetryDelayMilliseconds = 1; // speed up tests!

                    // Allow the task's default (false) to have a chance
                    t.UseSymboliclinksIfPossible = true;

                    MockEngine me = new MockEngine(true);
                    t.BuildEngine = me;
                    t.SourceFiles = sourceFiles;
                    t.DestinationFolder = new TaskItem(destFolder);
                    t.SkipUnchangedFiles = true;

                    bool success = t.Execute();

                    Assert.True(success); // "success"
                    Assert.True(File.Exists(destFile)); // "destination exists"

                    MockEngine.GetStringDelegate resourceDelegate = AssemblyResources.GetString;

                    me.AssertLogContainsMessageFromResource(resourceDelegate, "Copy.SymbolicLinkComment", sourceFile, destFile);

                    string destinationFileContents;

                    using (StreamReader sr = FileUtilities.OpenRead(destFile))
                        destinationFileContents = sr.ReadToEnd();

                    Assert.Equal("This is a source temp file.", destinationFileContents); //"Expected the destination symbolic linked file to contain the contents of source file."

                    Assert.Equal(1, t.DestinationFiles.Length);
                    Assert.Equal(1, t.CopiedFiles.Length);
                    Assert.Equal(destFile, t.DestinationFiles[0].ItemSpec);
                    Assert.Equal(destFile, t.CopiedFiles[0].ItemSpec);

                    // Now we will write new content to the source file
                    // we'll then check that the destination file automatically
                    // has the same content (i.e. it's been hard linked)

                    using (StreamWriter sw = FileUtilities.OpenWrite(sourceFile, false))    // HIGHCHAR: Test writes in UTF8 without preamble.
                        sw.Write("This is another source temp file.");

                    // Read the destination file (it should have the same modified content as the source)
                    using (StreamReader sr = FileUtilities.OpenRead(destFile))
                        destinationFileContents = sr.ReadToEnd();

                    Assert.Equal("This is another source temp file.", destinationFileContents); //"Expected the destination hard linked file to contain the contents of source file. Even after modification of the source"

                    ((MockEngine)t.BuildEngine).AssertLogDoesntContain("MSB3891"); // Didn't do retries

                }
                finally
                {
                    File.Delete(sourceFile);
                    File.Delete(destFile);
                    FileUtilities.DeleteWithoutTrailingBackslash(destFolder, true);
                }
            }
        }
    }
}
