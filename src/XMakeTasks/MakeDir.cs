// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Diagnostics;
using System.Resources;
using System.Reflection;
using System.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;
using System.Collections.Generic;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// A task that creates a directory
    /// </summary>
    public class MakeDir : TaskExtension
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public MakeDir()
        {
        }

        [Required]
        public ITaskItem[] Directories
        {
            get
            {
                ErrorUtilities.VerifyThrowArgumentNull(_directories, "directories");
                return _directories;
            }

            set
            {
                _directories = value;
            }
        }

        [Output]
        public ITaskItem[] DirectoriesCreated
        {
            get
            {
                return _directoriesCreated;
            }
        }

        private ITaskItem[] _directories;
        private ITaskItem[] _directoriesCreated;

        #region ITask Members

        /// <summary>
        /// Executes the MakeDir task. Create the directory.
        /// </summary>
        public override bool Execute()
        {
            ArrayList items = new ArrayList();
            HashSet<string> directoriesSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
                                // Do not log a fake command line as well, as it's superfluous, and also potentially expensive
                                Log.LogMessageFromResources(MessageImportance.Normal, "MakeDir.Comment", directory.ItemSpec);

                                Directory.CreateDirectory(FileUtilities.FixFilePath(directory.ItemSpec));
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

            _directoriesCreated = (ITaskItem[])items.ToArray(typeof(ITaskItem));

            return !Log.HasLoggedErrors;
        }

        #endregion
    }
}
