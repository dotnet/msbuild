// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// A task that calls BuildProjectFile to build another project.
    /// Used for testing BuildProjectFile callbacks from TaskHost.
    /// </summary>
    public class BuildProjectFileTask : Task
    {
        /// <summary>
        /// The project file to build.
        /// </summary>
        [Required]
        public string ProjectToBuild { get; set; }

        /// <summary>
        /// The targets to build. If not specified, builds default targets.
        /// </summary>
        public string[] Targets { get; set; }

        /// <summary>
        /// Whether the build succeeded.
        /// </summary>
        [Output]
        public bool BuildSucceeded { get; set; }

        /// <summary>
        /// The output items from the build (if any).
        /// </summary>
        [Output]
        public ITaskItem[] OutputItems { get; set; }

        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, $"BuildProjectFileTask: Building '{ProjectToBuild}'");

            IDictionary targetOutputs = new Hashtable();

            BuildSucceeded = BuildEngine.BuildProjectFile(
                ProjectToBuild,
                Targets,
                null, // globalProperties
                targetOutputs);

            Log.LogMessage(MessageImportance.High, $"BuildProjectFileTask: Build result = {BuildSucceeded}");

            // Extract output items if any targets returned outputs
            if (targetOutputs.Count > 0)
            {
                var outputList = new System.Collections.Generic.List<ITaskItem>();
                foreach (DictionaryEntry entry in targetOutputs)
                {
                    if (entry.Value is ITaskItem[] items)
                    {
                        outputList.AddRange(items);
                    }
                }
                OutputItems = outputList.ToArray();
                Log.LogMessage(MessageImportance.High, $"BuildProjectFileTask: Got {OutputItems.Length} output items");
            }

            return BuildSucceeded;
        }
    }
}
