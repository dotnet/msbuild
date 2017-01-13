// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Runtime.InteropServices;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// A task that copies files.
    /// </summary>
    public class Copy : TaskExtension, ICancelableTask
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public Copy()
        {
            this.RetryDelayMilliseconds = RetryDelayMillisecondsDefault;
        }

        #region Properties

        private ITaskItem[] _sourceFiles = null;
        private ITaskItem _destinationFolder = null;
        private ITaskItem[] _destinationFiles = null;
        private bool _skipUnchangedFiles = false;
        private ITaskItem[] _copiedFiles = null;
        private bool _canceling = false;
        private bool _overwriteReadOnlyFiles = false;
        private HashSet<string> _directoriesKnownToExist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Force the copy to retry even when it hits ERROR_ACCESS_DENIED -- normally we wouldn't retry in this case since 
        /// normally there's no point, but occasionally things get into a bad state temporarily, and retrying does actually 
        /// succeed.  So keeping around a secret environment variable to allow forcing that behavior if necessary.  
        /// </summary>
        private static bool s_alwaysRetryCopy = Environment.GetEnvironmentVariable("MSBUILDALWAYSRETRY") != null;

        /// <summary>
        /// Default to retrying "on". This is for robustness. We know that there are some files (eg interop assys) that are 
        /// just prone to colliding in shared output directories -- we will still warn, so that they can improve things, and
        /// spot the other case where there's an actual build process bug, but we don't want to break the build for the 
        /// sake of purity.
        /// </summary>
        private int _retries = 10;

        /// <summary>
        /// Default milliseconds to wait between necessary retries
        /// </summary>
        private const int RetryDelayMillisecondsDefault = 1000;

        [Required]
        public ITaskItem[] SourceFiles
        {
            get
            {
                return _sourceFiles;
            }

            set
            {
                _sourceFiles = value;
            }
        }

        public ITaskItem DestinationFolder
        {
            get
            {
                return _destinationFolder;
            }

            set
            {
                _destinationFolder = value;
            }
        }

        /// <summary>
        /// How many times to attempt to copy, if all previous
        /// attempts failed. Defaults to zero.
        /// Warning: using retries may mask a synchronization problem in your
        /// build process.
        /// </summary>
        public int Retries
        {
            get { return _retries; }
            set { _retries = value; }
        }

        /// <summary>
        /// Delay between any necessary retries.
        /// Defaults to <see cref="RetryDelayMillisecondsDefault">RetryDelayMillisecondsDefault</see>
        /// </summary>
        public int RetryDelayMilliseconds
        {
            get;
            set;
        }

        /// <summary>
        /// Create Hard Links for the copied files rather than copy the files if possible to do so
        /// </summary>
        public bool UseHardlinksIfPossible
        {
            get;
            set;
        }

        /// <summary>
        /// Create Symbolic Links for the copied files rather than copy the files if possible to do so
        /// </summary>
        public bool UseSymboliclinksIfPossible
        {
            get;
            set;
        }

        public bool SkipUnchangedFiles
        {
            get
            {
                return _skipUnchangedFiles;
            }

            set
            {
                _skipUnchangedFiles = value;
            }
        }

        [Output]
        public ITaskItem[] DestinationFiles
        {
            get
            {
                return _destinationFiles;
            }

            set
            {
                _destinationFiles = value;
            }
        }

        // Subset that were successfully copied
        [Output]
        public ITaskItem[] CopiedFiles
        {
            get
            {
                return _copiedFiles;
            }
        }

        /// <summary>
        /// Whether to overwrite files in the destination
        /// that have the read-only attribute set.
        /// </summary>
        public bool OverwriteReadOnlyFiles
        {
            get
            {
                return _overwriteReadOnlyFiles;
            }
            set
            {
                _overwriteReadOnlyFiles = value;
            }
        }

        #endregion

        /// <summary>
        /// Stop and return (in an undefined state) as soon as possible.
        /// </summary>
        public void Cancel()
        {
            _canceling = true;
        }

        #region ITask Members

        /// <summary>
        /// Method compares two files and returns true if their size and timestamp are identical.
        /// </summary>
        /// <param name="sourceFile">The source file</param>
        /// <param name="destinationFile">The destination file</param>
        /// <returns></returns>
        private static bool IsMatchingSizeAndTimeStamp
        (
            FileState sourceFile,
            FileState destinationFile
        )
        {
            // If the destination doesn't exists, then it is not a matching file.
            if (!destinationFile.FileExists)
            {
                return false;
            }

            if (sourceFile.LastWriteTimeUtcFast != destinationFile.LastWriteTimeUtcFast)
            {
                return false;
            }

            if (sourceFile.Length != destinationFile.Length)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// INTERNAL FOR UNIT-TESTING ONLY
        /// 
        /// We've got several environment variables that we read into statics since we don't expect them to ever 
        /// reasonably change, but we need some way of refreshing their values so that we can modify them for 
        /// unit testing purposes. 
        /// </summary>
        internal static void RefreshInternalEnvironmentValues()
        {
            s_alwaysRetryCopy = Environment.GetEnvironmentVariable("MSBUILDALWAYSRETRY") != null;
        }

        /// <summary>
        /// If MSBUILDALWAYSRETRY is set, also log useful diagnostic information -- as 
        /// a warning, so it's easily visible. 
        /// </summary>
        private void LogDiagnostic(string message, params object[] messageArgs)
        {
            if (s_alwaysRetryCopy)
            {
                Log.LogWarning(message, messageArgs);
            }
        }

        /// <summary>
        /// Copy one file from source to destination. Create the target directory if necessary and 
        /// leave the file read-write.
        /// </summary>
        /// <param name="sourceFileState"></param>
        /// <param name="destinationFileState"></param>
        /// <returns>Return true to indicate success, return false to indicate failure and NO retry, return NULL to indicate retry.</returns>
        private bool? CopyFileWithLogging
        (
            FileState sourceFileState,      // The source file
            FileState destinationFileState  // The destination file
        )
        {
            bool destinationFileExists = false;

            if (destinationFileState.DirectoryExists)
            {
                Log.LogErrorWithCodeFromResources("Copy.DestinationIsDirectory", sourceFileState.Name, destinationFileState.Name);
                return false;
            }

            if (sourceFileState.DirectoryExists)
            {
                // If the source file passed in is actually a directory instead of a file, log a nice
                // error telling the user so.  Otherwise, .NET Framework's File.Copy method will throw
                // an UnauthorizedAccessException saying "access is denied", which is not very useful
                // to the user.
                Log.LogErrorWithCodeFromResources("Copy.SourceIsDirectory", sourceFileState.Name);
                return false;
            }

            if (!sourceFileState.FileExists)
            {
                Log.LogErrorWithCodeFromResources("Copy.SourceFileNotFound", sourceFileState.Name);
                return false;
            }

            string destinationFolder = Path.GetDirectoryName(destinationFileState.Name);

            if (destinationFolder != null && destinationFolder.Length > 0 && !_directoriesKnownToExist.Contains(destinationFolder))
            {
                if (!Directory.Exists(destinationFolder))
                {
                    Log.LogMessageFromResources(MessageImportance.Normal, "Copy.CreatesDirectory", destinationFolder);
                    Directory.CreateDirectory(destinationFolder);
                }

                // It's very common for a lot of files to be copied to the same folder. 
                // Eg., "c:\foo\a"->"c:\bar\a", "c:\foo\b"->"c:\bar\b" and so forth.
                // We don't want to check whether this folder exists for every single file we copy. So store which we've checked.
                _directoriesKnownToExist.Add(destinationFolder);
            }

            if (_overwriteReadOnlyFiles)
            {
                MakeFileWriteable(destinationFileState, true);
                destinationFileExists = destinationFileState.FileExists;
            }

            bool linkCreated = false;
            string errorMessage = string.Empty;

            // If we want to create hard or symbolic links, then try that first
            if (UseHardlinksIfPossible)
                TryCopyViaLink("Copy.HardLinkComment", MessageImportance.Normal, sourceFileState, destinationFileState, ref destinationFileExists, ref linkCreated, ref errorMessage, (source, destination, errMessage) => NativeMethods.MakeHardLink(destination, source, ref errorMessage));
            else if (UseSymboliclinksIfPossible)
                TryCopyViaLink("Copy.SymbolicLinkComment", MessageImportance.Normal, sourceFileState, destinationFileState, ref destinationFileExists, ref linkCreated, ref errorMessage, (source, destination, errMessage) => NativeMethods.MakeSymbolicLink(destination, source, ref errorMessage));

            // If the link was not created (either because the user didn't want one, or because it couldn't be created)
            // then let's copy the file
            if (!linkCreated)
            {
                // Do not log a fake command line as well, as it's superfluous, and also potentially expensive
                Log.LogMessageFromResources(MessageImportance.Normal, "Copy.FileComment", sourceFileState.Name, destinationFileState.Name);
                File.Copy(sourceFileState.Name, destinationFileState.Name, true);
            }

            destinationFileState.Reset();

            // If the destinationFile file exists, then make sure it's read-write.
            // The File.Copy command copies attributes, but our copy needs to
            // leave the file writeable.
            if (sourceFileState.IsReadOnly)
            {
                MakeFileWriteable(destinationFileState, false);
            }

            return true;
        }

        private void TryCopyViaLink(string linkComment, MessageImportance messageImportance, FileState sourceFileState, FileState destinationFileState, ref bool destinationFileExists, ref bool linkCreated, ref string errorMessage, Func<string, string, string, bool> createLink)
        {
            // Do not log a fake command line as well, as it's superfluous, and also potentially expensive
            Log.LogMessageFromResources(MessageImportance.Normal, linkComment, sourceFileState.Name, destinationFileState.Name);

            if (!_overwriteReadOnlyFiles)
            {
                destinationFileExists = destinationFileState.FileExists;
            }

            // CreateHardLink and CreateSymbolicLink cannot overwrite an existing file or link
            // so we need to delete the existing entry before we create the hard or symbolic link.
            // We need to do a best-effort check to see if the files are the same
            // if they are the same then we won't delete, just in case they refer to the same
            // physical file on disk.
            // Since we'll fall back to a copy (below) this will fail and issue a correct
            // message in the case that the source and destination are in fact the same file.
            if (destinationFileExists && !IsMatchingSizeAndTimeStamp(sourceFileState, destinationFileState))
            {
                FileUtilities.DeleteNoThrow(destinationFileState.Name);
            }
            
            linkCreated = createLink(sourceFileState.Name, destinationFileState.Name, errorMessage);

            if (!linkCreated)
            {
                // This is only a message since we don't want warnings when copying to network shares etc.
                Log.LogMessageFromResources(messageImportance, "Copy.RetryingAsFileCopy", sourceFileState.Name, destinationFileState.Name, errorMessage);
            }
        }

        /// <summary>
        /// Ensure the read-only attribute on the specified file is off, so
        /// the file is writeable.
        /// </summary>
        private void MakeFileWriteable(FileState file, bool logActivity)
        {
            if (file.FileExists)
            {
                if (file.IsReadOnly)
                {
                    if (logActivity)
                    {
                        Log.LogMessageFromResources(MessageImportance.Low, "Copy.RemovingReadOnlyAttribute", file.Name);
                    }

                    File.SetAttributes(file.Name, FileAttributes.Normal);
                    file.Reset();
                }
            }
        }

        /// <summary>
        /// Copy the files.
        /// </summary>
        /// <param name="copyFile">Delegate used to copy the files.</param>
        /// <returns></returns>
        internal bool Execute
        (
            CopyFileWithState copyFile
        )
        {
            // If there are no source files then just return success.
            if (_sourceFiles == null || _sourceFiles.Length == 0)
            {
                _destinationFiles = new TaskItem[0];
                _copiedFiles = new TaskItem[0];
                return true;
            }

            if (!(ValidateInputs() && InitializeDestinationFiles()))
            {
                return false;
            }

            bool success = true;

            // Environment variable stomps on user-requested value if it's set. 
            if (Environment.GetEnvironmentVariable("MSBUILDALWAYSOVERWRITEREADONLYFILES") != null)
            {
                _overwriteReadOnlyFiles = true;
            }

            // Build up the sucessfully copied subset
            var destinationFilesSuccessfullyCopied = new List<ITaskItem>();

            // Set of files we actually copied and the location from which they were originally copied.  The purpose
            // of this collection is to let us skip copying duplicate files.  We will only copy the file if it 
            // either has never been copied to this destination before (key doesn't exist) or if we have copied it but
            // from a different location (value is different.)
            // { dest -> source }
            Dictionary<string, string> filesActuallyCopied = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Now that we have a list of destinationFolder files, copy from source to destinationFolder.
            for (int i = 0; i < _sourceFiles.Length && !_canceling; ++i)
            {
                bool copyComplete = false;
                string originalSource;
                if (filesActuallyCopied.TryGetValue(_destinationFiles[i].ItemSpec, out originalSource))
                {
                    if (String.Equals(originalSource, _sourceFiles[i].ItemSpec, StringComparison.OrdinalIgnoreCase))
                    {
                        // Already copied from this location, don't copy again.
                        copyComplete = true;
                    }
                }

                if (!copyComplete)
                {
                    if (DoCopyIfNecessary(new FileState(_sourceFiles[i].ItemSpec), new FileState(_destinationFiles[i].ItemSpec), copyFile))
                    {
                        filesActuallyCopied[_destinationFiles[i].ItemSpec] = _sourceFiles[i].ItemSpec;
                        copyComplete = true;
                    }
                    else
                    {
                        success = false;
                    }
                }

                if (copyComplete)
                {
                    _sourceFiles[i].CopyMetadataTo(_destinationFiles[i]);
                    destinationFilesSuccessfullyCopied.Add(_destinationFiles[i]);
                }
            }

            // copiedFiles contains only the copies that were successful.
            _copiedFiles = (ITaskItem[])destinationFilesSuccessfullyCopied.ToArray();

            return success && !_canceling;
        }

        /// <summary>
        /// Verify that the inputs are correct.
        /// </summary>
        /// <returns></returns>
        private bool ValidateInputs()
        {
            if (Retries < 0)
            {
                Log.LogErrorWithCodeFromResources("Copy.InvalidRetryCount", Retries);
                return false;
            }

            if (RetryDelayMilliseconds < 0)
            {
                Log.LogErrorWithCodeFromResources("Copy.InvalidRetryDelay", RetryDelayMilliseconds);
                return false;
            }

            // There must be a destinationFolder (either files or directory).
            if (_destinationFiles == null && _destinationFolder == null)
            {
                Log.LogErrorWithCodeFromResources("Copy.NeedsDestination", "DestinationFiles", "DestinationFolder");
                return false;
            }

            // There can't be two kinds of destination.
            if (_destinationFiles != null && _destinationFolder != null)
            {
                Log.LogErrorWithCodeFromResources("Copy.ExactlyOneTypeOfDestination", "DestinationFiles", "DestinationFolder");
                return false;
            }

            // If the caller passed in DestinationFiles, then its length must match SourceFiles.
            if (_destinationFiles != null && _destinationFiles.Length != _sourceFiles.Length)
            {
                Log.LogErrorWithCodeFromResources("General.TwoVectorsMustHaveSameLength", _destinationFiles.Length, _sourceFiles.Length, "DestinationFiles", "SourceFiles");
                return false;
            }

            //First check if create hard or symbolic link option is selected. If both then return an error
            if (UseHardlinksIfPossible & UseSymboliclinksIfPossible)
            {
                Log.LogErrorWithCodeFromResources("Copy.ExactlyOneTypeOfLink", "UseHardlinksIfPossible", "UseSymboliclinksIfPossible");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Set up our list of destination files.
        /// </summary>
        /// <returns></returns>
        private bool InitializeDestinationFiles()
        {
            if (_destinationFiles == null)
            {
                // If the caller passed in DestinationFolder, convert it to DestinationFiles
                _destinationFiles = new ITaskItem[_sourceFiles.Length];

                for (int i = 0; i < _sourceFiles.Length; ++i)
                {
                    // Build the correct path.
                    string destinationFile;
                    try
                    {
                        destinationFile = Path.Combine(_destinationFolder.ItemSpec, Path.GetFileName(_sourceFiles[i].ItemSpec));
                    }
                    catch (ArgumentException e)
                    {
                        Log.LogErrorWithCodeFromResources("Copy.Error", _sourceFiles[i].ItemSpec, _destinationFolder.ItemSpec, e.Message);
                        // Clear the outputs.
                        _destinationFiles = new ITaskItem[0];
                        return false;
                    }

                    // Initialize the destinationFolder item.
                    // ItemSpec is unescaped, and the TaskItem constructor expects an escaped input, so we need to 
                    // make sure to re-escape it here. 
                    _destinationFiles[i] = new TaskItem(EscapingUtilities.Escape(destinationFile));

                    // Copy meta-data from source to destinationFolder.
                    _sourceFiles[i].CopyMetadataTo(_destinationFiles[i]);
                }
            }

            return true;
        }

        /// <summary>
        /// Copy source to destination, unless SkipUnchangedFiles is true and they are equivalent.
        /// </summary>
        /// <param name="sourceFileState"></param>
        /// <param name="destinationFileState"></param>
        /// <param name="copyFile"></param>
        /// <returns></returns>
        private bool DoCopyIfNecessary(FileState sourceFileState, FileState destinationFileState, CopyFileWithState copyFile)
        {
            bool success = true;

            try
            {
                if (_skipUnchangedFiles && IsMatchingSizeAndTimeStamp(sourceFileState, destinationFileState))
                {
                    // If we got here, then the file's time and size match AND
                    // the user set the SkipUnchangedFiles flag which means we
                    // should skip matching files.
                    Log.LogMessageFromResources
                    (
                        MessageImportance.Low,
                        "Copy.DidNotCopyBecauseOfFileMatch",
                        sourceFileState.Name,
                        destinationFileState.Name,
                        "SkipUnchangedFiles",
                        "true"
                    );
                }
                // We only do the cheap check for identicalness here, we try the more expensive check
                // of comparing the fullpaths of source and destination to see if they are identical,
                // in the exception handler lower down.
                else if (0 != String.Compare(sourceFileState.Name, destinationFileState.Name, StringComparison.OrdinalIgnoreCase))
                {
                    success = DoCopyWithRetries(sourceFileState, destinationFileState, copyFile);
                }
            }
            catch (PathTooLongException e)
            {
                Log.LogErrorWithCodeFromResources("Copy.Error", sourceFileState.Name, destinationFileState.Name, e.Message);
                success = false;
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                Log.LogErrorWithCodeFromResources("Copy.Error", sourceFileState.Name, destinationFileState.Name, e.Message);
                success = false;
            }

            return success;
        }

        /// <summary>
        /// Copy one file with the appropriate number of retries if it fails.
        /// </summary>
        private bool DoCopyWithRetries(FileState sourceFileState, FileState destinationFileState, CopyFileWithState copyFile)
        {
            bool? result = null;
            int retries = 0;

            while (true && !_canceling)
            {
                try
                {
                    result = copyFile(sourceFileState, destinationFileState);
                    if (result.HasValue)
                    {
                        return result.Value;
                    }
                }
                catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                {
                    if (e is ArgumentException ||  // Invalid chars
                        e is NotSupportedException || // Colon in the middle of the path
                        e is PathTooLongException)
                    {
                        // No use retrying these cases
                        throw;
                    }

                    if (e is UnauthorizedAccessException || e is IOException) // Not clear why we can get one and not the other
                    {
                        int code = Marshal.GetHRForException(e);

                        LogDiagnostic("Got {0} copying {1} to {2} and HR is {3}", e.ToString(), sourceFileState.Name, destinationFileState.Name, code);
                        if (code == Microsoft.Build.Tasks.NativeMethods.ERROR_ACCESS_DENIED)
                        {
                            // ERROR_ACCESS_DENIED can either mean there's an ACL preventing us, or the file has the readonly bit set.
                            // In either case, that's likely not a race, and retrying won't help.
                            // Retrying is mainly for ERROR_SHARING_VIOLATION, where someone else is using the file right now.
                            // However, there is a limited set of circumstances where a copy failure will show up as access denied due 
                            // to a failure to reset the readonly bit properly, in which case retrying will succeed.  This seems to be 
                            // a pretty edge scenario, but since some of our internal builds appear to be hitting it, provide a secret
                            // environment variable to allow overriding the default behavior and forcing retries in this circumstance as well. 
                            if (!s_alwaysRetryCopy)
                            {
                                throw;
                            }
                            else
                            {
                                LogDiagnostic("Retrying on ERROR_ACCESS_DENIED because MSBUILDALWAYSRETRY = 1");
                            }
                        }
                    }

                    if (e is IOException && DestinationFolder != null && File.Exists(DestinationFolder.ItemSpec))
                    {
                        // We failed to create the DestinationFolder because it's an existing file. No sense retrying.
                        // We don't check for this case upstream because it'd be another hit to the filesystem.
                        throw;
                    }

                    if (e is IOException)
                    {
                        // if this was just because the source and destination files are the
                        // same file, that's not a failure.
                        // Note -- we check this exceptional case here, not before the copy, for perf.
                        if (PathsAreIdentical(sourceFileState.Name, destinationFileState.Name))
                        {
                            return true;
                        }
                    }

                    if (retries < Retries)
                    {
                        retries++;
                        Log.LogWarningWithCodeFromResources("Copy.Retrying", sourceFileState.Name, destinationFileState.Name, retries, RetryDelayMilliseconds, e.Message);

                        // if we have to retry for some reason, wipe the state -- it may not be correct anymore. 
                        destinationFileState.Reset();

                        Thread.Sleep(RetryDelayMilliseconds);
                        continue;
                    }
                    else if (Retries > 0)
                    {
                        // Exception message is logged in caller
                        Log.LogErrorWithCodeFromResources("Copy.ExceededRetries", sourceFileState.Name, destinationFileState.Name, Retries);
                        throw;
                    }
                    else
                    {
                        throw;
                    }
                }

                if (retries < Retries)
                {
                    retries++;
                    Log.LogWarningWithCodeFromResources("Copy.Retrying", sourceFileState.Name, destinationFileState.Name, retries, RetryDelayMilliseconds, String.Empty /* no details */);

                    // if we have to retry for some reason, wipe the state -- it may not be correct anymore. 
                    destinationFileState.Reset();

                    Thread.Sleep(RetryDelayMilliseconds);
                }
                else if (Retries > 0)
                {
                    Log.LogErrorWithCodeFromResources("Copy.ExceededRetries", sourceFileState.Name, destinationFileState.Name, Retries);
                    return false;
                }
                else
                {
                    return false;
                }
            }

            // Canceling
            return false;
        }

        /// <summary>
        /// Standard entry point.
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            return Execute
            (
                new CopyFileWithState(CopyFileWithLogging)
            );
        }

        /// <summary>
        /// Compares two paths to see if they refer to the same file. We can't solve the general
        /// canonicalization problem, so we just compare strings on the full paths.
        /// </summary>
        private bool PathsAreIdentical(string source, string destination)
        {
            string fullSourcePath = Path.GetFullPath(source);
            string fullDestinationPath = Path.GetFullPath(destination);
            StringComparison filenameComparison;
            if (NativeMethodsShared.IsWindows)
            {
                filenameComparison = StringComparison.OrdinalIgnoreCase;
            }
            else
            {
                filenameComparison = StringComparison.Ordinal;
            }
            return (0 == String.Compare(fullSourcePath, fullDestinationPath, filenameComparison));
        }

        #endregion
    }
}
