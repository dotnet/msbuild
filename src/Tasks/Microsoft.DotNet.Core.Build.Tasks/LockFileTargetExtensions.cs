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
        public static ProjectContext CreateProjectContext(this LockFileTarget lockFileTarget)
        {
            bool isPortable = string.IsNullOrEmpty(lockFileTarget.RuntimeIdentifier);

            IEnumerable<LockFileTargetLibrary> runtimeLibraries = lockFileTarget.Libraries;
            Dictionary<string, LockFileTargetLibrary> libraryLookup = 
                runtimeLibraries.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);

            HashSet<string> allExclusionList = new HashSet<string>();

            if (isPortable)
            {
                var platformLibrary = lockFileTarget.GetPlatformLibrary();

                isPortable = platformLibrary != null;
                if (isPortable)
                {
                    allExclusionList.UnionWith(GetPlatformExclusionList(platformLibrary, libraryLookup));
                }
            }

            // TODO: exclude "type: build" dependencies during publish - https://github.com/dotnet/sdk/issues/42

            return new ProjectContext(
                isPortable,
                runtimeLibraries.Filter(allExclusionList).ToArray());
        }

        private static LockFileTargetLibrary GetPlatformLibrary(this LockFileTarget lockFileTarget)
        {
            // TODO: https://github.com/dotnet/sdk/issues/17 get this from the lock file
            var platformPackageName = "Microsoft.NETCore.App";
            var platformLibrary = lockFileTarget
                .Libraries
                .FirstOrDefault(e => e.Name.Equals(platformPackageName, StringComparison.OrdinalIgnoreCase));

            return platformLibrary;
        }

        private static HashSet<string> GetPlatformExclusionList(
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
                LockFileTargetLibrary library = libraryLookup[dependency.Id];
                if (library.Version.Equals(dependency.VersionRange.MinVersion))
                {
                    if (exclusionList.Add(library.Name))
                    {
                        CollectDependencies(libraryLookup, library.Dependencies, exclusionList);
                    }
                }
            }
        }

        private static IEnumerable<LockFileTargetLibrary> Filter(
            this IEnumerable<LockFileTargetLibrary> libraries, 
            HashSet<string> exclusionList)
        {
            return libraries.Where(e => !exclusionList.Contains(e.Name));
        }
    }
}
