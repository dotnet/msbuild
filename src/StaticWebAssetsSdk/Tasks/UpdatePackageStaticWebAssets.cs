// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks
{
    public class UpdatePackageStaticWebAssets : Task
    {
        [Required]
        public ITaskItem[] Assets { get; set; }

        [Output]
        public ITaskItem[] UpdatedAssets { get; set; }

        [Output]
        public ITaskItem[] OriginalAssets { get; set; }

        public override bool Execute()
        {
            try
            {
                var originalAssets = new List<ITaskItem>();
                var updatedAssets = new List<ITaskItem>();
                for (var i = 0; i < Assets.Length; i++)
                {
                    var candidate = Assets[i];
                    if (!StaticWebAsset.SourceTypes.IsPackage(candidate.GetMetadata(nameof(StaticWebAsset.SourceType))))
                    {
                        continue;
                    }

                    originalAssets.Add(candidate);
                    updatedAssets.Add(StaticWebAsset.FromV1TaskItem(candidate).ToTaskItem());
                }

                OriginalAssets = originalAssets.ToArray();
                UpdatedAssets = updatedAssets.ToArray();
            }
            catch (Exception ex)
            {
                Log.LogError(ex.ToString());
            }

            return !Log.HasLoggedErrors;
        }
    }
}
