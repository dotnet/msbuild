// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using NuGet.Packaging.Core;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Returns the ITaskItem values in Items that were resolved from the specified
    /// set of Packages.
    /// </summary>
    /// <remarks>
    /// Both Items and Packages are expected to have 'NuGetPackageId' and 'NuGetPackageVersion'
    /// metadata properties to use for the matching.
    /// </remarks>
    public sealed class FindItemsFromPackages : TaskBase
    {
        [Required]
        public ITaskItem[] Items { get; set; }

        [Required]
        public ITaskItem[] Packages { get; set; }

        [Output]
        public ITaskItem[] ItemsFromPackages { get; private set; }

        protected override void ExecuteCore()
        {
            var packageIdentities = new HashSet<PackageIdentity>(
                Packages.Select(p => ItemUtilities.GetPackageIdentity(p)));

            var itemsFromPackages = new List<ITaskItem>();
            foreach (ITaskItem item in Items)
            {
                PackageIdentity identity = ItemUtilities.GetPackageIdentity(item);
                if (identity != null && packageIdentities.Contains(identity))
                {
                    itemsFromPackages.Add(item);
                }
            }

            ItemsFromPackages = itemsFromPackages.ToArray();
        }
    }
}
