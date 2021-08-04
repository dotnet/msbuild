// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;

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
        [Required]
        public ITaskItem[] AnnotatedProjects { get; set; }

        /// <summary>
        /// The platform the current project is building as. 
        /// </summary>
        [Required]
        public string CurrentProjectPlatform { get; set; }

        /// <summary>
        /// Optional parameter that defines mappings from current project platforms
        /// to what the ProjectReference should build as.
        /// Win32=x86, for example.
        /// </summary>
        public string PlatformLookupTable { get; set; }

        /// <summary>
        /// The resulting items with NearestPlatform metadata set.
        /// </summary>
        [Output]
        public ITaskItem[]? AssignedProjectsWithPlatform { get; set; }

        public GetCompatiblePlatform()
        {
            AnnotatedProjects = new ITaskItem[0];
            CurrentProjectPlatform = string.Empty;
            PlatformLookupTable = string.Empty;
        }

        public override bool Execute()
        {
            Dictionary<string, string>? currentProjectLookupTable = ExtractLookupTable(PlatformLookupTable);

            AssignedProjectsWithPlatform = new ITaskItem[AnnotatedProjects.Length];
            for (int i = 0; i < AnnotatedProjects.Length; i++)
            {
                AssignedProjectsWithPlatform[i] = new TaskItem(AnnotatedProjects[i]);

                string projectReferencePlatformMetadata = AssignedProjectsWithPlatform[i].GetMetadata("Platforms");

                if (string.IsNullOrEmpty(projectReferencePlatformMetadata))
                {
                    Log.LogWarningWithCodeFromResources("GetCompatiblePlatform.NoPlatformsListed", AssignedProjectsWithPlatform[i].ItemSpec);
                    continue;
                }

                string projectReferenceLookupTableMetadata = AssignedProjectsWithPlatform[i].GetMetadata("PlatformLookupTable");
                // Pull platformlookuptable metadata from the referenced project. This allows custom
                // mappings on a per-ProjectReference basis.
                Dictionary<string, string>? projectReferenceLookupTable = ExtractLookupTable(projectReferenceLookupTableMetadata);

                HashSet<string> projectReferencePlatforms = new HashSet<string>();
                foreach (string s in projectReferencePlatformMetadata.Split(MSBuildConstants.SemicolonChar, StringSplitOptions.RemoveEmptyEntries))
                {
                    projectReferencePlatforms.Add(s);
                }

                string buildProjectReferenceAs = string.Empty;

                // Prefer matching platforms
                if (projectReferencePlatforms.Contains(CurrentProjectPlatform))
                {
                    buildProjectReferenceAs = CurrentProjectPlatform;
                    Log.LogMessageFromResources(MessageImportance.Low, "GetCompatiblePlatform.SamePlatform");
                }
                // Prioritize PlatformLookupTable **metadata** attached to the ProjectReference item
                // before the current project's table. We do this to allow per-ProjectReference fine tuning.
                else if (projectReferenceLookupTable != null &&
                        projectReferenceLookupTable.ContainsKey(CurrentProjectPlatform) &&
                        projectReferencePlatforms.Contains(projectReferenceLookupTable[CurrentProjectPlatform]))
                {
                    buildProjectReferenceAs = projectReferenceLookupTable[CurrentProjectPlatform];
                    Log.LogMessageFromResources(MessageImportance.Low, "GetCompatiblePlatform.FoundMappingInTable", CurrentProjectPlatform, buildProjectReferenceAs, projectReferenceLookupTableMetadata);
                }
                // Current project's translation table follows
                else if (currentProjectLookupTable != null &&
                        currentProjectLookupTable.ContainsKey(CurrentProjectPlatform) &&
                        projectReferencePlatforms.Contains(currentProjectLookupTable[CurrentProjectPlatform]))
                {
                    buildProjectReferenceAs = currentProjectLookupTable[CurrentProjectPlatform];
                    Log.LogMessageFromResources(MessageImportance.Low, "GetCompatiblePlatform.FoundMappingInTable", CurrentProjectPlatform, buildProjectReferenceAs, PlatformLookupTable);
                }
                // AnyCPU if possible
                else if (projectReferencePlatforms.Contains("AnyCPU"))
                {
                    buildProjectReferenceAs = "AnyCPU";
                    Log.LogMessageFromResources(MessageImportance.Low, "GetCompatiblePlatform.AnyCPUDefault");
                }
                else
                {
                    // Keep NearestPlatform empty, log a warning. Common.CurrentVersion.targets will undefine 
                    // Platform/PlatformTarget when this is the case.
                    Log.LogWarningWithCodeFromResources("GetCompatiblePlatform.NoCompatiblePlatformFound", AssignedProjectsWithPlatform[i].ItemSpec);
                }

                AssignedProjectsWithPlatform[i].SetMetadata("NearestPlatform", buildProjectReferenceAs);
                Log.LogMessageFromResources(MessageImportance.Low, "GetCompatiblePlatform.DisplayChosenPlatform", AssignedProjectsWithPlatform[i].ItemSpec, buildProjectReferenceAs);
            }

            return !Log.HasLoggedErrors;
        }

        private Dictionary<string, string>? ExtractLookupTable(string stringTable)
        {
            if (string.IsNullOrEmpty(stringTable))
            {
                return null;
            }

            Dictionary<string, string> table = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string s in stringTable.Trim().Split(MSBuildConstants.SemicolonChar, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] keyVal = s.Trim().Split(MSBuildConstants.EqualsChar);

                // Invalid table, don't use it.
                if (keyVal.Length != 2 || string.IsNullOrEmpty(keyVal[0]) || string.IsNullOrEmpty(keyVal[1]))
                {
                    Log.LogWarningWithCodeFromResources("GetCompatiblePlatform.InvalidLookupTableFormat", stringTable);
                    return null;
                }

                table[keyVal[0]] = keyVal[1];
            }

            Log.LogMessageFromResources(MessageImportance.Low, "GetCompatiblePlatform.LookupTableParsed", stringTable);

            return table;
        }
    }
}
