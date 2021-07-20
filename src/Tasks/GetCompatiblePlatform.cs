// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
        /// The platform the parent is building as. 
        /// </summary>
        public string ParentProjectPlatform { get; set; }

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
            Dictionary<string, string> translationTable = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(PlatformLookupTable))
            {
                foreach (string s in PlatformLookupTable.Split(MSBuildConstants.SemicolonChar, StringSplitOptions.RemoveEmptyEntries))
                {
                    string[] keyVal = s.Split(MSBuildConstants.EqualsChar, StringSplitOptions.RemoveEmptyEntries);

                    ErrorUtilities.VerifyThrow(keyVal.Length > 1, "PlatformLookupTable must be of the form A=B;C=D");

                    translationTable[keyVal[0]] = keyVal[1];
                }
                
                Log.LogMessage($"Translation Table: {string.Join(";", translationTable.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
            }

            AssignedProjectsWithPlatform = new ITaskItem[AnnotatedProjects.Length];
            for (int i = 0; i < AnnotatedProjects.Length; i++)
            {
                AssignedProjectsWithPlatform[i] = new TaskItem(AnnotatedProjects[i]);

                if (string.IsNullOrEmpty(AssignedProjectsWithPlatform[i].GetMetadata("PlatformOptions")))
                {
                    Log.LogWarningWithCodeFromResources("GetCompatiblePlatform.NoPlatformsListed", AssignedProjectsWithPlatform[i].ItemSpec);
                    continue;
                }

                HashSet<string> childPlatforms = new HashSet<string>();
                foreach (string s in AssignedProjectsWithPlatform[i].GetMetadata("PlatformOptions").Split(MSBuildConstants.SemicolonChar, StringSplitOptions.RemoveEmptyEntries))
                {
                    childPlatforms.Add(s);
                }

                string buildChildProjectAs = "";

                // Translation table takes priority
                if (translationTable.ContainsKey(ParentProjectPlatform) &&
                          childPlatforms.Contains(translationTable[ParentProjectPlatform]))
                {
                    buildChildProjectAs = translationTable[ParentProjectPlatform];
                    Log.LogMessage($"Found '{ParentProjectPlatform}={buildChildProjectAs}' in the given translation table.");
                }
                // AnyCPU if possible
                else if (childPlatforms.Contains("AnyCPU"))
                {
                    buildChildProjectAs = "AnyCPU";
                    Log.LogMessage($"Defaulting to AnyCPU.");
                }
                // Prefer matching platforms
                else if (childPlatforms.Contains(ParentProjectPlatform))
                {
                    buildChildProjectAs = ParentProjectPlatform;
                    Log.LogMessage($"Child and parent have the same platform.");
                }
                else
                {
                    // Keep it empty, log a warning. Common.CurrentVersion.targets will undefine 
                    // Platform/PlatformTarget when this is the case.
                    Log.LogWarningWithCodeFromResources("GetCompatiblePlatform.NoCompatiblePlatformFound", AssignedProjectsWithPlatform[i].ItemSpec);
                }

                AssignedProjectsWithPlatform[i].SetMetadata("NearestPlatform", buildChildProjectAs);
                Log.LogMessage($"Project '{AssignedProjectsWithPlatform[i].ItemSpec}' will build with Platform: '{buildChildProjectAs}'");
            }

            return !Log.HasLoggedErrors;
        }
    }
}
