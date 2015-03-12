// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Resources;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Task to call Path.Combine.
    /// </summary>
    public class CombinePath : TaskExtension
    {
        /// <summary>
        /// Default constructor.  Does nothing.
        /// </summary>
        public CombinePath()
        {
        }

        private string _basePath;
        private ITaskItem[] _paths;
        private ITaskItem[] _combinedPaths;

        /// <summary>
        /// The base path, the first parameter into Path.Combine.  Can be a relative path,
        /// absolute path, or (blank).
        /// </summary>
        public string BasePath
        {
            get
            {
                return _basePath;
            }

            set
            {
                _basePath = value;
            }
        }

        /// <summary>
        /// The list of paths to combine with the base path.  These can be relative paths
        /// or absolute paths.
        /// </summary>
        [Required]
        public ITaskItem[] Paths
        {
            get
            {
                ErrorUtilities.VerifyThrowArgumentNull(_paths, "paths");
                return _paths;
            }

            set
            {
                _paths = value;
            }
        }

        /// <summary>
        /// This is the output of the task, a list of paths produced by combining the base
        /// path with each of the paths passed in.
        /// </summary>
        [Output]
        public ITaskItem[] CombinedPaths
        {
            get
            {
                return _combinedPaths;
            }

            set
            {
                _combinedPaths = value;
            }
        }

        /// <summary>
        /// Calls Path.Combine for each of the inputs.  Preserves metadata.
        /// </summary>
        /// <returns>true on success, false on failure</returns>
        public override bool Execute()
        {
            if (this.BasePath == null)
            {
                this.BasePath = String.Empty;
            }

            List<ITaskItem> combinedPathsList = new List<ITaskItem>();

            foreach (ITaskItem path in this.Paths)
            {
                TaskItem combinedPath = new TaskItem(path);

                try
                {
                    combinedPath.ItemSpec = Path.Combine(_basePath, path.ItemSpec);
                    combinedPathsList.Add(combinedPath);
                }
                catch (ArgumentException e)
                {
                    Log.LogErrorWithCodeFromResources("General.InvalidArgument", e.Message);
                }
            }

            this.CombinedPaths = combinedPathsList.ToArray();
            return !Log.HasLoggedErrors;
        }
    }
}
