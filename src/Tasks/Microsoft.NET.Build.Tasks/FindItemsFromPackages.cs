// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using NuGet.Packaging.Core;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Returns the ITaskItem values in Items that were resolved from the specified
    /// set of Packages.
    /// </summary>
    /// <remarks>
    /// Both Items and Packages are expected to have ('PackageName' and 'PackageVersion)' or ('NuGetPackageId' and 'NuGetPackageVersion')
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
