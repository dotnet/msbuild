// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Versioning;

namespace Microsoft.NET.Build.Tasks
{
    //  Locates the root NuGet package directory for each of the input items that has PackageName and PackageVersion,
    //  but not PackageDirectory metadata specified
    public class GetPackageDirectory : TaskBase
    {
        public ITaskItem[] Items { get; set; } = Array.Empty<ITaskItem>();

        public string[] PackageFolders { get; set; } = Array.Empty<string>();

        public string AssetsFileWithAdditionalPackageFolders { get; set; }

        [Output]
        public ITaskItem[] Output { get; set; }

        protected override void ExecuteCore()
        {
            if (Items.Length == 0 || (PackageFolders.Length == 0 && string.IsNullOrEmpty(AssetsFileWithAdditionalPackageFolders)))
            {
                Output = Items;
                return;
            }

            if (!string.IsNullOrEmpty(AssetsFileWithAdditionalPackageFolders))
            {
                var lockFileCache = new LockFileCache(this);
                var lockFile = lockFileCache.GetLockFile(AssetsFileWithAdditionalPackageFolders);
                PackageFolders = PackageFolders.Concat(lockFile.PackageFolders.Select(p => p.Path)).ToArray();
            }

            var packageResolver = NuGetPackageResolver.CreateResolver(PackageFolders);

            int index = 0;
            var updatedItems = new ITaskItem[Items.Length];

            foreach (var item in Items)
            {
                string packageName = item.GetMetadata(MetadataKeys.NuGetPackageId);
                string packageVersion = item.GetMetadata(MetadataKeys.NuGetPackageVersion);

                if (string.IsNullOrEmpty(packageName) || string.IsNullOrEmpty(packageVersion)
                    || !string.IsNullOrEmpty(item.GetMetadata(MetadataKeys.PackageDirectory)))
                {
                    updatedItems[index++] = item;
                    continue;
                }

                var parsedPackageVersion = NuGetVersion.Parse(packageVersion);
                string packageDirectory = packageResolver.GetPackageDirectory(packageName, parsedPackageVersion);

                if (packageDirectory == null)
                {
                    updatedItems[index++] = item;
                    continue;
                }

                var newItem = new TaskItem(item);
                newItem.SetMetadata(MetadataKeys.PackageDirectory, packageDirectory);
                updatedItems[index++] = newItem;
            }

            Output = updatedItems;
        }
    }
}
