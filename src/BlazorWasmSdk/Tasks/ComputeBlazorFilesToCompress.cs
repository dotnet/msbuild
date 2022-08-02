// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Sdk.BlazorWebAssembly
{
    // During the blazor build process some assets might not be at their final location by the time we try to compress them.
    // For that reason we need to determine the path to use to compress the file, which is what this task deals with.
    // We first check on the OriginalItemSpec of the asset and use that if the asset exists there.
    // In case it does not, we rely use the ItemSpec, which in case OriginalItemSpec does not exist, should point to an existing file on disk.
    // If neither the ItemSpec nor the OriginalItemSpec exist, we issue an error, since it indicates that the asset is not correctly
    // defined.
    // We can't just use the ItemSpec because for some assets that points to the output folder and causes issues with incrementalism.
    public class ComputeBlazorFilesToCompress : Task
    {
        [Required] public ITaskItem[] Assets { get; set; }

        [Output] public ITaskItem[] AssetsToCompress { get; set; }

        public override bool Execute()
        {
            var result = new List<ITaskItem>();

            for (var i = 0; i < Assets.Length; i++)
            {
                var asset = Assets[i];
                var originalItemSpec = asset.GetMetadata("OriginalItemSpec");
                if (File.Exists(originalItemSpec))
                {
                    Log.LogMessage(MessageImportance.Low, "Asset '{0}' found at OriginalItemSpec '{1}' and will be used for compressing the asset",
                        asset.ItemSpec,
                        originalItemSpec);

                    result.Add(CreateGzipAsset(asset, originalItemSpec));
                }
                else if (File.Exists(asset.ItemSpec))
                {
                    Log.LogMessage(MessageImportance.Low, "Asset '{0}' found at '{1}' and will be used for compressing the asset",
                        asset.ItemSpec,
                        asset.ItemSpec);

                    result.Add(CreateGzipAsset(asset, asset.ItemSpec));
                }
                else
                {
                    Log.LogError("The asset '{0}' can not be found at any of the searched locations '{1}' and '{2}'",
                        asset.ItemSpec,
                        asset.ItemSpec,
                        originalItemSpec);
                    break;
                }
            }

            AssetsToCompress = result.ToArray();

            return !Log.HasLoggedErrors;

            static TaskItem CreateGzipAsset(ITaskItem asset, string gzipSpec)
            {
                var result = new TaskItem(gzipSpec, asset.CloneCustomMetadata());

                result.SetMetadata("RelatedAsset", asset.ItemSpec);
                result.SetMetadata("AssetRole", "Alternative");
                result.SetMetadata("AssetTraitName", "Content-Encoding");
                result.SetMetadata("AssetTraitValue", "gzip");

                return result;
            }
        }
    }
}
