// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.Razor.Tasks
{
    // In this task we receive the list of static web assets during publish which includes the assets read
    // from this projects build manifest as well as the assets read from the related projects publish manifests.
    // The current project build manifest might contain assets declared as "All" that have been updated with "publish only"
    // versions of those assets.
    // This allows projects to declare their expected output during build and optionally update during the publish process
    // with Static Web Assets taking care of choosing the right asset. The publish asset will be preferred over the "All" asset
    // defined during the build process. (Build only assets are directly filtered out)
    // We do this by computing the target path for the two assets and determining if they'll end up in the same location on disk.
    public class ComputePublishStaticWebAssetsList : Task
    {
        [Required]
        public ITaskItem[] Assets { get; set; }

        [Output]
        public ITaskItem[] PublishAssets { get; set; }

        public override bool Execute()
        {
            var publishAssets = new Dictionary<string, StaticWebAsset>();
            var assets = Assets
                // We do the where first to avoid allocations for things we are about to filter.
                .Where(a => !StaticWebAsset.AssetKinds.IsBuild(a.GetMetadata(nameof(StaticWebAsset.AssetKind))))
                .Select(StaticWebAsset.FromTaskItem)
                .ToArray();
            try
            {
                for (var i = 0; i < assets.Length; i++)
                {
                    var asset = assets[i];
                    var targetPath = asset.ComputeTargetPath("", Path.AltDirectorySeparatorChar);
                    if (publishAssets.TryGetValue(targetPath, out var existing))
                    {
                        if (!string.Equals(asset.SourceId, existing.SourceId, StringComparison.Ordinal))
                        {
                            Log.LogError("Detected incompatible set of assets '{0}' and '{1}' with different sources '{2}' and '{3}' respectively.",
                                existing.Identity,
                                asset.Identity,
                                existing.SourceId,
                                asset.SourceId);
                            break;
                        }
                        if (!AreKindsCompatible(asset, existing))
                        {
                            Log.LogError("Detected incompatible set of assets '{0}' and '{1}' with asset kinds '{2}' and '{3}' respectively.",
                                existing.Identity,
                                asset.Identity,
                                existing.AssetKind,
                                asset.AssetKind);
                            break;
                        }
                        else
                        {
                            if (existing.IsBuildAndPublish())
                            {
                                Log.LogMessage("Updating asset '{0}' for '{1}' publish specific asset at '{2}'",
                                    existing.Identity,
                                    asset.Identity,
                                    targetPath);
                                publishAssets[asset.Identity] = asset;
                            }
                            else
                            {
                                // Even though we found the publish asset first, we want to acknowledge the upgrade in the logs.
                                Log.LogMessage("Updating asset '{0}' for '{1}' publish specific asset at '{2}'",
                                    asset.Identity,
                                    existing.Identity,
                                    targetPath);
                            }
                        }
                    }
                    else
                    {
                        publishAssets[targetPath] = asset;
                    }
                }


            }
            catch (Exception ex)
            {
                Log.LogError(ex.ToString());
                Log.LogErrorFromException(ex);
            }

            PublishAssets = publishAssets.Select(a => a.Value.ToTaskItem()).ToArray();

            return !Log.HasLoggedErrors;

            static bool AreKindsCompatible(StaticWebAsset asset, StaticWebAsset existing) =>
                // We could have done this with asset.IsPublishOnly() ^ existing.IsPublishOnly(), but this way is more clear.
                (asset.AssetKind, existing.AssetKind) switch
                {
                    (StaticWebAsset.AssetKinds.All, StaticWebAsset.AssetKinds.Publish) => true,
                    (StaticWebAsset.AssetKinds.All, StaticWebAsset.AssetKinds.All) => true,
                    (StaticWebAsset.AssetKinds.Publish, StaticWebAsset.AssetKinds.Publish) => false,
                    (StaticWebAsset.AssetKinds.Publish, StaticWebAsset.AssetKinds.All) => true,
                    _ => throw new InvalidOperationException($"One or more kinds are not valid for '{existing.Identity}' and " +
                         $"'{asset.Identity}' with asset kinds '{existing.AssetKind}' and '{asset.AssetKind}'")
                };
        }
    }
}
