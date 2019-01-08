using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Versioning;

namespace Microsoft.NET.Build.Tasks
{
    //  Locates the root NuGet package directory for each of the input items that has PackageName and PackageVersion
    //  metadata specified
    public class GetPackageDirectory : TaskBase
    {
        public ITaskItem[] Items { get; set; }

        [Required]
        public string ProjectPath { get; set; }

        [Required]
        public string [] PackageFolders { get; set; }

        [Output]
        public ITaskItem[] Output { get; set; }

        protected override void ExecuteCore()
        {
            if (Items != null)
            {
                NuGetPackageResolver packageResolver = NuGetPackageResolver.CreateResolver(PackageFolders, ProjectPath);

                var updatedItems = new List<ITaskItem>();

                foreach (var item in Items)
                {
                    string packageName = item.GetMetadata(MetadataKeys.PackageName);
                    string packageVersion = item.GetMetadata(MetadataKeys.PackageVersion);
                    if (!string.IsNullOrEmpty(packageName) && !string.IsNullOrEmpty(packageVersion))
                    {
                        var newItem = new TaskItem(item);

                        //  Gracefully handle case where we don't have a packageResolver because we don't have an assets
                        //  file, which happens for design-time builds
                        if (packageResolver != null)
                        {
                            var parsedPackageVersion = NuGetVersion.Parse(packageVersion);

                            string packageDirectory = packageResolver.GetPackageDirectory(packageName, parsedPackageVersion);
                            
                            newItem.SetMetadata(MetadataKeys.PackageDirectory, packageDirectory);
                        }

                        updatedItems.Add(newItem);
                    }
                    else
                    {
                        updatedItems.Add(item);
                    }
                }

                Output = updatedItems.ToArray();
            }
        }
    }
}
