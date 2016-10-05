// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;

namespace Microsoft.NETCore.Build.Tasks
{
    internal static class LockFileExtensions
    {
        public static ProjectContext CreateProjectContext(this LockFile lockFile, NuGetFramework framework, string runtime)
        {
            LockFileTarget lockFileTarget = lockFile.GetTarget(framework, runtime);

            return new ProjectContext(lockFile, lockFileTarget);
        }

        public static bool IsPortable(this LockFileTarget lockFileTarget)
        {
            return string.IsNullOrEmpty(lockFileTarget.RuntimeIdentifier) &&
                lockFileTarget.GetPlatformLibrary() != null;
        }

        public static LockFileTargetLibrary GetPlatformLibrary(this LockFileTarget lockFileTarget)
        {
            // TODO: https://github.com/dotnet/sdk/issues/17 get this from the lock file
            var platformPackageName = "Microsoft.NETCore.App";
            var platformLibrary = lockFileTarget
                .Libraries
                .FirstOrDefault(e => e.Name.Equals(platformPackageName, StringComparison.OrdinalIgnoreCase));

            return platformLibrary;
        }

        public static HashSet<string> GetPlatformExclusionList(
            this LockFileTarget lockFileTarget,
            IDictionary<string, LockFileTargetLibrary> libraryLookup)
        {
            var platformLibrary = lockFileTarget.GetPlatformLibrary();
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

        public static HashSet<string> GetPrivateAssetsExclusionList(
            LockFile lockFile,
            LockFileTarget lockFileTarget,
            IEnumerable<string> privateAssetPackageIds,
            IDictionary<string, LockFileTargetLibrary> libraryLookup)
        {
            var nonPrivateAssets = new HashSet<string>();

            var nonPrivateAssetsToSearch = new Stack<string>();
            var privateAssetsToSearch = new Stack<string>();

            // Start with the top-level dependencies, and put them into "private" or "non-private" buckets
            var privateAssetPackagesLookup = new HashSet<string>(privateAssetPackageIds);
            foreach (var topLevelDependency in GetTopLevelDependencies(lockFile, lockFileTarget))
            {
                if (!privateAssetPackagesLookup.Contains(topLevelDependency))
                {
                    nonPrivateAssetsToSearch.Push(topLevelDependency);
                    nonPrivateAssets.Add(topLevelDependency);
                }
                else
                {
                    privateAssetsToSearch.Push(topLevelDependency);
                }
            }

            LockFileTargetLibrary library;
            string libraryName;

            // Walk all the non-private assets' dependencies and mark them as non-private
            while (nonPrivateAssetsToSearch.Count > 0)
            {
                libraryName = nonPrivateAssetsToSearch.Pop();
                library = libraryLookup[libraryName];

                foreach (var dependency in library.Dependencies)
                {
                    if (!nonPrivateAssets.Contains(dependency.Id))
                    {
                        nonPrivateAssetsToSearch.Push(dependency.Id);
                        nonPrivateAssets.Add(dependency.Id);
                    }
                }
            }

            // Go through assets marked private and their dependencies
            // For libraries not marked as non-private, mark them down as private
            var privateAssetsToExclude = new HashSet<string>();
            while (privateAssetsToSearch.Count > 0)
            {
                libraryName = privateAssetsToSearch.Pop();
                library = libraryLookup[libraryName];

                privateAssetsToExclude.Add(libraryName);

                foreach (var dependency in library.Dependencies)
                {
                    if (!nonPrivateAssets.Contains(dependency.Id))
                    {
                        privateAssetsToSearch.Push(dependency.Id);
                    }
                }
            }

            return privateAssetsToExclude;
        }

        public static IEnumerable<string> GetTopLevelDependencies(
            LockFile lockFile, 
            LockFileTarget lockFileTarget)
        {
            return lockFile
                .ProjectFileDependencyGroups
                .Where(dg => dg.FrameworkName == string.Empty ||
                             dg.FrameworkName == lockFileTarget.TargetFramework.DotNetFrameworkName)
                .SelectMany(g => g.Dependencies)
                .Select(projectFileDependency =>
                {
                    int separatorIndex = projectFileDependency.IndexOf(' ');
                    return separatorIndex > 0 ?
                        projectFileDependency.Substring(0, separatorIndex) :
                        projectFileDependency;
                });
        }

        public static IEnumerable<LockFileTargetLibrary> Filter(
            this IEnumerable<LockFileTargetLibrary> libraries, 
            HashSet<string> exclusionList)
        {
            return libraries.Where(e => !exclusionList.Contains(e.Name));
        }

        public static IEnumerable<IGrouping<string, LockFileRuntimeTarget>> GetRuntimeTargetsGroups(
            this LockFileTargetLibrary library, 
            string assetType)
        {
            return library.RuntimeTargets
                .FilterPlaceHolderFiles()
                .Cast<LockFileRuntimeTarget>()
                .Where(t => string.Equals(t.AssetType, assetType, StringComparison.OrdinalIgnoreCase))
                .GroupBy(t => t.Runtime);
        }
    }
}
