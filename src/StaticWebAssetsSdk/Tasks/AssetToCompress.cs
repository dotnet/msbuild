// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

internal static class AssetToCompress
{
    public static bool TryFindInputFilePath(ITaskItem assetToCompress, TaskLoggingHelper log, out string fullPath)
    {
        var relatedAssetOriginalItemSpec = assetToCompress.GetMetadata("RelatedAssetOriginalItemSpec");
        if (File.Exists(relatedAssetOriginalItemSpec))
        {
            log.LogMessage(MessageImportance.Low, "Asset '{0}' found at original item spec '{1}'.",
                assetToCompress.ItemSpec,
                relatedAssetOriginalItemSpec);
            fullPath = relatedAssetOriginalItemSpec;
            return true;
        }

        var relatedAsset = assetToCompress.GetMetadata("RelatedAsset");
        if (File.Exists(relatedAsset))
        {
            log.LogMessage(MessageImportance.Low, "Asset '{0}' found at path '{1}'.",
                assetToCompress.ItemSpec,
                relatedAsset);
            fullPath = relatedAsset;
            return true;
        }

        log.LogError("The asset '{0}' can not be found at any of the searched locations '{1}' and '{2}'.",
            assetToCompress.ItemSpec,
            relatedAssetOriginalItemSpec,
            relatedAsset);
        fullPath = null;
        return false;
    }
}
