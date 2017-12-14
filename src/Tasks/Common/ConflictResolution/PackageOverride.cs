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
        public Dictionary<string, Version> OverridenPackages { get; }

        private PackageOverride(string packageName, IEnumerable<Tuple<string, Version>> overridenPackages)
        {
            PackageName = packageName;

            OverridenPackages = new Dictionary<string, Version>(StringComparer.OrdinalIgnoreCase);
            foreach (Tuple<string, Version> package in overridenPackages)
            {
                OverridenPackages[package.Item1] = package.Item2;
            }
        }

        public static PackageOverride Create(ITaskItem packageOverrideItem)
        {
            string packageName = packageOverrideItem.ItemSpec;
            string overridenPackagesString = packageOverrideItem.GetMetadata(MetadataKeys.OverridenPackages);

            return new PackageOverride(packageName, CreateOverridenPackages(overridenPackagesString));
        }

        private static IEnumerable<Tuple<string, Version>> CreateOverridenPackages(string overridenPackagesString)
        {
            if (!string.IsNullOrEmpty(overridenPackagesString))
            {
                overridenPackagesString = overridenPackagesString.Trim();
                string[] overridenPackagesAndVersions = overridenPackagesString.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string overridenPackagesAndVersion in overridenPackagesAndVersions)
                {
                    string trimmedOverridenPackagesAndVersion = overridenPackagesAndVersion.Trim();
                    int separatorIndex = trimmedOverridenPackagesAndVersion.IndexOf('|');
                    if (separatorIndex != -1)
                    {
                        if (Version.TryParse(trimmedOverridenPackagesAndVersion.Substring(separatorIndex + 1), out Version version))
                        {
                            yield return Tuple.Create(trimmedOverridenPackagesAndVersion.Substring(0, separatorIndex), version);
                        }
                    }
                }
            }
        }
    }
}
