// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Utilities;
#if FEATURE_WINDOWSINTEROP
using Windows.Win32.Storage.FileSystem;
#endif

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Task to move one or more files.
    /// </summary>
    /// <remarks>
    /// This does not support moving directories (ie, xcopy)
    /// but this could restriction could be lifted as MoveFileEx,
    /// which is used here, supports it.
    /// </remarks>
    [MSBuildMultiThreadableTask]
    public class Move : TaskExtension, ICancelableTask, IIncrementalTask, IMultiThreadableTask
    {
        /// <summary>
        /// Whether we should cancel.
        /// </summary>
        private bool _canceling;

        /// <summary>
        /// List of files to move.
        /// </summary>
        [Required]
        public ITaskItem[] SourceFiles { get; set; }

        /// <summary>
        /// Destination folder for all the source files.
        /// </summary>
        public ITaskItem DestinationFolder { get; set; }

        /// <summary>
        /// Whether to overwrite files in the destination
        /// that have the read-only attribute set.
        /// Default is to not overwrite.
        /// </summary>
        public bool OverwriteReadOnlyFiles { get; set; }

        /// <summary>
        /// Destination files matching each of the source files.
        /// </summary>
        [Output]
        public ITaskItem[] DestinationFiles { get; set; }

        /// <summary>
        /// Subset that were successfully moved.
        /// </summary>
        [Output]
        public ITaskItem[] MovedFiles { get; private set; }

        /// <summary>
        /// Set question parameter for Move task.
        /// </summary>
        /// <remarks>Move can be chained A->B->C with location C as the final location.
        /// Incrementally, it is hard to question A->B if both files are gone.
        /// In short, question will always return false and author should use target inputs/outputs.</remarks>
        public bool FailIfNotIncremental { get; set; }

        /// <inheritdoc />
        public TaskEnvironment TaskEnvironment { get; set; } = TaskEnvironment.Fallback;

        /// <summary>
        /// Stop and return (in an undefined state) as soon as possible.
        /// </summary>
        public void Cancel()
        {
            _canceling = true;
        }

        /// <summary>
        /// Main entry point.
        /// </summary>
        public override bool Execute()
        {
            bool success = true;

            // If there are no source files then just return success.
            if (SourceFiles == null || SourceFiles.Length == 0)
            {
                DestinationFiles = Array.Empty<ITaskItem>();
                MovedFiles = Array.Empty<ITaskItem>();
                return true;
            }

            // There must be a DestinationFolder (either files or directory).
            if (DestinationFiles == null && DestinationFolder == null)
            {
                Log.LogErrorWithCodeFromResources("Move.NeedsDestination", "DestinationFiles", "DestinationDirectory");
                return false;
            }

            // There can't be two kinds of destination.
            if (DestinationFiles != null && DestinationFolder != null)
            {
                Log.LogErrorWithCodeFromResources("Move.ExactlyOneTypeOfDestination", "DestinationFiles", "DestinationDirectory");
                return false;
            }

            // If the caller passed in DestinationFiles, then its length must match SourceFiles.
            if (DestinationFiles != null && DestinationFiles.Length != SourceFiles.Length)
            {
                Log.LogErrorWithCodeFromResources("General.TwoVectorsMustHaveSameLength", DestinationFiles.Length, SourceFiles.Length, "DestinationFiles", "SourceFiles");
                return false;
            }

            // If the caller passed in DestinationFolder, convert it to DestinationFiles
            if (DestinationFiles == null)
            {
                DestinationFiles = new ITaskItem[SourceFiles.Length];

                for (int i = 0; i < SourceFiles.Length; ++i)
                {
                    // Build the correct path.
                    string destinationFile;
                    try
                    {
                        destinationFile = Path.Combine(DestinationFolder.ItemSpec, Path.GetFileName(SourceFiles[i].ItemSpec));
                    }
                    catch (ArgumentException e)
                    {
                        Log.LogErrorWithCodeFromResources("Move.Error", SourceFiles[i].ItemSpec, DestinationFolder.ItemSpec, e.Message, string.Empty);

                        // Clear the outputs.
                        DestinationFiles = Array.Empty<ITaskItem>();
                        return false;
                    }

                    // Initialize the DestinationFolder item.
                    DestinationFiles[i] = new TaskItem(destinationFile);
                }
            }

            // Build up the sucessfully moved subset
            var destinationFilesSuccessfullyMoved = new List<ITaskItem>();

            // Now that we have a list of DestinationFolder files, move from source to DestinationFolder.
            for (int i = 0; i < SourceFiles.Length && !_canceling; ++i)
            {
                string sourceSpec = SourceFiles[i].ItemSpec;
                string destinationSpec = DestinationFiles[i].ItemSpec;

                AbsolutePath? sourceFile = null;
                AbsolutePath? destinationFile = null;

                try
                {
                    sourceFile = TaskEnvironment.GetAbsolutePath(sourceSpec);
                    destinationFile = TaskEnvironment.GetAbsolutePath(destinationSpec);

                    if (!FailIfNotIncremental && MoveFileWithLogging(sourceFile.Value, destinationFile.Value))
                    {
                        SourceFiles[i].CopyMetadataTo(DestinationFiles[i]);
                        destinationFilesSuccessfullyMoved.Add(DestinationFiles[i]);
                    }
                    else
                    {
                        success = false;
                    }
                }
                catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                {
                    string lockedFileMessage = LockCheck.GetLockedFileMessage(sourceFile?.OriginalValue ?? sourceSpec);
                    Log.LogErrorWithCodeFromResources("Move.Error", sourceSpec, destinationSpec, e.Message, lockedFileMessage);
                    success = false;

                    // Continue with the rest of the list
                }
            }

            // MovedFiles contains only the copies that were successful.
            MovedFiles = destinationFilesSuccessfullyMoved.ToArray();

            return success && !_canceling;
        }

        /// <summary>
        /// Makes the provided file writeable if necessary.
        /// </summary>
        private static void MakeWriteableIfReadOnly(AbsolutePath file)
        {
            var info = new FileInfo(file);
            if ((info.Attributes & FileAttributes.ReadOnly) != 0)
            {
                info.Attributes &= ~FileAttributes.ReadOnly;
            }
        }

        /// <summary>
        /// Move one file from source to destination. Create the target directory if necessary.
        /// </summary>
        /// <throws>IO related exceptions.</throws>
        private bool MoveFileWithLogging(
            AbsolutePath sourceFile,
            AbsolutePath destinationFile)
        {
            if (FileSystems.Default.DirectoryExists(destinationFile))
            {
                Log.LogErrorWithCodeFromResources("Move.DestinationIsDirectory", sourceFile.OriginalValue, destinationFile.OriginalValue);
                return false;
            }

            if (FileSystems.Default.DirectoryExists(sourceFile))
            {
                // If the source file passed in is actually a directory instead of a file, log a nice
                // error telling the user so.  Otherwise, .NET Framework's File.Move method will throw
                // an FileNotFoundException, which is not very useful to the user.
                Log.LogErrorWithCodeFromResources("Move.SourceIsDirectory", sourceFile.OriginalValue);
                return false;
            }

            // Check the source exists.
            if (!FileSystems.Default.FileExists(sourceFile))
            {
                Log.LogErrorWithCodeFromResources("Move.SourceDoesNotExist", sourceFile.OriginalValue);
                return false;
            }

            // We can't ovewrite a file unless it's writeable
            if (OverwriteReadOnlyFiles && FileSystems.Default.FileExists(destinationFile))
            {
                MakeWriteableIfReadOnly(destinationFile);
            }

            string destinationFolder = Path.GetDirectoryName(destinationFile);

            if (!string.IsNullOrEmpty(destinationFolder) && !FileSystems.Default.DirectoryExists(destinationFolder))
            {
                Log.LogMessageFromResources(MessageImportance.Normal, "Move.CreatesDirectory", destinationFolder);
                Directory.CreateDirectory(destinationFolder);
            }

            // Do not log a fake command line as well, as it's superfluous, and also potentially expensive
            Log.LogMessageFromResources(MessageImportance.Normal, "Move.FileComment", sourceFile.OriginalValue, destinationFile.OriginalValue);

            // We want to always overwrite any existing destination file.
            // Unlike File.Copy, File.Move does not have an overload to overwrite the destination.
            // We cannot simply delete the destination file first because possibly it is also the source!
            // Nor do we want to just do a Copy followed by a Delete, because for large files that will be slow.
            // We are forced to use Win32's MoveFileEx.
            bool result = MoveFileEx(sourceFile, destinationFile);

            if (!result)
            {
                // It failed so we need a nice error message. Unfortunately
                // Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error()); and
                // throw new IOException((new Win32Exception(error)).Message)
                // do not produce great error messages (eg., "The operation succeeded" (!)).
                // For this reason the BCL has is own mapping in System.IO.__Error.WinIOError
                // which is unfortunately internal.
                // So try to get a nice message by using the BCL Move(), which will likely fail
                // and throw. Otherwise use the "correct" method.
                File.Move(sourceFile, destinationFile);

                // Apparently that didn't throw, so..
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
            }

            // If the destination file exists, then make sure it's read-write.
            // The File.Move command copies attributes, but our move needs to
            // leave the file writeable.
            if (FileSystems.Default.FileExists(destinationFile))
            {
                // Make it writable
                MakeWriteableIfReadOnly(destinationFile);
            }

            return true;
        }

        /// <summary>
        /// Moves <paramref name="existingFileName"/> onto <paramref name="newFileName"/>, replacing the
        /// destination if it already exists. On Windows this defers to the Win32 <c>MoveFileEx</c> API, which
        /// can move across volumes and writes through to disk. On other platforms it falls back to a managed
        /// delete-then-move; that fallback is not a complete emulation but handles the common cases.
        /// </summary>
        private static bool MoveFileEx(AbsolutePath existingFileName, AbsolutePath newFileName)
        {
            if (NativeMethodsShared.IsWindows)
            {
#if FEATURE_WINDOWSINTEROP
                return Windows.Win32.PInvoke.MoveFileEx(
                    existingFileName,
                    newFileName,
                    // Do not return until the move is complete, and allow moving across volumes
                    MOVE_FILE_FLAGS.MOVEFILE_WRITE_THROUGH
                        | MOVE_FILE_FLAGS.MOVEFILE_REPLACE_EXISTING
                        | MOVE_FILE_FLAGS.MOVEFILE_COPY_ALLOWED);
#else
                return false;
#endif
            }

            if (!FileSystems.Default.FileExists(existingFileName))
            {
                return false;
            }

            var targetExists = FileSystems.Default.FileExists(newFileName);

            if (targetExists && (File.GetAttributes(newFileName) & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                throw new IOException("Moving target is read-only");
            }

            if (existingFileName == newFileName)
            {
                return true;
            }

            if (targetExists)
            {
                File.Delete(newFileName);
            }

            File.Move(existingFileName, newFileName);
            return true;
        }
    }
}
