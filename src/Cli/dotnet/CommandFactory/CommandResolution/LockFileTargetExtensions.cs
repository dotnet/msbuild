// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Packaging.Core;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.CommandFactory
{
    internal static class LockFileTargetExtensions
    {
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

        public static IEnumerable<LockFileTargetLibrary> GetRuntimeLibraries(this LockFileTarget lockFileTarget)
        {
            IEnumerable<LockFileTargetLibrary> runtimeLibraries = lockFileTarget.Libraries;
            Dictionary<string, LockFileTargetLibrary> libraryLookup =
                runtimeLibraries.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);

            HashSet<string> allExclusionList = new();

            if (lockFileTarget.IsPortable())
            {
                allExclusionList.UnionWith(lockFileTarget.GetPlatformExclusionList(libraryLookup));
            }

            return runtimeLibraries.Filter(allExclusionList).ToArray();
        }

        public static IEnumerable<LockFileTargetLibrary> GetCompileLibraries(this LockFileTarget lockFileTarget)
        {
            return lockFileTarget.Libraries;
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
    }
}
