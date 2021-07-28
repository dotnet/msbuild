// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Performs SetPlatform negotiation for all project references when opted
    /// in via the EnableDynamicPlatformResolution property.
    /// 
    /// See ProjectReference-Protocol.md for details.
    /// </summary>
    public class GetCompatiblePlatform : TaskExtension
    {
        /// <summary>
        /// All ProjectReference items.
        /// </summary>
        public ITaskItem[] AnnotatedProjects { get; set; }

        /// <summary>
        /// The platform the current project is building as. 
        /// </summary>
        public string CurrentProjectPlatform { get; set; }

        /// <summary>
        /// Optional parameter that defines translations from parent platforms to
        /// what the ProjectReference should build as.
        /// Win32=x86, for example.
        /// </summary>
        public string PlatformLookupTable { get; set; }

        /// <summary>
        /// The resulting items with NearestPlatform metadata set.
        /// </summary>
        [Output]
        public ITaskItem[] AssignedProjectsWithPlatform { get; set; }

        public override bool Execute()
        {
            Dictionary<string, string> translationTable = ExtractLookupTable(PlatformLookupTable);

            if (translationTable != null)
            {
                Log.LogMessage(MessageImportance.Low, $"Current Project's Translation Table: {string.Join(";", translationTable.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
            }

            AssignedProjectsWithPlatform = new ITaskItem[AnnotatedProjects.Length];
            for (int i = 0; i < AnnotatedProjects.Length; i++)
            {
                AssignedProjectsWithPlatform[i] = new TaskItem(AnnotatedProjects[i]);

                string childPlatformOptions = AssignedProjectsWithPlatform[i].GetMetadata("Platforms");

                if (string.IsNullOrEmpty(childPlatformOptions))
                {
                    Log.LogWarningWithCodeFromResources("GetCompatiblePlatform.NoPlatformsListed", AssignedProjectsWithPlatform[i].ItemSpec);
                    continue;
                }

                // Pull platformlookuptable metadata from the referenced project. This allows custom
                // translations on a per-ProjectReference basis.
                Dictionary<string, string> childPlatformLookupTable = ExtractLookupTable(AssignedProjectsWithPlatform[i].GetMetadata("PlatformLookupTable"));

                if (childPlatformLookupTable != null)
                {
                    Log.LogMessage(MessageImportance.Low, $"Referenced Project's Translation Table: {string.Join(";", childPlatformLookupTable.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
                }

                HashSet<string> childPlatforms = new HashSet<string>();
                foreach (string s in childPlatformOptions.Split(MSBuildConstants.SemicolonChar, StringSplitOptions.RemoveEmptyEntries))
                {
                    childPlatforms.Add(s);
                }

                string buildChildProjectAs = string.Empty;

                // Prefer matching platforms
                if (childPlatforms.Contains(CurrentProjectPlatform))
                {
                    buildChildProjectAs = CurrentProjectPlatform;
                    Log.LogMessage(MessageImportance.Low, $"Child and parent have the same platform.");
                }
                // Prioritize PlatformLookupTable **metadata** attached to the ProjectReference item
                // before the current project's table. We do this to allow per-ProjectReference fine tuning.
                else if (childPlatformLookupTable != null &&
                        childPlatformLookupTable.ContainsKey(CurrentProjectPlatform) &&
                        childPlatforms.Contains(childPlatformLookupTable[CurrentProjectPlatform]))
                {
                    buildChildProjectAs = childPlatformLookupTable[CurrentProjectPlatform];
                    Log.LogMessage(MessageImportance.Low, $"Found '{CurrentProjectPlatform}={buildChildProjectAs}' in the referenced project's translation table.");
                }
                // Current project's translation table follows
                else if (translationTable != null &&
                        translationTable.ContainsKey(CurrentProjectPlatform) &&
                        childPlatforms.Contains(translationTable[CurrentProjectPlatform]))
                {
                    buildChildProjectAs = translationTable[CurrentProjectPlatform];
                    Log.LogMessage(MessageImportance.Low, $"Found '{CurrentProjectPlatform}={buildChildProjectAs}' in the current project's translation table.");
                }
                // AnyCPU if possible
                else if (childPlatforms.Contains("AnyCPU"))
                {
                    buildChildProjectAs = "AnyCPU";
                    Log.LogMessage(MessageImportance.Low, $"Defaulting to AnyCPU.");
                }
                else
                {
                    // Keep NearestPlatform empty, log a warning. Common.CurrentVersion.targets will undefine 
                    // Platform/PlatformTarget when this is the case.
                    Log.LogWarningWithCodeFromResources("GetCompatiblePlatform.NoCompatiblePlatformFound", AssignedProjectsWithPlatform[i].ItemSpec);
                }

                AssignedProjectsWithPlatform[i].SetMetadata("NearestPlatform", buildChildProjectAs);
                Log.LogMessage(MessageImportance.Low, $"Project '{AssignedProjectsWithPlatform[i].ItemSpec}' will build with Platform: '{buildChildProjectAs}'");
            }

            return !Log.HasLoggedErrors;
        }

        private Dictionary<string, string> ExtractLookupTable(string stringTable)
        {
            if (string.IsNullOrEmpty(stringTable))
            {
                return null;
            }

            Dictionary<string, string> table = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string s in stringTable.Split(MSBuildConstants.SemicolonChar, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] keyVal = s.Split(MSBuildConstants.EqualsChar, StringSplitOptions.RemoveEmptyEntries);

                // Invalid table, don't use it.
                if (keyVal.Length <= 1 || keyVal.Length > 2)
                {
                    Log.LogWarningWithCodeFromResources("GetCompatiblePlatform.InvalidLookupTableFormat", stringTable);
                    return null;
                }

                table[keyVal[0]] = keyVal[1];
            }

            return table;
        }
    }
}
