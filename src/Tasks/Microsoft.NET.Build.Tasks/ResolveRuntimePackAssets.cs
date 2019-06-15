using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks
{
    public class ResolveRuntimePackAssets : TaskBase
    {
        public ITaskItem[] ResolvedRuntimePacks { get; set; }

        public ITaskItem[] FrameworkReferences { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] UnavailableRuntimePacks { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] SatelliteResourceLanguages { get; set; } = Array.Empty<ITaskItem>();

        [Output]
        public ITaskItem[] RuntimePackAssets { get; set; }

        protected override void ExecuteCore()
        {
            var runtimePackAssets = new List<ITaskItem>();

            HashSet<string> frameworkReferenceNames = new HashSet<string>(FrameworkReferences.Select(item => item.ItemSpec), StringComparer.OrdinalIgnoreCase);

            foreach (var unavailableRuntimePack in UnavailableRuntimePacks)
            {
                if (frameworkReferenceNames.Contains(unavailableRuntimePack.ItemSpec))
                {
                    //  This is a runtime pack that should be used, but wasn't available for the specified RuntimeIdentifier
                    //  NETSDK1082: There was no runtime pack for {0} available for the specified RuntimeIdentifier '{1}'.
                    Log.LogError(Strings.NoRuntimePackAvailable, unavailableRuntimePack.ItemSpec,
                        unavailableRuntimePack.GetMetadata(MetadataKeys.RuntimeIdentifier));
                }
            }

            HashSet<string> processedRuntimePackRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var runtimePack in ResolvedRuntimePacks)
            {
                if (!frameworkReferenceNames.Contains(runtimePack.GetMetadata(MetadataKeys.FrameworkName)))
                {
                    //  This is a runtime pack for a shared framework that ultimately wasn't referenced, so don't include its assets
                    continue;
                }

                string runtimePackRoot = runtimePack.GetMetadata(MetadataKeys.PackageDirectory);

                if (string.IsNullOrEmpty(runtimePackRoot) || !Directory.Exists(runtimePackRoot))
                {
                    //  If we do the work in https://github.com/dotnet/cli/issues/10528,
                    //  then we should add a new error message here indicating that the runtime pack hasn't
                    //  been downloaded, and that restore should be run with that runtime identifier.
                    Log.LogError(Strings.NoRuntimePackAvailable, runtimePack.ItemSpec,
                        runtimePack.GetMetadata(MetadataKeys.RuntimeIdentifier));
                }

                if (!processedRuntimePackRoots.Add(runtimePackRoot))
                {
                    //  We already added assets from this runtime pack (which can happen with FrameworkReferences to different
                    //  profiles of the same shared framework)
                    continue;
                }

                var runtimeListPath = Path.Combine(runtimePackRoot, "data", "RuntimeListList.xml");

                if (File.Exists(runtimeListPath))
                {
                    AddRuntimePackAssetsFromManifest(runtimePackAssets, runtimePackRoot, runtimeListPath, runtimePack);
                }
                else
                {
                    runtimePackAssets.AddRange(GetRuntimePackAssetsFromConvention(runtimePackRoot, runtimePack));
                }
            }

            RuntimePackAssets = runtimePackAssets.ToArray();
        }

        private void AddRuntimePackAssetsFromManifest(List<ITaskItem> runtimePackAssets, string runtimePackRoot,
            string runtimeListPath, ITaskItem runtimePack)
        {            
            XDocument frameworkListDoc = XDocument.Load(runtimeListPath);
            foreach (var fileElement in frameworkListDoc.Root.Elements("File"))
            {
                string assetPath = Path.Combine(runtimePackRoot, fileElement.Attribute("Path").Value);

                string typeAttributeValue = fileElement.Attribute("Type").Value;
                string assetType;
                string culture = null;
                if (typeAttributeValue.Equals("Managed", StringComparison.OrdinalIgnoreCase))
                {
                    assetType = "runtime";
                }
                else if (typeAttributeValue.Equals("Native", StringComparison.OrdinalIgnoreCase))
                {
                    assetType = "native";
                }
                else if (typeAttributeValue.Equals("Resources", StringComparison.OrdinalIgnoreCase))
                {
                    assetType = "resources";
                    culture = fileElement.Attribute("Culture")?.Value;
                    if (culture == null)
                    {
                        throw new BuildErrorException($"Culture not set in runtime manifest for {assetPath}");
                    }
                    if (this.SatelliteResourceLanguages.Length > 1 &&
                        !this.SatelliteResourceLanguages.Any(lang => string.Equals(lang.ItemSpec, culture, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }
                }
                else
                {
                    throw new BuildErrorException($"Unrecognized file type '{typeAttributeValue}' in {runtimeListPath}");
                }

                var assetItem = CreateAssetItem(assetPath, assetType, runtimePack, culture);

                assetItem.SetMetadata("AssemblyVersion", fileElement.Attribute("AssemblyVersion")?.Value);
                assetItem.SetMetadata("FileVersion", fileElement.Attribute("FileVersion")?.Value);
                assetItem.SetMetadata("PublicKeyToken", fileElement.Attribute("PublicKeyToken")?.Value);

                runtimePackAssets.Add(assetItem);
            }
        }

        private IEnumerable<TaskItem> GetRuntimePackAssetsFromConvention(string runtimePackRoot, ITaskItem runtimePack)
        {
            List<TaskItem> runtimePackAssets = new List<TaskItem>();

            string runtimeIdentifier = runtimePack.GetMetadata(MetadataKeys.RuntimeIdentifier);

            //  These hard-coded paths are temporary until we have "real" runtime packs, which will likely have a flattened
            //  folder structure and a manifest indicating how the files should be used: https://github.com/dotnet/cli/issues/10442
            string runtimeAssetsPath = GetRuntimeAssetsPath(runtimePackRoot, runtimeIdentifier);
            string nativeAssetsPath = Path.Combine(runtimePackRoot, "runtimes", runtimeIdentifier, "native");

            var runtimeAssets = Directory.Exists(runtimeAssetsPath) ? Directory.GetFiles(runtimeAssetsPath) : Array.Empty<string>();
            var nativeAssets = Directory.Exists(nativeAssetsPath) ? Directory.GetFiles(nativeAssetsPath) : Array.Empty<string>();

            void AddAsset(string assetPath, string assetType)
            {
                if (assetPath.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase) ||
                    assetPath.EndsWith(".map", StringComparison.OrdinalIgnoreCase) ||
                    assetPath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
                    assetPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
                    assetPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                    assetPath.EndsWith("._", StringComparison.Ordinal))
                {
                    //  Don't add assets for these files (shouldn't be necessary if/once we have a manifest in the runtime pack
                    //  https://github.com/dotnet/cli/issues/10442
                    return;
                }

                var assetItem = CreateAssetItem(assetPath, assetType, runtimePack, null);

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

            runtimePackAssets.AddRange(EnumerateResourceAssets(runtimePackRoot, runtimeIdentifier, runtimePack));
            return runtimePackAssets;
        }

        private static TaskItem CreateAssetItem(string assetPath, string assetType, ITaskItem runtimePack, string culture)
        {
            string runtimeIdentifier = runtimePack.GetMetadata(MetadataKeys.RuntimeIdentifier);

            var assetItem = new TaskItem(assetPath);

            assetItem.SetMetadata(MetadataKeys.CopyLocal, "true");
            if (string.IsNullOrEmpty(culture))
            {
                assetItem.SetMetadata(MetadataKeys.DestinationSubPath, Path.GetFileName(assetPath));
            }
            else
            {
                assetItem.SetMetadata(MetadataKeys.DestinationSubDirectory, culture + Path.DirectorySeparatorChar);
                assetItem.SetMetadata(MetadataKeys.DestinationSubPath, Path.Combine(culture, Path.GetFileName(assetPath)));
                assetItem.SetMetadata(MetadataKeys.Culture, culture);
            }

            assetItem.SetMetadata(MetadataKeys.AssetType, assetType);
            assetItem.SetMetadata(MetadataKeys.PackageName, runtimePack.GetMetadata(MetadataKeys.PackageName));
            assetItem.SetMetadata(MetadataKeys.PackageVersion, runtimePack.GetMetadata(MetadataKeys.PackageVersion));
            assetItem.SetMetadata(MetadataKeys.RuntimeIdentifier, runtimeIdentifier);
            assetItem.SetMetadata(MetadataKeys.IsTrimmable, runtimePack.GetMetadata(MetadataKeys.IsTrimmable));

            return assetItem;
        }

        private static string GetRuntimeAssetsPath(string runtimePackRoot, string runtimeIdentifier)
        {
            //  These hard-coded paths are temporary until we have "real" runtime packs, which will likely have a flattened structure
            return Path.Combine(runtimePackRoot, "runtimes", runtimeIdentifier, "lib", "netcoreapp3.0");
        }

        private IEnumerable<TaskItem> EnumerateResourceAssets(string runtimePackRoot, string runtimeIdentifier, ITaskItem runtimePack)
        {
            //  These hard-coded paths are temporary until we have "real" runtime packs, which will likely have a flattened structure
            var directory = GetRuntimeAssetsPath(runtimePackRoot, runtimeIdentifier);
            if (!Directory.Exists(directory))
            {
                yield break;
            }

            foreach (var subdir in Directory.EnumerateDirectories(directory))
            {
                foreach (var asset in EnumerateCultureAssets(subdir, runtimeIdentifier, runtimePack))
                {
                    yield return asset;
                }
            }
        }

        private IEnumerable<TaskItem> EnumerateCultureAssets(string cultureDirectory, string runtimeIdentifier, ITaskItem runtimePack)
        {
            var culture = Path.GetFileName(cultureDirectory);

            if (this.SatelliteResourceLanguages.Length > 1 &&
                !this.SatelliteResourceLanguages.Any(lang => string.Equals(lang.ItemSpec, culture, StringComparison.OrdinalIgnoreCase)))
            {
                yield break;
            }

            foreach (var file in Directory.EnumerateFiles(cultureDirectory, "*.resources.dll"))
            {
                var item = CreateAssetItem(file, "resources", runtimePack, culture);

                yield return item;
            }
        }
    }
}
