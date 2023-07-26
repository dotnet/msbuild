// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//Microsoft.NET.Build.Extensions.Tasks (net7.0) has nullables disabled
#pragma warning disable IDE0240 // Remove redundant nullable directive
#nullable disable
#pragma warning restore IDE0240 // Remove redundant nullable directive

using Microsoft.Build.Framework;

#if EXTENSIONS
using OverrideVersion = System.Version;
#else
using OverrideVersion = NuGet.Versioning.NuGetVersion;
using NuGet.Versioning;
#endif

namespace Microsoft.NET.Build.Tasks.ConflictResolution
{
    /// <summary>
    /// Resolves conflicts between items by allowing specific packages to override
    /// all items coming from a set of packages up to a certain version of each package.
    /// </summary>
    internal class PackageOverrideResolver<TConflictItem> where TConflictItem : class, IConflictItem
    {
        private ITaskItem[] _packageOverrideItems;
        private Lazy<Dictionary<string, PackageOverride>> _packageOverrides;

        public PackageOverrideResolver(ITaskItem[] packageOverrideItems)
        {
            _packageOverrideItems = packageOverrideItems;
            _packageOverrides = new Lazy<Dictionary<string, PackageOverride>>(() => BuildPackageOverrides());
        }

        public Dictionary<string, PackageOverride> PackageOverrides => _packageOverrides.Value;

        private Dictionary<string, PackageOverride> BuildPackageOverrides()
        {
            Dictionary<string, PackageOverride> result;

            if (_packageOverrideItems?.Length > 0)
            {
                result = new Dictionary<string, PackageOverride>(_packageOverrideItems.Length, StringComparer.OrdinalIgnoreCase);

                foreach (ITaskItem packageOverrideItem in _packageOverrideItems)
                {
                    PackageOverride packageOverride = PackageOverride.Create(packageOverrideItem);

                    if (result.TryGetValue(packageOverride.PackageName, out PackageOverride existing))
                    {
                        MergePackageOverrides(packageOverride, existing);
                    }
                    else
                    {
                        result[packageOverride.PackageName] = packageOverride;
                    }
                }
            }
            else
            {
                result = null;
            }

            return result;
        }

        /// <summary>
        /// Merges newPackageOverride into existingPackageOverride by adding all the new overridden packages
        /// and taking the highest version when they both contain the same overridden package.
        /// </summary>
        private static void MergePackageOverrides(PackageOverride newPackageOverride, PackageOverride existingPackageOverride)
        {
            foreach (KeyValuePair<string, OverrideVersion> newOverride in newPackageOverride.OverriddenPackages)
            {
                if (existingPackageOverride.OverriddenPackages.TryGetValue(newOverride.Key, out OverrideVersion existingOverrideVersion))
                {
                    if (existingOverrideVersion < newOverride.Value)
                    {
                        existingPackageOverride.OverriddenPackages[newOverride.Key] = newOverride.Value;
                    }
                }
                else
                {
                    existingPackageOverride.OverriddenPackages[newOverride.Key] = newOverride.Value;
                }
            }
        }

        public TConflictItem Resolve(TConflictItem item1, TConflictItem item2)
        {
            if (PackageOverrides != null && item1.PackageId != null && item2.PackageId != null)
            {
                PackageOverride packageOverride;
                OverrideVersion version;
                if (PackageOverrides.TryGetValue(item1.PackageId, out packageOverride)
                    && packageOverride.OverriddenPackages.TryGetValue(item2.PackageId, out version)
                    && item2.PackageVersion != null
                    && item2.PackageVersion <= version)
                {
                    return item1;
                }
                else if (PackageOverrides.TryGetValue(item2.PackageId, out packageOverride)
                    && packageOverride.OverriddenPackages.TryGetValue(item1.PackageId, out version)
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
