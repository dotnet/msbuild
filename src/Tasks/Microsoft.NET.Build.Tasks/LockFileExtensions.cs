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
    internal static class LockFileExtensions
    {
        public static LockFileTarget GetTargetAndThrowIfNotFound(this LockFile lockFile, NuGetFramework framework, string runtime)
        {
            LockFileTarget lockFileTarget = lockFile.GetTarget(framework, runtime);

            if (lockFileTarget == null)
            {
                string frameworkString = framework.DotNetFrameworkName;
                string targetMoniker = string.IsNullOrEmpty(runtime) ?
                    frameworkString :
                    $"{frameworkString}/{runtime}";

                string message;
                if (string.IsNullOrEmpty(runtime))
                {
                    message = string.Format(Strings.AssetsFileMissingTarget, lockFile.Path, targetMoniker, framework.GetShortFolderName());
                }
                else
                {
                    message = string.Format(Strings.AssetsFileMissingRuntimeIdentifier, lockFile.Path, targetMoniker, framework.GetShortFolderName(), runtime);
                }

                throw new BuildErrorException(message);
            }

            return lockFileTarget;
        }

        public static ProjectContext CreateProjectContext(
            this LockFile lockFile,
            NuGetFramework framework,
            string runtime,
            //  Trimmed from publish output, and if there are no runtimeFrameworks, written to runtimeconfig.json
            string platformLibraryName,
            //  Written to runtimeconfig.json
            Microsoft.Build.Framework.ITaskItem[] runtimeFrameworks,
            bool isSelfContained)
        {
            if (lockFile == null)
            {
                throw new ArgumentNullException(nameof(lockFile));
            }
            if (framework == null)
            {
                throw new ArgumentNullException(nameof(framework));
            }

            var lockFileTarget = lockFile.GetTargetAndThrowIfNotFound(framework, runtime);

            LockFileTargetLibrary platformLibrary = lockFileTarget.GetLibrary(platformLibraryName);
            bool isFrameworkDependent = (platformLibrary != null || runtimeFrameworks?.Any() == true) &&
                (!isSelfContained || string.IsNullOrEmpty(lockFileTarget.RuntimeIdentifier));

            return new ProjectContext(lockFile, lockFileTarget, platformLibrary,
                runtimeFrameworks?.Select(i => new ProjectContext.RuntimeFramework(i))?.ToArray(),
                isFrameworkDependent);
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

        private static readonly char[] DependencySeparators = new char[] { '<', '=', '>' };

        public static Dictionary<string, string> GetProjectFileDependencies(this LockFile lockFile)
        {
            var projectDeps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in lockFile.ProjectFileDependencyGroups)
            {
                foreach (var dep in group.Dependencies)
                {
                    var parts = dep.Split(DependencySeparators, StringSplitOptions.RemoveEmptyEntries);
                    var packageName = parts[0].Trim();

                    if (!projectDeps.ContainsKey(packageName))
                    {
                        projectDeps.Add(packageName, parts.Length == 2 ? parts[1].Trim() : null);
                    }
                }
            }

            return projectDeps;
        }

        public static HashSet<string> GetProjectFileDependencySet(this LockFile lockFile)
        {
            // Get package name from e.g. Microsoft.VSSDK.BuildTools >= 15.0.25604-Preview4
            string GetPackageNameFromDependency(string dependency)
            {
                int indexOfWhiteSpace = IndexOfWhiteSpace(dependency);
                if (indexOfWhiteSpace < 0)
                {
                    return dependency;
                }

                return dependency.Substring(0, indexOfWhiteSpace);
            }

            int IndexOfWhiteSpace(string s)
            {
                for (int i = 0; i < s.Length; i++)
                {
                    if (char.IsWhiteSpace(s[i]))
                    {
                        return i;
                    }
                }

                return -1;
            }

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in lockFile.ProjectFileDependencyGroups)
            {
                foreach (string dependency in group.Dependencies)
                {
                    string packageName = GetPackageNameFromDependency(dependency);
                    set.Add(packageName);
                }
            }

            return set;
        }

        public static HashSet<string> GetPlatformExclusionList(
            this LockFileTarget lockFileTarget,
            LockFileTargetLibrary platformLibrary,
            IDictionary<string, LockFileTargetLibrary> libraryLookup)
        {
            var exclusionList = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            exclusionList.Add(platformLibrary.Name);
            CollectDependencies(libraryLookup, platformLibrary.Dependencies, exclusionList);

            return exclusionList;
        }

        public static HashSet<PackageIdentity> GetTransitivePackagesList(
            this LockFileTarget lockFileTarget,
            LockFileTargetLibrary package,
            IDictionary<string, LockFileTargetLibrary> libraryLookup)
        {
            var exclusionList = new HashSet<PackageIdentity>();

            exclusionList.Add(new PackageIdentity(package.Name, package.Version));
            CollectDependencies(libraryLookup, package.Dependencies, exclusionList);

            return exclusionList;
        }

        private static void CollectDependencies(
            IDictionary<string, LockFileTargetLibrary> libraryLookup,
            IEnumerable<PackageDependency> dependencies,
            HashSet<string> exclusionList)
        {
            var excludedPackages = new HashSet<PackageIdentity>();
            CollectDependencies(libraryLookup, dependencies, excludedPackages);

            foreach (var pkg in excludedPackages)
            {
                exclusionList.Add(pkg.Id);
            }
        }

        private static void CollectDependencies(
            IDictionary<string, LockFileTargetLibrary> libraryLookup,
            IEnumerable<PackageDependency> dependencies,
            HashSet<PackageIdentity> exclusionList)
        {
            foreach (PackageDependency dependency in dependencies)
            {
                LockFileTargetLibrary library = libraryLookup[dependency.Id];
                if (library.Version.Equals(dependency.VersionRange.MinVersion))
                {
                    if (exclusionList.Add(new PackageIdentity(library.Name, library.Version)))
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
                .FilterPlaceholderFiles()
                .Cast<LockFileRuntimeTarget>()
                .Where(t => string.Equals(t.AssetType, assetType, StringComparison.OrdinalIgnoreCase))
                .GroupBy(t => t.Runtime);
        }


        // A package is a TransitiveProjectReference if it is a project, is not directly referenced,
        // and does not contain a placeholder compile time assembly
        public static bool IsTransitiveProjectReference(this LockFileTargetLibrary library, LockFile lockFile, ref HashSet<string> directProjectDependencies)
        {
            if (!library.IsProject())
            {
                return false;
            }

            if (directProjectDependencies == null)
            {
                directProjectDependencies = lockFile.GetProjectFileDependencySet();
            }

            return !directProjectDependencies.Contains(library.Name) 
                && !library.CompileTimeAssemblies.Any(f => f.IsPlaceholderFile());
        }

        public static IEnumerable<LockFileItem> FilterPlaceholderFiles(this IEnumerable<LockFileItem> files)
            => files.Where(f => !f.IsPlaceholderFile());

        public static bool IsPlaceholderFile(this LockFileItem item)
            => NuGetUtils.IsPlaceholderFile(item.Path);

        public static bool IsPackage(this LockFileTargetLibrary library)
            => library.Type == "package";

        public static bool IsPackage(this LockFileLibrary library)
            => library.Type == "package";

        public static bool IsProject(this LockFileTargetLibrary library)
            => library.Type == "project";

        public static bool IsProject(this LockFileLibrary library)
            => library.Type == "project";
    }
}
