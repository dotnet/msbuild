// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// A task that creates a directory
    /// </summary>
    public class MakeDir : TaskExtension, IIncrementalTask
    {
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

        [Output]
        public ITaskItem[] DirectoriesCreated { get; private set; }

        public bool FailIfNotIncremental { get; set; }

        private ITaskItem[] _directories;

        #region ITask Members

        /// <summary>
        /// Executes the MakeDir task. Create the directory.
        /// </summary>
        public override bool Execute()
        {
            var items = new List<ITaskItem>();
            var directoriesSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (ITaskItem directory in Directories)
            {
                // Sometimes people pass in an item transform like @(myitem->'%(RelativeDir)') in order
                // to create a bunch of directories for a set of items.  But if the item
                // is in the current project directory, %(RelativeDir) evaluates to empty-string.  So,
                // here we check for that case.
                if (directory.ItemSpec.Length > 0)
                {
                    try
                    {
                        // For speed, eliminate duplicates caused by poor targets authoring
                        if (!directoriesSet.Contains(directory.ItemSpec))
                        {
                            // Only log a message if we actually need to create the folder
                            if (!FileUtilities.DirectoryExistsNoThrow(directory.ItemSpec))
                            {
                                if (FailIfNotIncremental)
                                {
                                    Log.LogErrorFromResources("MakeDir.Comment", directory.ItemSpec);
                                }
                                else
                                {
                                    // Do not log a fake command line as well, as it's superfluous, and also potentially expensive
                                    Log.LogMessageFromResources(MessageImportance.Normal, "MakeDir.Comment", directory.ItemSpec);

                                    Directory.CreateDirectory(FileUtilities.FixFilePath(directory.ItemSpec));
                                }
                            }

                            items.Add(directory);
                        }
                    }
                    catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                    {
                        Log.LogErrorWithCodeFromResources("MakeDir.Error", directory.ItemSpec, e.Message);
                    }

                    // Add even on failure to avoid reattempting
                    directoriesSet.Add(directory.ItemSpec);
                }
            }

            DirectoriesCreated = items.ToArray();

            return !Log.HasLoggedErrors;
        }

        #endregion
    }
}
