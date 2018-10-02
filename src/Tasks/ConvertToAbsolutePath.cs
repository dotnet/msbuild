// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Task to call Path.GetFullPath
    /// </summary>
    public class ConvertToAbsolutePath : TaskExtension
    {
        private ITaskItem[] _paths;

        /// <summary>
        /// The list of paths to convert to absolute paths.
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
        /// This is the output of the task, a list of absolute paths for the items passed in
        /// </summary>
        [Output]
        public ITaskItem[] AbsolutePaths { get; set; }

        /// <summary>
        /// Calls Path.GetFullPath for each of the inputs.  Preserves metadata.
        /// </summary>
        /// <returns>true on success, false on failure</returns>
        public override bool Execute()
        {
            var absolutePathsList = new List<ITaskItem>();

            foreach (ITaskItem path in Paths)
            {
                try
                {
                    // Only call Path.GetFullPath if the path is not rooted to avoid 
                    // going to disk when it is not necessary
                    if (!Path.IsPathRooted(path.ItemSpec))
                    {
                        if (path is ITaskItem2 item2)
                        {
                            item2.EvaluatedIncludeEscaped = item2.GetMetadataValueEscaped("FullPath");
                        }
                        else
                        {
                            path.ItemSpec = EscapingUtilities.Escape(path.GetMetadata("FullPath"));
                        }
                    }
                    absolutePathsList.Add(path);
                }
                catch (ArgumentException e)
                {
                    Log.LogErrorWithCodeFromResources("General.InvalidArgument", e.Message);
                }
            }

            AbsolutePaths = absolutePathsList.ToArray();
            return !Log.HasLoggedErrors;
        }
    }
}
