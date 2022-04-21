using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class contains only static methods, which are used in both the 
    /// tasks and graph projects in order for two projects to negotiate which platform a projectreference
    /// should be built as.
    /// </summary>
    static internal class PlatformNegotiation
    {
        internal static string? GetNearestPlatform(string referencedProjectPlatform, string projectReferencePlatformsMetadata, string projectReferenceLookupTableMetadata, String platformLookupTable, String projectPath, String CurrentProjectPlatform, TaskLoggingHelper? log = null)
        {
            Dictionary<string, string>? currentProjectLookupTable = ExtractLookupTable(platformLookupTable, log);

            if (string.IsNullOrEmpty(projectReferencePlatformsMetadata) && string.IsNullOrEmpty(referencedProjectPlatform))
                {
                    log?.LogWarningWithCodeFromResources("GetCompatiblePlatform.NoPlatformsListed", projectPath);
                    return null;
                }

                // Pull platformLookupTable metadata from the referenced project. This allows custom
                // mappings on a per-ProjectReference basis.
                Dictionary<string, string>? projectReferenceLookupTable = ExtractLookupTable(projectReferenceLookupTableMetadata, log);

                HashSet<string> projectReferencePlatforms = new HashSet<string>();
                foreach (string s in projectReferencePlatformsMetadata.Split(MSBuildConstants.SemicolonChar, StringSplitOptions.RemoveEmptyEntries))
                {
                    projectReferencePlatforms.Add(s);
                }

                string buildProjectReferenceAs = string.Empty;

                // If the referenced project has a defined `Platform` that's compatible, it will build that way by default.
                // Don't set `buildProjectReferenceAs` and the `_GetProjectReferencePlatformProperties` target will handle the rest.
                if (!string.IsNullOrEmpty(referencedProjectPlatform) && referencedProjectPlatform.Equals(CurrentProjectPlatform, StringComparison.OrdinalIgnoreCase))
                {
                    log?.LogMessageFromResources(MessageImportance.Low, "GetCompatiblePlatform.ReferencedProjectHasDefinitivePlatform", projectPath, referencedProjectPlatform);
                }
                // Prefer matching platforms
                else if (projectReferencePlatforms.Contains(CurrentProjectPlatform))
                {
                    buildProjectReferenceAs = CurrentProjectPlatform;
                    log?.LogMessageFromResources(MessageImportance.Low, "GetCompatiblePlatform.SamePlatform");
                }
                // Prioritize platformLookupTable **metadata** attached to the ProjectReference item
                // before the current project's table. We do this to allow per-ProjectReference fine tuning.
                else if (projectReferenceLookupTable != null &&
                        projectReferenceLookupTable.ContainsKey(CurrentProjectPlatform) &&
                        projectReferencePlatforms.Contains(projectReferenceLookupTable[CurrentProjectPlatform]))
                {
                    buildProjectReferenceAs = projectReferenceLookupTable[CurrentProjectPlatform];
                    log?.LogMessageFromResources(MessageImportance.Low, "GetCompatiblePlatform.FoundMappingInTable", CurrentProjectPlatform, buildProjectReferenceAs, projectReferenceLookupTableMetadata);
                }
                // Current project's translation table follows
                else if (currentProjectLookupTable != null &&
                        currentProjectLookupTable.ContainsKey(CurrentProjectPlatform) &&
                        projectReferencePlatforms.Contains(currentProjectLookupTable[CurrentProjectPlatform]))
                {
                    buildProjectReferenceAs = currentProjectLookupTable[CurrentProjectPlatform];
                    log?.LogMessageFromResources(MessageImportance.Low, "GetCompatiblePlatform.FoundMappingInTable", CurrentProjectPlatform, buildProjectReferenceAs, platformLookupTable);
                }
                // AnyCPU if possible
                else if (projectReferencePlatforms.Contains("AnyCPU"))
                {
                    buildProjectReferenceAs = "AnyCPU";
                    log?.LogMessageFromResources(MessageImportance.Low, "GetCompatiblePlatform.AnyCPUDefault");
                }
                else
                {
                    // Keep NearestPlatform empty, log a warning. Common.CurrentVersion.targets will undefine 
                    // Platform/PlatformTarget when this is the case.
                    log?.LogWarningWithCodeFromResources("GetCompatiblePlatform.NoCompatiblePlatformFound", projectPath);
                }
            return buildProjectReferenceAs;
        }
        internal static Dictionary<string, string>? ExtractLookupTable(string stringTable, TaskLoggingHelper? log = null)
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
                    log?.LogWarningWithCodeFromResources("GetCompatiblePlatform.InvalidLookupTableFormat", stringTable);
                    return null;
                }

                table[keyVal[0]] = keyVal[1];
            }

            log?.LogMessageFromResources(MessageImportance.Low, "GetCompatiblePlatform.LookupTableParsed", stringTable);

            return table;
        }
    }
}
