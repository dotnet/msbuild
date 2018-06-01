// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Moves files from one place to another.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

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
    public class Move : TaskExtension, ICancelableTask
    {
        /// <summary>
        /// Flags for MoveFileEx.
        /// 
        /// </summary>
        private const NativeMethods.MoveFileFlags Flags = NativeMethods.MoveFileFlags.MOVEFILE_WRITE_THROUGH |    // Do not return until the Move is complete
                                                          NativeMethods.MoveFileFlags.MOVEFILE_REPLACE_EXISTING | // Replace any existing target
                                                          NativeMethods.MoveFileFlags.MOVEFILE_COPY_ALLOWED;      // Moving across volumes is allowed

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
        /// Subset that were successfully moved 
        /// </summary>
        [Output]
        public ITaskItem[] MovedFiles { get; private set; }

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
                        Log.LogErrorWithCodeFromResources("Move.Error", SourceFiles[i].ItemSpec, DestinationFolder.ItemSpec, e.Message);

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
                string sourceFile = SourceFiles[i].ItemSpec;
                string destinationFile = DestinationFiles[i].ItemSpec;

                try
                {
                    if (MoveFileWithLogging(sourceFile, destinationFile))
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
                    Log.LogErrorWithCodeFromResources("Move.Error", sourceFile, destinationFile, e.Message);
                    success = false;

                    // Continue with the rest of the list
                }
            }

            // MovedFiles contains only the copies that were successful.
            MovedFiles = destinationFilesSuccessfullyMoved.ToArray();

            return success && !_canceling;
        }

        /// <summary>
        /// Makes the provided file writeable if necessary
        /// </summary>
        private static void MakeWriteableIfReadOnly(string file)
        {
            var info = new FileInfo(file);
            if ((info.Attributes & FileAttributes.ReadOnly) != 0)
            {
                info.Attributes = info.Attributes & ~FileAttributes.ReadOnly;
            }
        }

        /// <summary>
        /// Move one file from source to destination. Create the target directory if necessary.
        /// </summary>
        /// <throws>IO related exceptions</throws>
        private bool MoveFileWithLogging
        (
            string sourceFile,
            string destinationFile
        )
        {
            if (Directory.Exists(destinationFile))
            {
                Log.LogErrorWithCodeFromResources("Move.DestinationIsDirectory", sourceFile, destinationFile);
                return false;
            }

            if (Directory.Exists(sourceFile))
            {
                // If the source file passed in is actually a directory instead of a file, log a nice
                // error telling the user so.  Otherwise, .NET Framework's File.Move method will throw
                // an FileNotFoundException, which is not very useful to the user.
                Log.LogErrorWithCodeFromResources("Move.SourceIsDirectory", sourceFile);
                return false;
            }

            // Check the source exists.
            if (!File.Exists(sourceFile))
            {
                Log.LogErrorWithCodeFromResources("Move.SourceDoesNotExist", sourceFile);
                return false;
            }

            // We can't ovewrite a file unless it's writeable
            if (OverwriteReadOnlyFiles && File.Exists(destinationFile))
            {
                MakeWriteableIfReadOnly(destinationFile);
            }

            string destinationFolder = Path.GetDirectoryName(destinationFile);

            if (!string.IsNullOrEmpty(destinationFolder) && !Directory.Exists(destinationFolder))
            {
                Log.LogMessageFromResources(MessageImportance.Normal, "Move.CreatesDirectory", destinationFolder);
                Directory.CreateDirectory(destinationFolder);
            }

            // Do not log a fake command line as well, as it's superfluous, and also potentially expensive
            Log.LogMessageFromResources(MessageImportance.Normal, "Move.FileComment", sourceFile, destinationFile);

            // We want to always overwrite any existing destination file.
            // Unlike File.Copy, File.Move does not have an overload to overwrite the destination.
            // We cannot simply delete the destination file first because possibly it is also the source!
            // Nor do we want to just do a Copy followed by a Delete, because for large files that will be slow.
            // We are forced to use Win32's MoveFileEx.
            bool result = NativeMethods.MoveFileEx(sourceFile, destinationFile, Flags);

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
            if (File.Exists(destinationFile))
            {
                // Make it writable
                MakeWriteableIfReadOnly(destinationFile);
            }

            return true;
        }
    }
}
