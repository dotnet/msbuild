// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Packaging.Core;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Parses the target manifest files into MSBuild Items.
    /// </summary>
    public sealed class ParseTargetManifests : TaskBase
    {
        public string TargetManifestFiles { get; set; }

        [Output]
        public ITaskItem[] RuntimeStorePackages { get; private set; }

        protected override void ExecuteCore()
        {
            string[] targetManifestFileList = TargetManifestFiles?.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            if (targetManifestFileList != null && targetManifestFileList.Length > 0)
            {
                var runtimeStorePackages = new Dictionary<PackageIdentity, StringBuilder>();
                foreach (var manifestFile in targetManifestFileList)
                {
                    var packagesSpecified = StoreArtifactParser.Parse(manifestFile);
                    var targetManifestFileName = Path.GetFileName(manifestFile);

                    foreach (var pkg in packagesSpecified)
                    {
                        StringBuilder fileList;
                        if (runtimeStorePackages.TryGetValue(pkg, out fileList))
                        {
                            fileList.Append($";{targetManifestFileName}");
                        }
                        else
                        {
                            runtimeStorePackages.Add(pkg, new StringBuilder(targetManifestFileName));
                        }
                    }
                }

                var resultPackages = new List<ITaskItem>();
                foreach (var storeEntry in runtimeStorePackages)
                {
                    string packageName = storeEntry.Key.Id;
                    string packageVersion = storeEntry.Key.Version.ToNormalizedString();

                    TaskItem item = new($"{packageName}/{packageVersion}");
                    item.SetMetadata(MetadataKeys.NuGetPackageId, packageName);
                    item.SetMetadata(MetadataKeys.NuGetPackageVersion, packageVersion);
                    item.SetMetadata(MetadataKeys.RuntimeStoreManifestNames, storeEntry.Value.ToString());

                    resultPackages.Add(item);
                }

                RuntimeStorePackages = resultPackages.ToArray();
            }
        }
    }
}
