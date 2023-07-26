// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks
{
    public class MergeConfigurationProperties : Task
    {
        [Required]
        public ITaskItem[] CandidateConfigurations { get; set; }

        [Required]
        public ITaskItem[] ProjectReferences { get; set; }

        [Output]
        public ITaskItem[] ProjectConfigurations { get; set; }

        public override bool Execute()
        {
            try
            {
                ProjectConfigurations = new TaskItem[CandidateConfigurations.Length];

                for (var i = 0; i < CandidateConfigurations.Length; i++)
                {
                    var configuration = CandidateConfigurations[i];
                    var foundProjectReference = FindMatchingProject(configuration);
                    if (foundProjectReference == null)
                    {
                        Log.LogError(
                            "Unable to find a project reference for project configuration item '{0}'",
                            configuration.ItemSpec);

                        return false;
                    }

                    var entry = new TaskItem(configuration.ItemSpec, new Dictionary<string, string>
                    {
                        ["Version"] = configuration.GetMetadata(nameof(StaticWebAssetsManifest.ReferencedProjectConfiguration.Version)),
                        ["Source"] = configuration.GetMetadata(nameof(StaticWebAssetsManifest.ReferencedProjectConfiguration.Source)),
                        ["GetBuildAssetsTargets"] = configuration.GetMetadata(nameof(StaticWebAssetsManifest.ReferencedProjectConfiguration.GetBuildAssetsTargets)),
                        ["AdditionalBuildProperties"] = configuration.GetMetadata(nameof(StaticWebAssetsManifest.ReferencedProjectConfiguration.AdditionalBuildProperties)),
                        ["AdditionalBuildPropertiesToRemove"] = configuration.GetMetadata(nameof(StaticWebAssetsManifest.ReferencedProjectConfiguration.AdditionalBuildPropertiesToRemove)),
                        ["GetPublishAssetsTargets"] = configuration.GetMetadata(nameof(StaticWebAssetsManifest.ReferencedProjectConfiguration.GetPublishAssetsTargets)),
                        ["AdditionalPublishProperties"] = configuration.GetMetadata(nameof(StaticWebAssetsManifest.ReferencedProjectConfiguration.AdditionalPublishProperties)),
                        ["AdditionalPublishPropertiesToRemove"] = configuration.GetMetadata(nameof(StaticWebAssetsManifest.ReferencedProjectConfiguration.AdditionalPublishPropertiesToRemove)),
                    });

                    var additionalBuildProperties = string.Join(
                        ";", configuration.GetMetadata("AdditionalBuildProperties"),
                        foundProjectReference.GetMetadata("SetConfiguration"),
                        foundProjectReference.GetMetadata("SetPlatform"),
                        foundProjectReference.GetMetadata("SetTargetFramework"));

                    CleanupMetadata(entry, "AdditionalBuildProperties", additionalBuildProperties);

                    var buildPropertiesToRemove = string.Join(
                        ";", configuration.GetMetadata("AdditionalBuildPropertiesToRemove"),
                        foundProjectReference.GetMetadata("GlobalPropertiesToRemove"),
                        foundProjectReference.GetMetadata("UndefineProperties"));

                    CleanupMetadata(entry, "AdditionalBuildPropertiesToRemove", buildPropertiesToRemove);

                    var additionalPublishProperties = string.Join(
                        ";", configuration.GetMetadata("AdditionalPublishProperties"),
                        foundProjectReference.GetMetadata("SetConfiguration"),
                        foundProjectReference.GetMetadata("SetPlatform"),
                        foundProjectReference.GetMetadata("SetTargetFramework"));

                    CleanupMetadata(entry, "AdditionalPublishProperties", additionalPublishProperties);

                    var publishPropertiesToRemove = string.Join(
                        ";", configuration.GetMetadata("AdditionalPublishPropertiesToRemove"),
                        foundProjectReference.GetMetadata("GlobalPropertiesToRemove"),
                        foundProjectReference.GetMetadata("UndefineProperties"));

                    CleanupMetadata(entry, "AdditionalPublishPropertiesToRemove", publishPropertiesToRemove);

                    ProjectConfigurations[i] = entry;
                }
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex, showStackTrace: true, showDetail: true, file: null);
            }

            return !Log.HasLoggedErrors;

            static void CleanupMetadata(TaskItem entry, string metadataName, string metadataValue)
            {
                metadataValue = Regex.Replace(metadataValue, ";{2,}", ";");
                metadataValue = metadataValue.Trim(';');
                entry.SetMetadata(metadataName, metadataValue);
            }
        }

        private ITaskItem FindMatchingProject(ITaskItem configuration)
        {
            for (var j = 0; j < ProjectReferences.Length; j++)
            {
                var projectReference = ProjectReferences[j];
                var referenceMetadata = projectReference.GetMetadata("MSBuildSourceProjectFile");
                // All project references should define MSBuildSourceProjectFile but in the ASP.NET Core some special (malformed) references do not.
                // We can be more lenient here and fallback to the project reference ItemSpec if not present.
                referenceMetadata = !string.IsNullOrEmpty(referenceMetadata) ? referenceMetadata : projectReference.ItemSpec;
                var configurationFullPath = configuration.GetMetadata("FullPath");
                var projectReferenceFullPath = Path.GetFullPath(referenceMetadata);
                var matchPath = string.Equals(
                    configurationFullPath,
                    projectReferenceFullPath,
                    OSPath.PathComparison);

                if (matchPath)
                {
                    Log.LogMessage(MessageImportance.Low, "Found project reference '{0}' for configuration item '{1}'.", configurationFullPath, projectReferenceFullPath);
                    return projectReference;
                }
                else
                {
                    Log.LogMessage(MessageImportance.Low, "Rejected project reference '{0}' for configuration item '{1}' because paths don't match.", configurationFullPath, projectReferenceFullPath);
                }
            }

            return null;
        }
    }
}
