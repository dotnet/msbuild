// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Delete files from disk.
    /// </summary>
    public class Delete : TaskExtension, ICancelableTask
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
        /// Delete the files.
        /// </summary>
        public override bool Execute()
        {
            var deletedFilesList = new List<ITaskItem>();
            var deletedFilesSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (ITaskItem file in Files)
            {
                if (_canceling)
                {
                    return false;
                }

                try
                {
                    // For speed, eliminate duplicates caused by poor targets authoring
                    if (!deletedFilesSet.Contains(file.ItemSpec))
                    {
                        if (File.Exists(file.ItemSpec))
                        {
                            // Do not log a fake command line as well, as it's superfluous, and also potentially expensive
                            Log.LogMessageFromResources(MessageImportance.Normal, "Delete.DeletingFile", file.ItemSpec);

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
                    }
                }
                catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                {
                    LogError(file, e);
                }

                // Add even on failure to avoid reattempting
                deletedFilesSet.Add(file.ItemSpec);
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
