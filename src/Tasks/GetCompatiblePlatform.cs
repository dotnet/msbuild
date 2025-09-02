// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

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
            AnnotatedProjects = Array.Empty<ITaskItem>();
            CurrentProjectPlatform = string.Empty;
            PlatformLookupTable = string.Empty;
        }

        public override bool Execute()
        {

            AssignedProjectsWithPlatform = new ITaskItem[AnnotatedProjects.Length];
            for (int i = 0; i < AnnotatedProjects.Length; i++)
            {
                AssignedProjectsWithPlatform[i] = new TaskItem(AnnotatedProjects[i]);

                string referencedProjectPlatform = AssignedProjectsWithPlatform[i].GetMetadata("Platform");
                string projectReferencePlatformsMetadata = AssignedProjectsWithPlatform[i].GetMetadata("Platforms");
                string projectReferenceLookupTableMetadata = AssignedProjectsWithPlatform[i].GetMetadata("PlatformLookupTable");
                string projectReferenceOverridePlatformNegotiationMetadata = AssignedProjectsWithPlatform[i].GetMetadata("OverridePlatformNegotiationValue");

                string? buildProjectReferenceAs = PlatformNegotiation.GetNearestPlatform(projectReferenceOverridePlatformNegotiationMetadata, referencedProjectPlatform, projectReferencePlatformsMetadata, projectReferenceLookupTableMetadata, PlatformLookupTable, AssignedProjectsWithPlatform[i].ItemSpec, CurrentProjectPlatform, Log);

                AssignedProjectsWithPlatform[i].SetMetadata("NearestPlatform", buildProjectReferenceAs);
                Log.LogMessageFromResources(MessageImportance.Low, "GetCompatiblePlatform.DisplayChosenPlatform", AssignedProjectsWithPlatform[i].ItemSpec, buildProjectReferenceAs);
            }

            return !Log.HasLoggedErrors;
        }
    }
}
