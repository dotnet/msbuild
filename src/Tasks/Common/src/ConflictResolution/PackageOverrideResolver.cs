// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks.ConflictResolution
{
    /// <summary>
    /// Resolves conflicts between items by allowing specific packages to override
    /// all items coming from a set of packages up to a certain version of each package.
    /// </summary>
    internal class PackageOverrideResolver<TConflictItem> where TConflictItem : class, IConflictItem
    {
        private Dictionary<string, PackageOverride> _packageOverrides;

        public PackageOverrideResolver(ITaskItem[] packageOverrideItems)
        {
            if (packageOverrideItems?.Length > 0)
            {
                _packageOverrides = new Dictionary<string, PackageOverride>(packageOverrideItems.Length, StringComparer.OrdinalIgnoreCase);

                foreach (ITaskItem packageOverrideItem in packageOverrideItems)
                {
                    PackageOverride packageOverride = PackageOverride.Create(packageOverrideItem);

                    if (_packageOverrides.TryGetValue(packageOverride.PackageName, out PackageOverride existing))
                    {
                        MergePackageOverrides(packageOverride, existing);
                    }
                    else
                    {
                        _packageOverrides[packageOverride.PackageName] = packageOverride;
                    }
                }
            }
        }

        /// <summary>
        /// Merges newPackageOverride into existingPackageOverride by adding all the new overriden packages
        /// and taking the highest version when they both contain the same overriden package.
        /// </summary>
        private static void MergePackageOverrides(PackageOverride newPackageOverride, PackageOverride existingPackageOverride)
        {
            foreach (KeyValuePair<string, Version> newOverride in newPackageOverride.OverridenPackages)
            {
                if (existingPackageOverride.OverridenPackages.TryGetValue(newOverride.Key, out Version existingOverrideVersion))
                {
                    if (existingOverrideVersion < newOverride.Value)
                    {
                        existingPackageOverride.OverridenPackages[newOverride.Key] = newOverride.Value;
                    }
                }
                else
                {
                    existingPackageOverride.OverridenPackages[newOverride.Key] = newOverride.Value;
                }
            }
        }

        public TConflictItem Resolve(TConflictItem item1, TConflictItem item2)
        {
            if (_packageOverrides != null)
            {
                PackageOverride packageOverride;
                Version version;
                if (item1.PackageId != null
                    && _packageOverrides.TryGetValue(item1.PackageId, out packageOverride)
                    && packageOverride.OverridenPackages.TryGetValue(item2.PackageId, out version)
                    && item2.PackageVersion != null
                    && item2.PackageVersion <= version)
                {
                    return item1;
                }
                else if (item2.PackageId != null
                    && _packageOverrides.TryGetValue(item2.PackageId, out packageOverride)
                    && packageOverride.OverridenPackages.TryGetValue(item1.PackageId, out version)
                    && item1.PackageVersion != null
                    && item1.PackageVersion <= version)
                {
                    return item2;
                }
            }

            return null;
        }
    }
}
