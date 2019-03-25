using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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

        [Output]
        public ITaskItem[] Output { get; set; }

        protected override void ExecuteCore()
        {
            if (Items.Length == 0 || PackageFolders.Length == 0)
            {
                Output = Items;
                return;
            }

            var packageResolver = NuGetPackageResolver.CreateResolver(PackageFolders);

            int index = 0;
            var updatedItems = new ITaskItem[Items.Length];

            foreach (var item in Items)
            {
                string packageName = item.GetMetadata(MetadataKeys.PackageName);
                string packageVersion = item.GetMetadata(MetadataKeys.PackageVersion);

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
