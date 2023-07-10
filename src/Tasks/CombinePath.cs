// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Task to call Path.Combine.
    /// </summary>
    public class CombinePath : TaskExtension
    {
        private ITaskItem[] _paths;

        /// <summary>
        /// The base path, the first parameter into Path.Combine.  Can be a relative path,
        /// absolute path, or (blank).
        /// </summary>
        public string BasePath { get; set; }

        /// <summary>
        /// The list of paths to combine with the base path.  These can be relative paths
        /// or absolute paths.
        /// </summary>
        [Required]
        public ITaskItem[] Paths
        {
            get
            {
                ErrorUtilities.VerifyThrowArgumentNull(_paths, nameof(Paths));
                return _paths;
            }

            set => _paths = value;
        }

        /// <summary>
        /// This is the output of the task, a list of paths produced by combining the base
        /// path with each of the paths passed in.
        /// </summary>
        [Output]
        public ITaskItem[] CombinedPaths { get; set; }

        /// <summary>
        /// Calls Path.Combine for each of the inputs.  Preserves metadata.
        /// </summary>
        /// <returns>true on success, false on failure</returns>
        public override bool Execute()
        {
            if (BasePath == null)
            {
                BasePath = String.Empty;
            }

            var combinedPathsList = new List<ITaskItem>();

            foreach (ITaskItem path in Paths)
            {
                var combinedPath = new TaskItem(path);

                try
                {
                    combinedPath.ItemSpec = Path.Combine(BasePath, path.ItemSpec);
                    combinedPathsList.Add(combinedPath);
                }
                catch (ArgumentException e)
                {
                    Log.LogErrorWithCodeFromResources("General.InvalidArgument", e.Message);
                }
            }

            CombinedPaths = combinedPathsList.ToArray();
            return !Log.HasLoggedErrors;
        }
    }
}
