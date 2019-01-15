using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks
{
    public class ResolveRuntimePackAssets : TaskBase
    {
        public ITaskItem[] ResolvedRuntimePacks { get; set; }

        [Output]
        public ITaskItem[] RuntimePackAssets { get; set; }

        protected override void ExecuteCore()
        {
            List<TaskItem> runtimePackAssets = new List<TaskItem>();

            foreach (var runtimePack in ResolvedRuntimePacks)
            {
                string runtimePackRoot = runtimePack.GetMetadata(MetadataKeys.PackageDirectory);
                string runtimeIdentifier = runtimePack.GetMetadata(MetadataKeys.RuntimeIdentifier);

                string runtimeAssetsPath = Path.Combine(runtimePackRoot, "runtimes", runtimeIdentifier, "lib", "netcoreapp3.0");
                string nativeAssetsPath = Path.Combine(runtimePackRoot, "runtimes", runtimeIdentifier, "native");

                var runtimeAssets = Directory.Exists(runtimeAssetsPath) ? Directory.GetFiles(runtimeAssetsPath) : Array.Empty<string>();
                var nativeAssets = Directory.Exists(nativeAssetsPath) ? Directory.GetFiles(nativeAssetsPath) : Array.Empty<string>();

                void AddAsset(string assetPath, string assetType)
                {
                    if (Path.GetExtension(assetPath).Equals(".pdb", StringComparison.OrdinalIgnoreCase))
                    {
                        //  Don't add assets for .pdb files
                        return;
                    }

                    var assetItem = new TaskItem(assetPath);

                    assetItem.SetMetadata(MetadataKeys.CopyLocal, "true");
                    assetItem.SetMetadata(MetadataKeys.DestinationSubPath, Path.GetFileName(assetPath));
                    assetItem.SetMetadata(MetadataKeys.AssetType, assetType);
                    assetItem.SetMetadata(MetadataKeys.PackageName, runtimePack.GetMetadata(MetadataKeys.PackageName));
                    assetItem.SetMetadata(MetadataKeys.PackageVersion, runtimePack.GetMetadata(MetadataKeys.PackageVersion));
                    assetItem.SetMetadata(MetadataKeys.RuntimeIdentifier, runtimeIdentifier);

                    runtimePackAssets.Add(assetItem);
                }

                foreach (var asset in runtimeAssets)
                {
                    AddAsset(asset, "runtime");
                }
                foreach (var asset in nativeAssets)
                {
                    AddAsset(asset, "native");
                }
            }

            RuntimePackAssets = runtimePackAssets.ToArray();
        }
    }
}
