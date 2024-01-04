// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Delete files from disk.
    /// </summary>
    public class Delete : TaskExtension, ICancelableTask, IIncrementalTask
    {
        #region Properties

        private ITaskItem[] _files;
        private bool _canceling;

        [Required]
        public ITaskItem[] Files
        {
            get
            {
                ErrorUtilities.VerifyThrowArgumentNull(_files, nameof(Files));
                return _files;
            }

            set => _files = value;
        }

        /// <summary>
        /// When true, errors will be logged as warnings.
        /// </summary>
        public bool TreatErrorsAsWarnings { get; set; } = false;

        [Output]
        public ITaskItem[] DeletedFiles { get; set; }


        /// <summary>
        /// Gets or sets the delay, in milliseconds, between any necessary retries.
        /// </summary>
        public int RetryDelayMilliseconds { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the number of times to attempt to copy, if all previous attempts failed.
        /// </summary>
        public int Retries { get; set; } = 0;

        #endregion

        /// <summary>
        /// Set question parameter to verify if this is incremental.
        /// </summary>
        /// <remarks></remarks>
        public bool FailIfNotIncremental { get; set; }

        /// <summary>
        /// Verify that the inputs are correct.
        /// </summary>
        /// <returns>False on an error, implying that the overall delete operation should be aborted.</returns>
        private bool ValidateInputs()
        {
            if (Retries < 0)
            {
                Log.LogErrorWithCodeFromResources("Delete.InvalidRetryCount", Retries);
                return false;
            }

            if (RetryDelayMilliseconds < 0)
            {
                Log.LogErrorWithCodeFromResources("Delete.InvalidRetryDelay", RetryDelayMilliseconds);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Stop and return (in an undefined state) as soon as possible.
        /// </summary>
        public void Cancel()
        {
            _canceling = true;
        }

        #region ITask Members

        /// <summary>
        /// Delete the files.
        /// </summary>
        public override bool Execute()
        {
            if (!ValidateInputs())
            {
                return false;
            }
            var deletedFilesList = new List<ITaskItem>();
            var deletedFilesSet = new HashSet<string>(FileUtilities.PathComparer);

            foreach (ITaskItem file in Files)
            {
                if (_canceling)
                {
                    DeletedFiles = deletedFilesList.ToArray();
                    return false;
                }

                int retries = 0;
                while (!deletedFilesSet.Contains(file.ItemSpec))
                {
                    try
                    {
                        if (FileSystems.Default.FileExists(file.ItemSpec))
                        {
                            if (FailIfNotIncremental)
                            {
                                Log.LogWarningFromResources("Delete.DeletingFile", file.ItemSpec);
                            }
                            else
                            {
                                // Do not log a fake command line as well, as it's superfluous, and also potentially expensive
                                Log.LogMessageFromResources(MessageImportance.Normal, "Delete.DeletingFile", file.ItemSpec);
                            }

                            File.Delete(file.ItemSpec);
                        }
                        else
                        {
                            Log.LogMessageFromResources(MessageImportance.Low, "Delete.SkippingNonexistentFile", file.ItemSpec);
                        }
                        // keep a running list of the files that were actually deleted
                        // note that we include in this list files that did not exist
                        ITaskItem deletedFile = new TaskItem(file);
                        deletedFilesList.Add(deletedFile);
                        // Avoid reattempting when succeed to delete and file doesn't exist.
                        deletedFilesSet.Add(file.ItemSpec);
                    }
                    catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                    {
                        if (retries < Retries)
                        {
                            retries++;
                            Log.LogWarningWithCodeFromResources("Delete.Retrying", file.ToString(), retries, RetryDelayMilliseconds, e.Message);

                            Thread.Sleep(RetryDelayMilliseconds);
                            continue;
                        }
                        else
                        {
                            LogError(file, e);
                            // Add on failure to avoid reattempting
                            deletedFilesSet.Add(file.ItemSpec);
                        }
                    }
                }
            }
            // convert the list of deleted files into an array of ITaskItems
            DeletedFiles = deletedFilesList.ToArray();
            return !Log.HasLoggedErrors;
        }

        /// <summary>
        /// Log an error.
        /// </summary>
        /// <param name="file">The file that wasn't deleted.</param>
        /// <param name="e">The exception.</param>
        private void LogError(ITaskItem file, Exception e)
        {
            if (TreatErrorsAsWarnings)
            {
                Log.LogWarningWithCodeFromResources("Delete.Error", file.ItemSpec, e.Message);
            }
            else
            {
                Log.LogErrorWithCodeFromResources("Delete.Error", file.ItemSpec, e.Message);
            }
        }

        #endregion
    }
}
