// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;

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
        public Dictionary<string, Version> OverriddenPackages { get; }

        private PackageOverride(string packageName, IEnumerable<Tuple<string, Version>> overriddenPackages)
        {
            PackageName = packageName;

            OverriddenPackages = new Dictionary<string, Version>(StringComparer.OrdinalIgnoreCase);
            foreach (Tuple<string, Version> package in overriddenPackages)
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

        private static IEnumerable<Tuple<string, Version>> CreateOverriddenPackages(string overriddenPackagesString)
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
                        if (Version.TryParse(trimmedOverriddenPackagesAndVersion.Substring(separatorIndex + 1), out Version version))
                        {
                            yield return Tuple.Create(trimmedOverriddenPackagesAndVersion.Substring(0, separatorIndex), version);
                        }
                    }
                }
            }
        }
    }
}
