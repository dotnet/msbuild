// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// A test task that calls IBuildEngine.BuildProjectFile to build another project.
    /// Used by TaskHostCallback_Tests (in-process) and NetTaskHost_E2E_Tests (cross-runtime).
    /// The E2E project includes this file via linked compile to avoid duplication.
    /// </summary>
    public class BuildProjectFileTask : Task
    {
        /// <summary>
        /// Path to the project file to build.
        /// </summary>
        [Required]
        public string ProjectFile { get; set; } = string.Empty;

        /// <summary>
        /// Semicolon-separated list of targets to build. If empty, builds default targets.
        /// </summary>
        public string Targets { get; set; } = string.Empty;

        /// <summary>
        /// Semicolon-separated list of Property=Value pairs to pass as global properties.
        /// </summary>
        public string Properties { get; set; } = string.Empty;

        /// <summary>
        /// Whether the child build succeeded.
        /// </summary>
        [Output]
        public bool BuildSucceeded { get; set; }

        /// <summary>
        /// Target output items from the child build (if any).
        /// </summary>
        [Output]
        public ITaskItem[] OutputItems { get; set; } = [];

        public override bool Execute()
        {
            string[]? targetNames = null;
            if (!string.IsNullOrEmpty(Targets))
            {
                targetNames = Targets.Split(';');
            }

            Hashtable? globalProperties = null;
            if (!string.IsNullOrEmpty(Properties))
            {
                globalProperties = new Hashtable();
                foreach (string pair in Properties.Split(';'))
                {
                    string[] parts = pair.Split('=');
                    if (parts.Length == 2)
                    {
                        globalProperties[parts[0].Trim()] = parts[1].Trim();
                    }
                }
            }

            Hashtable targetOutputs = new();

            BuildSucceeded = BuildEngine.BuildProjectFile(ProjectFile, targetNames, globalProperties, targetOutputs);

            // Collect all targets' outputs as ITaskItem[] if available.
            if (BuildSucceeded && targetOutputs.Count > 0)
            {
                var items = new List<ITaskItem>();
                foreach (DictionaryEntry entry in targetOutputs)
                {
                    if (entry.Value is ITaskItem[] taskItems)
                    {
                        items.AddRange(taskItems);
                    }
                }

                OutputItems = items.ToArray();
            }

            Log.LogMessage(MessageImportance.High, $"BuildProjectFile({ProjectFile}) = {BuildSucceeded}, OutputItems={OutputItems.Length}");
            return true;
        }
    }
}
