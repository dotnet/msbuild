// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    internal class RuntimePackAssetInfo
    {
        public string SourcePath { get; set; }

        public string DestinationSubPath { get; set; }

        public AssetType AssetType { get; set; }

        public string PackageName { get; set; }

        public string PackageVersion { get; set; }

        public string PackageRuntimeIdentifier { get; set; }

        public static RuntimePackAssetInfo FromItem(ITaskItem item)
        {
            var assetInfo = new RuntimePackAssetInfo
            {
                SourcePath = item.ItemSpec,
                DestinationSubPath = item.GetMetadata(MetadataKeys.DestinationSubPath)
            };

            string assetTypeString = item.GetMetadata(MetadataKeys.AssetType);
            if (assetTypeString.Equals("runtime", StringComparison.OrdinalIgnoreCase))
            {
                assetInfo.AssetType = AssetType.Runtime;
            }
            else if (assetTypeString.Equals("native", StringComparison.OrdinalIgnoreCase))
            {
                assetInfo.AssetType = AssetType.Native;
            }
            else if (assetTypeString.Equals("resources", StringComparison.OrdinalIgnoreCase))
            {
                assetInfo.AssetType = AssetType.Resources;
            }
            else if (assetTypeString.Equals("pgodata", StringComparison.OrdinalIgnoreCase))
            {
                assetInfo.AssetType = AssetType.PgoData;
            }
            else
            {
                throw new InvalidOperationException("Unexpected asset type: " + item.GetMetadata(MetadataKeys.AssetType));
            }

            assetInfo.PackageName = item.GetMetadata(MetadataKeys.NuGetPackageId);
            assetInfo.PackageVersion = item.GetMetadata(MetadataKeys.NuGetPackageVersion);
            assetInfo.PackageRuntimeIdentifier = item.GetMetadata(MetadataKeys.RuntimeIdentifier);

            return assetInfo;
        }
    }
}
