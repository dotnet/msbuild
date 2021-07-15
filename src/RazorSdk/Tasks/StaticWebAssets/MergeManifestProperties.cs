// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.Razor.Tasks
{
    public class MergeManifestProperties : Task
    {
        [Required]
        public ITaskItem[] CandidateManifests { get; set; }

        [Required]
        public ITaskItem[] ProjectReferences { get; set; }

        [Output]
        public ITaskItem[] Manifests { get; set; }

        public override bool Execute()
        {
            try
            {
                Manifests = new TaskItem[CandidateManifests.Length];

                for (var i = 0; i < CandidateManifests.Length; i++)
                {
                    var manifest = CandidateManifests[i];
                    var foundProjectReference = FindMatchingProject(manifest);
                    if (foundProjectReference == null)
                    {
                        Log.LogError(
                            "Unable to find a project reference for manifest item '{0}' with project file '{1}'",
                            manifest.ItemSpec,
                            manifest.GetMetadata("ProjectFile"));

                        return false;
                    }

                    var entry = new TaskItem(manifest.ItemSpec, new Dictionary<string, string>
                    {
                        [nameof(StaticWebAssetsManifest.ManifestReference.Source)] = manifest.GetMetadata(nameof(StaticWebAssetsManifest.ManifestReference.Source)),
                        [nameof(StaticWebAssetsManifest.ManifestReference.ManifestType)] = manifest.GetMetadata(nameof(StaticWebAssetsManifest.ManifestReference.ManifestType)),
                        [nameof(StaticWebAssetsManifest.ManifestReference.ProjectFile)] = manifest.GetMetadata(nameof(StaticWebAssetsManifest.ManifestReference.ProjectFile)),
                        [nameof(StaticWebAssetsManifest.ManifestReference.PublishTarget)] = manifest.GetMetadata(nameof(StaticWebAssetsManifest.ManifestReference.PublishTarget)),
                    });

                    var additionalPublishProperties = string.Join(
                        ";", manifest.GetMetadata("AdditionalPublishProperties"),
                        foundProjectReference.GetMetadata("SetConfiguration"),
                        foundProjectReference.GetMetadata("SetPlatform"),
                        foundProjectReference.GetMetadata("SetTargetFramework"));

                    additionalPublishProperties = Regex.Replace(additionalPublishProperties, ";{2,}", ";");

                    entry.SetMetadata("AdditionalPublishProperties", additionalPublishProperties);

                    var propertiesToRemove = string.Join(
                        ";", manifest.GetMetadata("AdditionalPublishPropertiesToRemove"),
                        foundProjectReference.GetMetadata("GlobalPropertiesToRemove"));

                    propertiesToRemove = Regex.Replace(propertiesToRemove, ";{2,}", ";");

                    entry.SetMetadata(
                        "AdditionalPublishPropertiesToRemove",
                        propertiesToRemove);

                    Manifests[i] = entry;
                }
            }
            catch (Exception ex)
            {
                Log.LogError(ex.ToString());
            }

            return !Log.HasLoggedErrors;
        }

        private ITaskItem FindMatchingProject(ITaskItem manifest)
        {
            for (var j = 0; j < ProjectReferences.Length; j++)
            {
                var projectReference = ProjectReferences[j];
                var matchPath = string.Equals(
                    Path.GetFullPath(manifest.GetMetadata("ProjectFile")),
                    Path.GetFullPath(projectReference.GetMetadata("MSBuildSourceProjectFile")),
                    StringComparison.Ordinal);

                if (matchPath)
                {
                    return projectReference;
                }
            }

            return null;
        }
    }
}
