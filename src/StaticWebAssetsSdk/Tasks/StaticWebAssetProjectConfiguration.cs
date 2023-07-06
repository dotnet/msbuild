// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks
{
    public class StaticWebAssetProjectConfiguration
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public string Source { get; set; }
        public string GetBuildAssetsTargets { get; set; }
        public string AdditionalBuildProperties { get; set; }
        public string AdditionalBuildPropertiesToRemove { get; set; }
        public string GetPublishAssetsTargets { get; set; }
        public string AdditionalPublishProperties { get; set; }
        public string AdditionalPublishPropertiesToRemove { get; set; }
        public string TargetFramework { get; set; }

        public static StaticWebAssetProjectConfiguration FromTaskItem(ITaskItem taskItem)
        {
            return new StaticWebAssetProjectConfiguration
            {
                Id = taskItem.ItemSpec,
                Version = taskItem.GetMetadata(nameof(Version)),
                Source = taskItem.GetMetadata(nameof(Source)),
                GetBuildAssetsTargets = taskItem.GetMetadata(nameof(GetBuildAssetsTargets)),
                AdditionalBuildProperties = taskItem.GetMetadata(nameof(AdditionalBuildProperties)),
                AdditionalBuildPropertiesToRemove = taskItem.GetMetadata(nameof(AdditionalBuildPropertiesToRemove)),
                GetPublishAssetsTargets = taskItem.GetMetadata(nameof(GetPublishAssetsTargets)),
                AdditionalPublishProperties = taskItem.GetMetadata(nameof(AdditionalPublishProperties)),
                AdditionalPublishPropertiesToRemove = taskItem.GetMetadata(nameof(AdditionalPublishPropertiesToRemove)),
                TargetFramework = taskItem.GetMetadata(nameof(TargetFramework))
            };
        }

        public ITaskItem2 ToTaskItem()
        {
            var result = new TaskItem(Id);
            result.SetMetadata(nameof(Version), Version);
            result.SetMetadata(nameof(Source), Source);
            result.SetMetadata(nameof(GetBuildAssetsTargets), GetBuildAssetsTargets);
            result.SetMetadata(nameof(AdditionalBuildProperties), AdditionalBuildProperties);
            result.SetMetadata(nameof(AdditionalBuildPropertiesToRemove), AdditionalBuildPropertiesToRemove);
            result.SetMetadata(nameof(GetPublishAssetsTargets), GetPublishAssetsTargets);
            result.SetMetadata(nameof(AdditionalPublishProperties), AdditionalPublishProperties);
            result.SetMetadata(nameof(AdditionalPublishPropertiesToRemove), AdditionalPublishPropertiesToRemove);
            result.SetMetadata(nameof(TargetFramework), TargetFramework);

            return result;
        }
    }
}

