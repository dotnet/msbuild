// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Evaluation;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class contains only static methods, which are used in both the 
    /// tasks and graph projects in order for two projects to negotiate which platform a projectreference
    /// should be built as.
    /// </summary>
    static internal class PlatformNegotiation
    {

        internal static string? GetNearestPlatform(String projectReferencePlatformMetadata, String projectReferenceLookupTableMetadata, String currentProjectPlatformMetadata, String currentPlatformLookupTableMetadata, String projectPath, TaskLoggingHelper? Log = null)
        {

            Dictionary<string, string>? currentProjectLookupTable = ExtractLookupTable(currentPlatformLookupTableMetadata, Log);

            if (string.IsNullOrEmpty(projectReferencePlatformMetadata))
            {
                Log?.LogWarningWithCodeFromResources("GetCompatiblePlatform.NoPlatformsListed", projectPath);
                return null;
            }
            Dictionary<string, string>? projectReferenceLookupTable = ExtractLookupTable(projectReferenceLookupTableMetadata, Log);
            HashSet<string> projectReferencePlatforms = new HashSet<string>();
            foreach (string s in projectReferencePlatformMetadata.Split(MSBuildConstants.SemicolonChar, StringSplitOptions.RemoveEmptyEntries))
            {
                projectReferencePlatforms.Add(s);
            }

            string buildProjectReferenceAs = string.Empty;

            if (projectReferencePlatforms.Contains(currentProjectPlatformMetadata))
            {
                buildProjectReferenceAs = currentProjectPlatformMetadata;
                Log?.LogMessageFromResources(MessageImportance.Low, "GetCompatiblePlatform.SamePlatform");
            }
            // Prioritize PlatformLookupTable **metadata** attached to the ProjectReference item
            // before the current project's table. We do this to allow per-ProjectReference fine tuning.
            else if (projectReferenceLookupTable != null &&
                    projectReferenceLookupTable.TryGetValue(currentProjectPlatformMetadata, out var projectreference) &&
                    projectReferencePlatforms.Contains(projectreference))
            {
                buildProjectReferenceAs = projectReferenceLookupTable[currentProjectPlatformMetadata];
                Log?.LogMessageFromResources(MessageImportance.Low, "GetCompatiblePlatform.FoundMappingInTable", currentProjectPlatformMetadata, buildProjectReferenceAs, projectReferenceLookupTableMetadata); 
            }
            // Current project's translation table follows
            else if (currentProjectLookupTable != null &&
                    currentProjectLookupTable.ContainsKey(currentProjectPlatformMetadata) &&
                    projectReferencePlatforms.Contains(currentProjectLookupTable[currentProjectPlatformMetadata]))
            {
                buildProjectReferenceAs = currentProjectLookupTable[currentProjectPlatformMetadata];
                Log?.LogMessageFromResources(MessageImportance.Low, "GetCompatiblePlatform.FoundMappingInTable", currentProjectPlatformMetadata, buildProjectReferenceAs, currentPlatformLookupTableMetadata);
            }
            // AnyCPU if possible
            else if (projectReferencePlatforms.Contains("AnyCPU") && buildProjectReferenceAs.Equals(String.Empty))
            {
                buildProjectReferenceAs = "AnyCPU";
                Log?.LogMessageFromResources(MessageImportance.Low, "GetCompatiblePlatform.AnyCPUDefault");
            }
            else
            {
                // Keep NearestPlatform empty, log a warning. Common.CurrentVersion.targets will undefine 
                // Platform/PlatformTarget when this is the case.
                Log?.LogWarningWithCodeFromResources("GetCompatiblePlatform.NoCompatiblePlatformFound", projectPath);
            }

            return buildProjectReferenceAs;
        }


        internal static Dictionary<string, string>? ExtractLookupTable(string stringTable, TaskLoggingHelper? Log = null)
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
                    Log?.LogWarningWithCodeFromResources("GetCompatiblePlatform.InvalidLookupTableFormat", stringTable);
                    return null;
                }

                table[keyVal[0]] = keyVal[1];
            }

            Log?.LogMessageFromResources(MessageImportance.Low, "GetCompatiblePlatform.LookupTableParsed", stringTable);

            return table;
        }
    }
}
