// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
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
    /// A PackageOverride contains information about a package that overrides
    /// a set of packages up to a certain version.
    /// </summary>
    /// <remarks>
    /// For example, Microsoft.NETCore.App overrides System.Console up to version 4.3.0,
    /// System.IO up to version version 4.3.0, etc.
    /// </remarks>
    internal class PackageOverride
    {
        public string PackageName { get; }
        public Dictionary<string, OverrideVersion> OverriddenPackages { get; }

        private PackageOverride(string packageName, IEnumerable<Tuple<string, OverrideVersion>> overriddenPackages)
        {
            PackageName = packageName;

            OverriddenPackages = new Dictionary<string, OverrideVersion>(StringComparer.OrdinalIgnoreCase);
            foreach (Tuple<string, OverrideVersion> package in overriddenPackages)
            {
                OverriddenPackages[package.Item1] = package.Item2;
            }
        }

        public static PackageOverride Create(ITaskItem packageOverrideItem)
        {
            string packageName = packageOverrideItem.ItemSpec;
            string overriddenPackagesString = packageOverrideItem.GetMetadata(MetadataKeys.OverriddenPackages);

            return new PackageOverride(packageName, CreateOverriddenPackages(overriddenPackagesString));
        }

        private static IEnumerable<Tuple<string, OverrideVersion>> CreateOverriddenPackages(string overriddenPackagesString)
        {
            if (!string.IsNullOrEmpty(overriddenPackagesString))
            {
                overriddenPackagesString = overriddenPackagesString.Trim();
                string[] overriddenPackagesAndVersions = overriddenPackagesString.Split(new char[] { ';', '\r', '\n', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string overriddenPackagesAndVersion in overriddenPackagesAndVersions)
                {
                    string trimmedOverriddenPackagesAndVersion = overriddenPackagesAndVersion.Trim();
                    int separatorIndex = trimmedOverriddenPackagesAndVersion.IndexOf('|');
                    if (separatorIndex != -1)
                    {
                        string versionString = trimmedOverriddenPackagesAndVersion.Substring(separatorIndex + 1);
                        string overriddenPackage = trimmedOverriddenPackagesAndVersion.Substring(0, separatorIndex);
                        if (OverrideVersion.TryParse(versionString, out OverrideVersion version))
                        {
                            yield return Tuple.Create(overriddenPackage, version);
                        }
                    }
                }
            }
        }
    }
}
