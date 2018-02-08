// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.UnitTests;
using Microsoft.Build.Tasks;
using Microsoft.Build.Shared;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    sealed public class Move_Tests
    {
        /// <summary>
        /// Basic case of moving a file
        /// </summary>
        [Fact]
        public void BasicMove()
        {
            string sourceFile = FileUtilities.GetTemporaryFile();
            string destinationFile = FileUtilities.GetTemporaryFile();

            try
            {
                using (StreamWriter sw = FileUtilities.OpenWrite(sourceFile, true))
                    sw.Write("This is a source temp file.");
                FileInfo file = new FileInfo(sourceFile);
                file.Attributes = file.Attributes | FileAttributes.ReadOnly; // mark read only

                ITaskItem[] sourceFiles = new ITaskItem[] { new TaskItem(sourceFile) };
                ITaskItem[] destinationFiles = new ITaskItem[] { new TaskItem(destinationFile) };

                File.Delete(destinationFile);

                Move t = new Move();
                t.BuildEngine = new MockEngine(true /* log to console */);
                t.SourceFiles = sourceFiles;
                t.DestinationFiles = destinationFiles;

                Assert.True(t.Execute());

                Assert.False(File.Exists(sourceFile)); // "Expected the source file to be gone."
                Assert.True(File.Exists(destinationFile)); // "Expected the destination file to exist."
                Assert.Equal(1, t.DestinationFiles.Length);
                Assert.Equal(destinationFile, t.DestinationFiles[0].ItemSpec);
                Assert.Equal(1, t.MovedFiles.Length);
                Assert.Equal(true, ((new FileInfo(destinationFile)).Attributes & FileAttributes.ReadOnly) == 0); // should have cleared r/o bit
            }
            finally
            {
                File.Delete(sourceFile);
                File.Delete(destinationFile);
            }
        }

        /// <summary>
        /// Basic case of moving a file but with OverwriteReadOnlyFiles = true.
        /// </summary>
        [Fact]
        public void BasicMoveOverwriteReadOnlyFilesTrue()
        {
            string sourceFile = FileUtilities.GetTemporaryFile();
            string destinationFile = FileUtilities.GetTemporaryFile();

            try
            {
                using (StreamWriter sw = FileUtilities.OpenWrite(sourceFile, true))
                    sw.Write("This is a source temp file.");
                FileInfo file = new FileInfo(sourceFile);
                file.Attributes = file.Attributes | FileAttributes.ReadOnly; // mark read only

                ITaskItem[] sourceFiles = new ITaskItem[] { new TaskItem(sourceFile) };
                ITaskItem[] destinationFiles = new ITaskItem[] { new TaskItem(destinationFile) };

                File.Delete(destinationFile);

                Move t = new Move();
                t.BuildEngine = new MockEngine(true /* log to console */);
                t.SourceFiles = sourceFiles;
                t.OverwriteReadOnlyFiles = true;
                t.DestinationFiles = destinationFiles;

                Assert.True(t.Execute());

                Assert.False(File.Exists(sourceFile)); // "Expected the source file to be gone."
                Assert.True(File.Exists(destinationFile)); // "Expected the destination file to exist."
                Assert.Equal(1, t.DestinationFiles.Length);
                Assert.Equal(destinationFile, t.DestinationFiles[0].ItemSpec);
                Assert.Equal(1, t.MovedFiles.Length);
                Assert.Equal(true, ((new FileInfo(destinationFile)).Attributes & FileAttributes.ReadOnly) == 0); // should have cleared r/o bit
            }
            finally
            {
                if (File.Exists(sourceFile))
                {
                    FileInfo file = new FileInfo(sourceFile);
                    file.Attributes = file.Attributes & ~FileAttributes.ReadOnly; // mark read only
                    File.Delete(sourceFile);
                }

                File.Delete(destinationFile);
            }
        }

        /// <summary>
        /// File to move does not exist
        /// Should not overwrite destination!
        /// </summary>
        [Fact]
        public void NonexistentSource()
        {
            string sourceFile = FileUtilities.GetTemporaryFile();
            string destinationFile = FileUtilities.GetTemporaryFile();

            try
            {
                File.Delete(sourceFile);
                using (StreamWriter sw = FileUtilities.OpenWrite(destinationFile, true))
                    sw.Write("This is a destination temp file.");

                ITaskItem[] sourceFiles = new ITaskItem[] { new TaskItem(sourceFile) };
                ITaskItem[] destinationFiles = new ITaskItem[] { new TaskItem(destinationFile) };

                Move t = new Move();
                t.BuildEngine = new MockEngine(true /* log to console */);
                t.SourceFiles = sourceFiles;
                t.DestinationFiles = destinationFiles;

                Assert.False(t.Execute());

                Assert.False(File.Exists(sourceFile)); // "Expected the source file to still not exist."
                Assert.True(File.Exists(destinationFile)); // "Expected the destination file to still exist."
                Assert.Equal(1, t.DestinationFiles.Length);
                Assert.Equal(destinationFile, t.DestinationFiles[0].ItemSpec);
                Assert.Equal(0, t.MovedFiles.Length);

                string destinationFileContents;
                using (StreamReader sr = FileUtilities.OpenRead(destinationFile))
                    destinationFileContents = sr.ReadToEnd();

                Assert.Equal(destinationFileContents, "This is a destination temp file."); //                     "Expected the destination file to still contain the contents of destination file."
            }
            finally
            {
                File.Delete(sourceFile);
                File.Delete(destinationFile);
            }
        }

        /// <summary>
        /// A file can be moved onto itself successfully (it's a no-op).
        /// </summary>
        [Fact]
        public void MoveOverSelfIsSuccessful()
        {
            string sourceFile = FileUtilities.GetTemporaryFile();
            try
            {
                using (StreamWriter sw = FileUtilities.OpenWrite(sourceFile, true))
                {
                    sw.Write("This is a temp file.");
                }

                ITaskItem[] sourceFiles = new ITaskItem[] { new TaskItem(sourceFile) };
                ITaskItem[] destinationFiles = new ITaskItem[] { new TaskItem(sourceFile) };

                Move t = new Move();

                t.BuildEngine = new MockEngine(true /* log to console */);
                t.SourceFiles = sourceFiles;
                t.DestinationFiles = destinationFiles;

                // Success
                Assert.True(t.Execute());

                // File is still there.
                Assert.True(File.Exists(sourceFile)); // "Source file should be there"
            }
            finally
            {
                File.Delete(sourceFile);
            }
        }

        /// <summary>
        /// Move should overwrite any destination file except if it's r/o
        /// </summary>
        [Fact]
        public void MoveOverExistingFileReadOnlyNoOverwrite()
        {
            string sourceFile = FileUtilities.GetTemporaryFile();
            string destinationFile = FileUtilities.GetTemporaryFile();
            try
            {
                using (StreamWriter sw = FileUtilities.OpenWrite(sourceFile, true))
                    sw.Write("This is a source temp file.");

                using (StreamWriter sw = FileUtilities.OpenWrite(destinationFile, true))
                    sw.Write("This is a destination temp file.");

                ITaskItem[] sourceFiles = new ITaskItem[] { new TaskItem(sourceFile) };
                ITaskItem[] destinationFiles = new ITaskItem[] { new TaskItem(destinationFile) };

                FileInfo file = new FileInfo(destinationFile);
                file.Attributes = file.Attributes | FileAttributes.ReadOnly; // mark destination read only

                Move t = new Move();
                t.BuildEngine = new MockEngine(true /* log to console */);
                t.SourceFiles = sourceFiles;
                t.DestinationFiles = destinationFiles;

                Assert.False(t.Execute());

                Assert.True(File.Exists(sourceFile)); // "Source file should be present"

                string destinationFileContents;
                using (StreamReader sr = FileUtilities.OpenRead(destinationFile))
                    destinationFileContents = sr.ReadToEnd();

                Assert.Equal(destinationFileContents, "This is a destination temp file."); //                     "Expected the destination file to be unchanged."

                Assert.Equal(true, ((new FileInfo(destinationFile)).Attributes & FileAttributes.ReadOnly) != 0); // should still be r/o
            }
            finally
            {
                File.Delete(sourceFile);

                FileInfo file = new FileInfo(destinationFile);
                file.Attributes = file.Attributes ^ FileAttributes.ReadOnly; // mark destination writable only
                File.Delete(destinationFile);
            }
        }

        /// <summary>
        /// Move should overwrite any writable destination file
        /// </summary>
        [Fact]
        public void MoveOverExistingFileDestinationWriteable()
        {
            string sourceFile = FileUtilities.GetTemporaryFile();
            string destinationFile = FileUtilities.GetTemporaryFile();
            try
            {
                using (StreamWriter sw = FileUtilities.OpenWrite(sourceFile, true))
                    sw.Write("This is a source temp file.");

                using (StreamWriter sw = FileUtilities.OpenWrite(destinationFile, true))
                    sw.Write("This is a destination temp file.");

                ITaskItem[] sourceFiles = new ITaskItem[] { new TaskItem(sourceFile) };
                ITaskItem[] destinationFiles = new ITaskItem[] { new TaskItem(destinationFile) };

                Move t = new Move();
                t.BuildEngine = new MockEngine(true /* log to console */);
                t.SourceFiles = sourceFiles;
                t.DestinationFiles = destinationFiles;

                t.Execute();

                Assert.False(File.Exists(sourceFile)); // "Source file should be gone"

                string destinationFileContents;
                using (StreamReader sr = FileUtilities.OpenRead(destinationFile))
                    destinationFileContents = sr.ReadToEnd();

                Assert.Equal(destinationFileContents, "This is a source temp file."); //                     "Expected the destination file to contain the contents of source file."
            }
            finally
            {
                File.Delete(sourceFile);
                File.Delete(destinationFile);
            }
        }


        /// <summary>
        /// Move should overwrite any destination file even if it's not r/o
        /// if OverwriteReadOnlyFiles is set.
        /// 
        /// This is a regression test for bug 814744 where a move operation with OverwriteReadonlyFiles = true on a destination file with the readonly 
        /// flag not set caused the readonly flag to be set before the move which caused the move to fail.
        /// </summary>
        [Fact]
        public void MoveOverExistingFileOverwriteReadOnly()
        {
            string sourceFile = FileUtilities.GetTemporaryFile();
            string destinationFile = FileUtilities.GetTemporaryFile();
            try
            {
                using (StreamWriter sw = FileUtilities.OpenWrite(sourceFile, true))
                    sw.Write("This is a source temp file.");

                using (StreamWriter sw = FileUtilities.OpenWrite(destinationFile, true))
                    sw.Write("This is a destination temp file.");

                ITaskItem[] sourceFiles = new ITaskItem[] { new TaskItem(sourceFile) };
                ITaskItem[] destinationFiles = new ITaskItem[] { new TaskItem(destinationFile) };

                FileInfo file = new FileInfo(destinationFile);
                file.Attributes = file.Attributes & ~FileAttributes.ReadOnly; // mark not read only

                Move t = new Move();
                t.OverwriteReadOnlyFiles = true;
                t.BuildEngine = new MockEngine(true /* log to console */);
                t.SourceFiles = sourceFiles;
                t.DestinationFiles = destinationFiles;

                t.Execute();

                Assert.False(File.Exists(sourceFile)); // "Source file should be gone"

                string destinationFileContents;
                using (StreamReader sr = FileUtilities.OpenRead(destinationFile))
                    destinationFileContents = sr.ReadToEnd();

                Assert.Equal(destinationFileContents, "This is a source temp file."); //                     "Expected the destination file to contain the contents of source file."

                Assert.Equal(true, ((new FileInfo(destinationFile)).Attributes & FileAttributes.ReadOnly) == 0); // readonly bit should not be set
            }
            finally
            {
                File.Delete(sourceFile);
                File.Delete(destinationFile);
            }
        }

        /// <summary>
        /// Move should overwrite any destination file even if it's r/o
        /// if OverwriteReadOnlyFiles is set.
        /// </summary>
        [Fact]
        public void MoveOverExistingFileOverwriteReadOnlyOverWriteReadOnlyFilesTrue()
        {
            string sourceFile = FileUtilities.GetTemporaryFile();
            string destinationFile = FileUtilities.GetTemporaryFile();
            try
            {
                using (StreamWriter sw = FileUtilities.OpenWrite(sourceFile, true))
                    sw.Write("This is a source temp file.");

                using (StreamWriter sw = FileUtilities.OpenWrite(destinationFile, true))
                    sw.Write("This is a destination temp file.");

                ITaskItem[] sourceFiles = new ITaskItem[] { new TaskItem(sourceFile) };
                ITaskItem[] destinationFiles = new ITaskItem[] { new TaskItem(destinationFile) };

                FileInfo file = new FileInfo(destinationFile);
                file.Attributes = file.Attributes | FileAttributes.ReadOnly; // mark read only

                Move t = new Move();
                t.OverwriteReadOnlyFiles = true;
                t.BuildEngine = new MockEngine(true /* log to console */);
                t.SourceFiles = sourceFiles;
                t.DestinationFiles = destinationFiles;

                t.Execute();

                Assert.False(File.Exists(sourceFile)); // "Source file should be gone"

                string destinationFileContents;
                using (StreamReader sr = FileUtilities.OpenRead(destinationFile))
                    destinationFileContents = sr.ReadToEnd();

                Assert.Equal(destinationFileContents, "This is a source temp file."); //                     "Expected the destination file to contain the contents of source file."

                Assert.Equal(true, ((new FileInfo(destinationFile)).Attributes & FileAttributes.ReadOnly) == 0); // should have cleared r/o bit
            }
            finally
            {
                File.Delete(sourceFile);
                File.Delete(destinationFile);
            }
        }

        /// <summary>
        /// MovedFiles should only include files that were successfully moved 
        /// (or skipped), not files for which there was an error.
        /// </summary>
        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)] // "Under Unix all filenames are valid and this test is not useful"
        public void OutputsOnlyIncludeSuccessfulMoves()
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

                Move t = new Move();
                MockEngine engine = new MockEngine(true /* log to console */);
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
                Assert.Equal(1, t.MovedFiles.Length);
                Assert.Equal(validOutFile, t.MovedFiles[0].ItemSpec);
                Assert.Equal(2, t.DestinationFiles.Length);
                Assert.Equal("fr", t.DestinationFiles[1].GetMetadata("Locale"));

                // Output ItemSpec should not be overwritten.
                Assert.Equal(invalidFile, t.DestinationFiles[0].ItemSpec);
                Assert.Equal(validOutFile, t.DestinationFiles[1].ItemSpec);
                Assert.Equal(validOutFile, t.MovedFiles[0].ItemSpec);

                // Sources attributes should be left untouched.
                Assert.Equal("en-GB", t.SourceFiles[1].GetMetadata("Locale"));
                Assert.Equal("taupe", t.SourceFiles[1].GetMetadata("Color"));

                // Attributes not on Sources should be left untouched.
                Assert.Equal("Pumpkin", t.DestinationFiles[1].GetMetadata("Flavor"));
                Assert.Equal("Pumpkin", t.MovedFiles[0].GetMetadata("Flavor"));

                // Attribute should have been forwarded
                Assert.Equal("taupe", t.DestinationFiles[1].GetMetadata("Color"));
                Assert.Equal("taupe", t.MovedFiles[0].GetMetadata("Color"));

                // Attribute should not have been updated if it already existed on destination
                Assert.Equal("fr", t.DestinationFiles[1].GetMetadata("Locale"));
                Assert.Equal("fr", t.MovedFiles[0].GetMetadata("Locale"));
            }
            finally
            {
                File.Delete(inFile1);
                File.Delete(inFile2);
                File.Delete(validOutFile);
            }
        }

        /// <summary>
        /// Moving a locked file will fail
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void MoveLockedFile()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                bool result;
                Move move = null;

                using (StreamWriter writer = FileUtilities.OpenWrite(file, false)) // lock it for write
                {
                    move = new Move();
                    move.BuildEngine = new MockEngine(true /* log to console */);
                    move.SourceFiles = new ITaskItem[] { new TaskItem(file) };
                    move.DestinationFiles = new ITaskItem[] { new TaskItem(file + "2") };
                    result = move.Execute();
                }

                Assert.False(result);
                ((MockEngine)move.BuildEngine).AssertLogContains("MSB3677");
                Assert.Equal(false, File.Exists(file + "2"));
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Must have destination
        /// </summary>
        [Fact]
        public void NoDestination()
        {
            Move move = new Move();
            move.BuildEngine = new MockEngine();
            move.SourceFiles = new ITaskItem[] { new TaskItem("source") };

            Assert.Equal(false, move.Execute());
            ((MockEngine)move.BuildEngine).AssertLogContains("MSB3679");
        }

        /// <summary>
        /// Can't have both destination file and directory
        /// </summary>
        [Fact]
        public void DestinationFileAndDirectory()
        {
            Move move = new Move();
            move.BuildEngine = new MockEngine();
            move.SourceFiles = new ITaskItem[] { new TaskItem("source") };
            move.DestinationFiles = new ITaskItem[] { new TaskItem("x") };
            move.DestinationFolder = new TaskItem(Directory.GetCurrentDirectory());

            Assert.Equal(false, move.Execute());
            ((MockEngine)move.BuildEngine).AssertLogContains("MSB3678");
        }

        /// <summary>
        /// Can't specify a directory for the destination file
        /// </summary>
        [Fact]
        public void DestinationFileIsDirectory()
        {
            Move move = new Move();
            move.BuildEngine = new MockEngine();
            move.SourceFiles = new ITaskItem[] { new TaskItem("source") };
            move.DestinationFiles = new ITaskItem[] { new TaskItem(Directory.GetCurrentDirectory()) };

            Assert.Equal(false, move.Execute());
            ((MockEngine)move.BuildEngine).AssertLogContains("MSB3676");
        }

        /// <summary>
        /// Can't move a directory to a file
        /// </summary>
        [Fact]
        public void SourceFileIsDirectory()
        {
            Move move = new Move();
            move.BuildEngine = new MockEngine();
            move.DestinationFiles = new ITaskItem[] { new TaskItem("destination") };
            move.SourceFiles = new ITaskItem[] { new TaskItem(Directory.GetCurrentDirectory()) };

            Assert.Equal(false, move.Execute());
            ((MockEngine)move.BuildEngine).AssertLogContains("MSB3681");
        }

        /// <summary>
        /// Moving a file on top of itself should be a success (no-op).
        /// Variation with different casing/relativeness.
        /// </summary>
        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)] // "File names under Unix are case-sensitive and this test is not useful"
        public void MoveFileOnItself2()
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

                Move t = new Move();
                MockEngine engine = new MockEngine(true /* log to console */);
                t.BuildEngine = engine;
                t.SourceFiles = new ITaskItem[] { new TaskItem(file) };
                t.DestinationFiles = new ITaskItem[] { new TaskItem(filename.ToLowerInvariant()) };
                bool success = t.Execute();

                Assert.True(success);
                Assert.Equal(1, t.DestinationFiles.Length);
                Assert.Equal(filename.ToLowerInvariant(), t.DestinationFiles[0].ItemSpec);

                Assert.True(File.Exists(file)); // "Source file should be there"
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Moving a file on top of itself should be a success (no-op).
        /// Variation with a second move failure.
        /// </summary>
        [Fact]
        public void MoveFileOnItselfAndFailAMove()
        {
            string temp = Path.GetTempPath();
            string file = Path.Combine(temp, "2A333ED756AF4dc392E728D0F864A395");
            string invalidFile = "!/@#$%^&*()|";
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

                Move t = new Move();
                MockEngine engine = new MockEngine(true /* log to console */);
                t.BuildEngine = engine;
                t.SourceFiles = new ITaskItem[] { new TaskItem(file), new TaskItem(invalidFile) };
                t.DestinationFiles = new ITaskItem[] { new TaskItem(file), new TaskItem(dest2) };
                bool success = t.Execute();

                Assert.False(success);
                Assert.Equal(2, t.DestinationFiles.Length);
                Assert.Equal(file, t.DestinationFiles[0].ItemSpec);
                Assert.Equal(dest2, t.DestinationFiles[1].ItemSpec);
                Assert.Equal(1, t.MovedFiles.Length);
                Assert.Equal(file, t.MovedFiles[0].ItemSpec);
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
        public void MoveToNonexistentDestinationFolder()
        {
            string sourceFile = FileUtilities.GetTemporaryFile();
            string temp = Path.GetTempPath();
            string destFolder = Path.Combine(temp, "2A333ED756AF4d1392E728D0F864A398");
            string destFile = Path.Combine(destFolder, Path.GetFileName(sourceFile));
            try
            {
                using (StreamWriter sw = FileUtilities.OpenWrite(sourceFile, true))
                    sw.Write("This is a source temp file.");

                // Don't create the dest folder, let task do that

                ITaskItem[] sourceFiles = new ITaskItem[] { new TaskItem(sourceFile) };

                Move t = new Move();
                t.BuildEngine = new MockEngine(true /* log to console */);
                t.SourceFiles = sourceFiles;
                t.DestinationFolder = new TaskItem(destFolder);

                bool success = t.Execute();

                Assert.True(success); // "success"
                Assert.False(File.Exists(sourceFile)); // "source gone"
                Assert.True(File.Exists(destFile)); // "destination exists"

                string destinationFileContents;
                using (StreamReader sr = FileUtilities.OpenRead(destFile))
                    destinationFileContents = sr.ReadToEnd();

                Assert.Equal(destinationFileContents, "This is a source temp file."); //                     "Expected the destination file to contain the contents of source file."

                Assert.Equal(1, t.DestinationFiles.Length);
                Assert.Equal(1, t.MovedFiles.Length);
                Assert.Equal(destFile, t.DestinationFiles[0].ItemSpec);
                Assert.Equal(destFile, t.MovedFiles[0].ItemSpec);
            }
            finally
            {
                File.Delete(sourceFile);
                File.Delete(destFile);
                FileUtilities.DeleteWithoutTrailingBackslash(destFolder);
            }
        }


        /// <summary>
        /// DestinationFiles should only include files that were successfully moved,
        /// not files for which there was an error.
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

                Move t = new Move();
                MockEngine engine = new MockEngine(true /* log to console */);
                t.BuildEngine = engine;

                t.SourceFiles = new ITaskItem[] { new TaskItem(inFile1), new TaskItem(inFile2) };
                t.DestinationFiles = new ITaskItem[] { new TaskItem(outFile1) };

                bool success = t.Execute();

                Assert.False(success);
                Assert.Equal(1, t.DestinationFiles.Length);
                Assert.Null(t.MovedFiles);
                Assert.False(File.Exists(outFile1));
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
        [SkipOnTargetFramework(TargetFrameworkMonikers.Netcoreapp)]
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

                Move t = new Move();
                t.BuildEngine = new MockEngine(true /* log to console */);
                t.SourceFiles = sourceFiles;
                t.DestinationFiles = destinationFiles;

                bool result = t.Execute();

                // Expect for there to have been no copies.
                Assert.Equal(false, result);
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
        [SkipOnTargetFramework(TargetFrameworkMonikers.Netcoreapp)]
        public void Regress451057_ExitGracefullyIfPathNameIsTooLong2()
        {
            string sourceFile = "ABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZ";
            string destinationFile = FileUtilities.GetTemporaryFile();

            ITaskItem[] sourceFiles = new ITaskItem[] { new TaskItem(sourceFile) };
            ITaskItem[] destinationFiles = new ITaskItem[] { new TaskItem(destinationFile) };

            Move t = new Move();
            t.BuildEngine = new MockEngine(true /* log to console */);
            t.SourceFiles = sourceFiles;
            t.DestinationFiles = destinationFiles;

            bool result = t.Execute();

            // Expect for there to have been no copies.
            Assert.Equal(false, result);
        }

        /// <summary>
        /// If the SourceFiles parameter is given invalid path
        /// characters, make sure the task exits gracefully.
        /// </summary>
        [Fact]
        public void ExitGracefullyOnInvalidPathCharacters()
        {
            Move t = new Move();
            t.BuildEngine = new MockEngine(true /* log to console */);
            t.SourceFiles = new ITaskItem[] { new TaskItem("foo | bar") };
            t.DestinationFolder = new TaskItem("dest");

            bool result = t.Execute();

            // Expect for there to have been no copies.
            Assert.Equal(false, result);
        }

        /// <summary>
        /// If the DestinationFile parameter is given invalid path
        /// characters, make sure the task exits gracefully.
        /// </summary>
        [Fact]
        public void ExitGracefullyOnInvalidPathCharactersInDestinationFile()
        {
            Move t = new Move();
            t.BuildEngine = new MockEngine(true /* log to console */);
            t.SourceFiles = new ITaskItem[] { new TaskItem("source") };
            t.DestinationFiles = new ITaskItem[] { new TaskItem("foo | bar") };

            bool result = t.Execute();

            // Expect for there to have been no copies.
            Assert.Equal(false, result);
        }

        /// <summary>
        /// If the DestinationFile parameter is given invalid path
        /// characters, make sure the task exits gracefully.
        /// </summary>
        [Fact]
        public void ExitGracefullyOnInvalidPathCharactersInDestinationFolder()
        {
            Move t = new Move();
            t.BuildEngine = new MockEngine(true /* log to console */);
            t.SourceFiles = new ITaskItem[] { new TaskItem("source") };
            t.DestinationFolder = new TaskItem("foo | bar");

            bool result = t.Execute();

            // Expect for there to have been no copies.
            Assert.Equal(false, result);
        }
    }
}
