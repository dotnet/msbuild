// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Remove the specified directories.
    /// </summary>
    public class RemoveDir : TaskExtension
    {
        //-----------------------------------------------------------------------------------
        // Property:  directory to remove
        //-----------------------------------------------------------------------------------
        private ITaskItem[] _directories;

        [Required]
        public ITaskItem[] Directories
        {
            get
            {
                ErrorUtilities.VerifyThrowArgumentNull(_directories, nameof(Directories));
                return _directories;
            }
            set => _directories = value;
        }

        //-----------------------------------------------------------------------------------
        // Property:  list of directories that were removed from disk
        //-----------------------------------------------------------------------------------

        [Output]
        public ITaskItem[] RemovedDirectories { get; set; }

        //-----------------------------------------------------------------------------------
        // Execute -- this runs the task
        //-----------------------------------------------------------------------------------
        public override bool Execute()
        {
            // Delete each directory
            bool overallSuccess = true;
            // Our record of the directories that were removed
            var removedDirectoriesList = new List<ITaskItem>();

            foreach (ITaskItem directory in Directories)
            {
                if (Directory.Exists(directory.ItemSpec))
                {
                    // Do not log a fake command line as well, as it's superfluous, and also potentially expensive
                    Log.LogMessageFromResources(MessageImportance.Normal, "RemoveDir.Removing", directory.ItemSpec);

                    // Try to remove the directory, this will not log unauthorized access errors since
                    // we will attempt to remove read only attributes and try again.
                    bool currentSuccess = RemoveDirectory(directory, false, out bool unauthorizedAccess);

                    // The first attempt failed, to we will remove readonly attributes and try again..
                    if (!currentSuccess && unauthorizedAccess)
                    {
                        // If the directory delete operation returns an unauthorized access exception
                        // we need to attempt to remove the readonly attributes and try again.
                        currentSuccess = RemoveReadOnlyAttributeRecursively(new DirectoryInfo(directory.ItemSpec));
                        if (currentSuccess)
                        {
                            // Retry the remove directory operation, this time we want to log any errors
                            currentSuccess = RemoveDirectory(directory, true, out unauthorizedAccess);
                        }
                    }

                    // The current directory was not removed successfully
                    if (!currentSuccess)
                    {
                        overallSuccess = false;
                    }

                    // We successfully removed the directory, so add the removed directory to our record
                    if (currentSuccess)
                    {
                        // keep a running list of the directories that were actually removed
                        // note that we include in this list directories that did not exist
                        removedDirectoriesList.Add(new TaskItem(directory));
                    }
                }
                else
                {
                    Log.LogMessageFromResources(MessageImportance.Normal, "RemoveDir.SkippingNonexistentDirectory", directory.ItemSpec);
                    // keep a running list of the directories that were actually removed
                    // note that we include in this list directories that did not exist
                    removedDirectoriesList.Add(new TaskItem(directory));
                }
            }
            // convert the list of deleted files into an array of ITaskItems
            RemovedDirectories = removedDirectoriesList.ToArray();
            return overallSuccess;
        }

        // Core implementation of directory removal
        private bool RemoveDirectory(ITaskItem directory, bool logUnauthorizedError, out bool unauthorizedAccess)
        {
            bool success = true;

            unauthorizedAccess = false;

            try
            {
                // Try to delete the directory
                Directory.Delete(directory.ItemSpec, true);
            }
            catch (UnauthorizedAccessException e)
            {
                success = false;
                // Log the fact that there was a problem only if we have been asked to.
                if (logUnauthorizedError)
                {
                    Log.LogErrorWithCodeFromResources("RemoveDir.Error", directory, e.Message);
                }
                unauthorizedAccess = true;
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                Log.LogErrorWithCodeFromResources("RemoveDir.Error", directory.ItemSpec, e.Message);
                success = false;
            }

            return success;
        }

        // recursively remove RO attribs from all files
        private bool RemoveReadOnlyAttributeRecursively(DirectoryInfo directory)
        {
            bool success = true;
            try
            {
                // Remove the ReadOnly attribute from the directory if it is present
                if ((directory.Attributes & FileAttributes.ReadOnly) != 0)
                {
                    FileAttributes faNew = (directory.Attributes & ~FileAttributes.ReadOnly);
                    directory.Attributes = faNew;
                }

                // For each file in the directory remove the readonly attribute if it is present
                foreach (FileSystemInfo file in directory.GetFileSystemInfos())
                {
                    if ((file.Attributes & FileAttributes.ReadOnly) != 0)
                    {
                        FileAttributes faNew = (file.Attributes & ~FileAttributes.ReadOnly);
                        file.Attributes = faNew;
                    }
                }

                // Recursively call ourselves for sub-directories
                foreach (DirectoryInfo folder in directory.GetDirectories())
                {
                    success = RemoveReadOnlyAttributeRecursively(folder);
                }
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                Log.LogErrorWithCodeFromResources("RemoveDir.Error", directory, e.Message);
                success = false;
            }

            return success;
        }
    }
}
