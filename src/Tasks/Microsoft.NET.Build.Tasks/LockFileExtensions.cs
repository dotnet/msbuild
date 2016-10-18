// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;

namespace Microsoft.NET.Build.Tasks
{
    public static class LockFileExtensions
    {
        public static ProjectContext CreateProjectContext(
            this LockFile lockFile,
            NuGetFramework framework,
            string runtime,
            string platformLibraryName)
        {
            if (lockFile == null)
            {
                throw new ArgumentNullException(nameof(lockFile));
            }
            if (framework == null)
            {
                throw new ArgumentNullException(nameof(framework));
            }

            LockFileTarget lockFileTarget = lockFile.GetTarget(framework, runtime);

            if (lockFileTarget == null)
            {
                string frameworkString = framework.DotNetFrameworkName;
                string targetMoniker = string.IsNullOrEmpty(runtime) ?
                    frameworkString :
                    $"{frameworkString}/{runtime}";

                throw new ReportUserErrorException($"Assets file '{lockFile.Path}' doesn't have a target for '{targetMoniker}'." +
                    $" Ensure you have restored this project for TargetFramework='{framework.GetShortFolderName()}'" +
                    $" and RuntimeIdentifier='{runtime}'.");
            }

            return new ProjectContext(lockFile, lockFileTarget, platformLibraryName);
        }

        public static LockFileTargetLibrary GetLibrary(this LockFileTarget lockFileTarget, string libraryName)
        {
            if (string.IsNullOrEmpty(libraryName))
            {
                return null;
            }

            return lockFileTarget
                .Libraries
                .FirstOrDefault(e => e.Name.Equals(libraryName, StringComparison.OrdinalIgnoreCase));
        }

        public static HashSet<string> GetPlatformExclusionList(
            this LockFileTarget lockFileTarget,
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
