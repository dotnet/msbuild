// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.Core.Build.Tasks
{
    internal static class LockFileTargetExtensions
    {
        public static LockFileTargetLibrary GetPlatformLibrary(this LockFileTarget lockFileTarget)
        {
            // TODO: https://github.com/dotnet/sdk/issues/17 get this from the lock file
            var platformPackageName = "Microsoft.NETCore.App";
            var platformExport = lockFileTarget
                .Libraries
                .FirstOrDefault(e => e.Name.Equals(platformPackageName, StringComparison.OrdinalIgnoreCase));

            return platformExport;
        }

        public static HashSet<string> GetPlatformExclusionList(
            LockFileTargetLibrary platformLibrary,
            IDictionary<string, LockFileTargetLibrary> libraryLookup)
        {
            var exclusionList = new HashSet<string>();

            exclusionList.Add(platformLibrary.Name);
            CollectDependencies(libraryLookup, platformLibrary.Dependencies, exclusionList);

            return exclusionList;
        }

        private static void CollectDependencies(
            IDictionary<string, LockFileTargetLibrary> libraryLookup,
            IEnumerable<PackageDependency> dependencies,
            HashSet<string> exclusionList)
        {
            foreach (PackageDependency dependency in dependencies)
            {
                LockFileTargetLibrary export = libraryLookup[dependency.Id];
                if (export.Version.Equals(dependency.VersionRange.MinVersion))
                {
                    if (exclusionList.Add(export.Name))
                    {
                        CollectDependencies(libraryLookup, export.Dependencies, exclusionList);
                    }
                }
            }
        }

        public static IEnumerable<LockFileTargetLibrary> FilterExports(this IEnumerable<LockFileTargetLibrary> exports, HashSet<string> exclusionList)
        {
            return exports.Where(e => !exclusionList.Contains(e.Name));
        }
    }
}
