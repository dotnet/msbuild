// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class contains only static methods, which are used in both the
    /// tasks and graph projects in order for two projects to negotiate which platform a projectreference
    /// should be built as.
    /// </summary>
    internal static class PlatformNegotiation
    {
        internal static string GetNearestPlatform(
            string overridePlatformValue,
            string referencedProjectPlatform,
            string projectReferencePlatformsMetadata,
            string projectReferenceLookupTableMetadata,
            string platformLookupTable,
            string projectPath,
            string currentProjectPlatform)
        {
            return GetNearestPlatform(
                overridePlatformValue,
                referencedProjectPlatform,
                projectReferencePlatformsMetadata,
                projectReferenceLookupTableMetadata,
                platformLookupTable,
                projectPath,
                currentProjectPlatform,
                default(NullTaskLogger));
        }

        internal static string GetNearestPlatform<TLogger>(
            string overridePlatformValue,
            string referencedProjectPlatform,
            string projectReferencePlatformsMetadata,
            string projectReferenceLookupTableMetadata,
            string platformLookupTable,
            string projectPath,
            string currentProjectPlatform,
            TLogger log)
            where TLogger : ITaskLogger
        {
            Dictionary<string, string>? currentProjectLookupTable = ExtractLookupTable(platformLookupTable, log);

            if (string.IsNullOrEmpty(projectReferencePlatformsMetadata) && string.IsNullOrEmpty(referencedProjectPlatform))
            {
                if (log.IsEnabled)
                {
                    log.LogWarningWithCodeFromResources("GetCompatiblePlatform.NoPlatformsListed", projectPath);
                }
                return string.Empty;
            }

            // Pull platformLookupTable metadata from the referenced project. This allows custom
            // mappings on a per-ProjectReference basis.
            Dictionary<string, string>? projectReferenceLookupTable = ExtractLookupTable(projectReferenceLookupTableMetadata, log);

            HashSet<string> projectReferencePlatforms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string s in projectReferencePlatformsMetadata.Split(MSBuildConstants.SemicolonChar, StringSplitOptions.RemoveEmptyEntries))
            {
                projectReferencePlatforms.Add(s);
            }

            string buildProjectReferenceAs = string.Empty;

            // If an override value is set define that as the platform value as the top priority
            if (!string.IsNullOrEmpty(overridePlatformValue))
            {
                buildProjectReferenceAs = overridePlatformValue;
            }
            // If the referenced project has a defined `Platform` that's compatible, it will build that way by default.
            // Don't set `buildProjectReferenceAs` and the `_GetProjectReferencePlatformProperties` target will handle the rest.
            else if (!string.IsNullOrEmpty(referencedProjectPlatform) && referencedProjectPlatform.Equals(currentProjectPlatform, StringComparison.OrdinalIgnoreCase))
            {
                if (log.IsEnabled)
                {
                    log.LogMessageFromResources(MessageImportance.Low, "GetCompatiblePlatform.ReferencedProjectHasDefinitivePlatform", projectPath, referencedProjectPlatform);
                }
            }
            // Prefer matching platforms
            else if (projectReferencePlatforms.Contains(currentProjectPlatform))
            {
                buildProjectReferenceAs = currentProjectPlatform;
                if (log.IsEnabled)
                {
                    log.LogMessageFromResources(MessageImportance.Low, "GetCompatiblePlatform.SamePlatform");
                }
            }
            // Prioritize platformLookupTable **metadata** attached to the ProjectReference item
            // before the current project's table. We do this to allow per-ProjectReference fine tuning.
            else if (projectReferenceLookupTable != null &&
                    projectReferenceLookupTable.TryGetValue(currentProjectPlatform, out string? value) &&
                    projectReferencePlatforms.Contains(value))
            {
                buildProjectReferenceAs = value;
                if (log.IsEnabled)
                {
                    log.LogMessageFromResources(MessageImportance.Low, "GetCompatiblePlatform.FoundMappingInTable", currentProjectPlatform, buildProjectReferenceAs, projectReferenceLookupTableMetadata);
                }
            }
            // Current project's translation table follows
            else if (currentProjectLookupTable != null &&
                    currentProjectLookupTable.TryGetValue(currentProjectPlatform, out value) &&
                    projectReferencePlatforms.Contains(value))
            {
                buildProjectReferenceAs = value;
                if (log.IsEnabled)
                {
                    log.LogMessageFromResources(MessageImportance.Low, "GetCompatiblePlatform.FoundMappingInTable", currentProjectPlatform, buildProjectReferenceAs, platformLookupTable);
                }
            }
            // AnyCPU if possible
            else if (projectReferencePlatforms.Contains("AnyCPU"))
            {
                buildProjectReferenceAs = "AnyCPU";
                if (log.IsEnabled)
                {
                    log.LogMessageFromResources(MessageImportance.Low, "GetCompatiblePlatform.AnyCPUDefault");
                }
            }
            else
            {
                // Keep NearestPlatform empty, log a warning. Common.CurrentVersion.targets will undefine
                // Platform/PlatformTarget when this is the case.
                if (log.IsEnabled)
                {
                    log.LogWarningWithCodeFromResources("GetCompatiblePlatform.NoCompatiblePlatformFound", projectPath);
                }
            }
            // If the referenced project has a defined `Platform` that's compatible, it will build that way by default.
            // If we're about to tell the reference to build using its default platform, don't pass it as a global property.
            if (!string.IsNullOrEmpty(referencedProjectPlatform) && referencedProjectPlatform.Equals(buildProjectReferenceAs, StringComparison.OrdinalIgnoreCase))
            {
                if (log.IsEnabled)
                {
                    log.LogMessageFromResources(MessageImportance.Low, "GetCompatiblePlatform.ReferencedProjectHasDefinitivePlatform", projectPath, referencedProjectPlatform);
                }
                buildProjectReferenceAs = string.Empty;
            }
            return buildProjectReferenceAs;
        }
        private static Dictionary<string, string>? ExtractLookupTable<TLogger>(string stringTable, TLogger log)
            where TLogger : ITaskLogger
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
                    if (log.IsEnabled)
                    {
                        log.LogWarningWithCodeFromResources("GetCompatiblePlatform.InvalidLookupTableFormat", stringTable);
                    }
                    return null;
                }

                table[keyVal[0]] = keyVal[1];
            }

            if (log.IsEnabled)
            {
                log.LogMessageFromResources(MessageImportance.Low, "GetCompatiblePlatform.LookupTableParsed", stringTable);
            }

            return table;
        }
    }
}
