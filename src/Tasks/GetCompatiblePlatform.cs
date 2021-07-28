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
        /// Optional parameter that defines translations from current project platforms
        /// to what the ProjectReference should build as.
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

                string projectReferenceOptions = AssignedProjectsWithPlatform[i].GetMetadata("Platforms");

                if (string.IsNullOrEmpty(projectReferenceOptions))
                {
                    Log.LogWarningWithCodeFromResources("GetCompatiblePlatform.NoPlatformsListed", AssignedProjectsWithPlatform[i].ItemSpec);
                    continue;
                }

                // Pull platformlookuptable metadata from the referenced project. This allows custom
                // translations on a per-ProjectReference basis.
                Dictionary<string, string> projectReferenceLookupTable = ExtractLookupTable(AssignedProjectsWithPlatform[i].GetMetadata("PlatformLookupTable"));

                if (projectReferenceLookupTable != null)
                {
                    Log.LogMessage(MessageImportance.Low, $"Referenced Project's Translation Table: {string.Join(";", projectReferenceLookupTable.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
                }

                HashSet<string> projectReferencePlatforms = new HashSet<string>();
                foreach (string s in projectReferenceOptions.Split(MSBuildConstants.SemicolonChar, StringSplitOptions.RemoveEmptyEntries))
                {
                    projectReferencePlatforms.Add(s);
                }

                string buildProjectReferenceAs = string.Empty;

                // Prefer matching platforms
                if (projectReferencePlatforms.Contains(CurrentProjectPlatform))
                {
                    buildProjectReferenceAs = CurrentProjectPlatform;
                    Log.LogMessage(MessageImportance.Low, $"ProjectReference and current project have the same platform.");
                }
                // Prioritize PlatformLookupTable **metadata** attached to the ProjectReference item
                // before the current project's table. We do this to allow per-ProjectReference fine tuning.
                else if (projectReferenceLookupTable != null &&
                        projectReferenceLookupTable.ContainsKey(CurrentProjectPlatform) &&
                        projectReferencePlatforms.Contains(projectReferenceLookupTable[CurrentProjectPlatform]))
                {
                    buildProjectReferenceAs = projectReferenceLookupTable[CurrentProjectPlatform];
                    Log.LogMessage(MessageImportance.Low, $"Found '{CurrentProjectPlatform}={buildProjectReferenceAs}' in the referenced project's translation table.");
                }
                // Current project's translation table follows
                else if (translationTable != null &&
                        translationTable.ContainsKey(CurrentProjectPlatform) &&
                        projectReferencePlatforms.Contains(translationTable[CurrentProjectPlatform]))
                {
                    buildProjectReferenceAs = translationTable[CurrentProjectPlatform];
                    Log.LogMessage(MessageImportance.Low, $"Found '{CurrentProjectPlatform}={buildProjectReferenceAs}' in the current project's translation table.");
                }
                // AnyCPU if possible
                else if (projectReferencePlatforms.Contains("AnyCPU"))
                {
                    buildProjectReferenceAs = "AnyCPU";
                    Log.LogMessage(MessageImportance.Low, $"Defaulting to AnyCPU.");
                }
                else
                {
                    // Keep NearestPlatform empty, log a warning. Common.CurrentVersion.targets will undefine 
                    // Platform/PlatformTarget when this is the case.
                    Log.LogWarningWithCodeFromResources("GetCompatiblePlatform.NoCompatiblePlatformFound", AssignedProjectsWithPlatform[i].ItemSpec);
                }

                AssignedProjectsWithPlatform[i].SetMetadata("NearestPlatform", buildProjectReferenceAs);
                Log.LogMessage(MessageImportance.Low, $"Project '{AssignedProjectsWithPlatform[i].ItemSpec}' will build with Platform: '{buildProjectReferenceAs}'");
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
