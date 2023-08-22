// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;

namespace Microsoft.NET.Build.Tasks
{
    internal static class LockFileExtensions
    {
        public static LockFileTarget GetTargetAndReturnNullIfNotFound(this LockFile lockFile, string frameworkAlias, string runtimeIdentifier)
        {
            LockFileTarget lockFileTarget = lockFile.GetTarget(frameworkAlias, runtimeIdentifier);

            if (lockFileTarget == null &&
                lockFile.PackageSpec.TargetFrameworks.All(tfi => string.IsNullOrEmpty(tfi.TargetAlias)))
            {
                var nuGetFramework = NuGetUtils.ParseFrameworkName(frameworkAlias);
                lockFileTarget = lockFile.GetTarget(nuGetFramework, runtimeIdentifier);
            }

            return lockFileTarget;
        }

        public static LockFileTarget GetTargetAndThrowIfNotFound(this LockFile lockFile, string frameworkAlias, string runtimeIdentifier)
        {
            LockFileTarget lockFileTarget = lockFile.GetTargetAndReturnNullIfNotFound(frameworkAlias, runtimeIdentifier);

            if (lockFileTarget == null)
            {
                string frameworkString = frameworkAlias;
                string targetMoniker = string.IsNullOrEmpty(runtimeIdentifier) ?
                    frameworkString :
                    $"{frameworkString}/{runtimeIdentifier}";

                string message;
                if (string.IsNullOrEmpty(runtimeIdentifier))
                {
                    message = string.Format(Strings.AssetsFileMissingTarget, lockFile.Path, targetMoniker, frameworkString);
                }
                else
                {
                    message = string.Format(Strings.AssetsFileMissingRuntimeIdentifier, lockFile.Path, targetMoniker, frameworkString, runtimeIdentifier);
                }

                throw new BuildErrorException(message);
            }

            return lockFileTarget;
        }

        public static string GetLockFileTargetAlias(this LockFile lockFile, LockFileTarget lockFileTarget)
        {
            var frameworkAlias = lockFile.PackageSpec.TargetFrameworks.FirstOrDefault(tfi => tfi.FrameworkName == lockFileTarget.TargetFramework)?.TargetAlias;
            if (frameworkAlias == null)
            {
                throw new ArgumentException("Could not find TargetFramework alias in lock file for " + lockFileTarget.TargetFramework);
            }
            return frameworkAlias;
        }

        public static ProjectContext CreateProjectContext(
            this LockFile lockFile,
            string frameworkAlias,
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
            if (frameworkAlias == null)
            {
                throw new ArgumentNullException(nameof(frameworkAlias));
            }

            var lockFileTarget = lockFile.GetTargetAndThrowIfNotFound(frameworkAlias, runtime);

            LockFileTargetLibrary platformLibrary = lockFileTarget.GetLibrary(platformLibraryName);
            bool isFrameworkDependent = IsFrameworkDependent(runtimeFrameworks, isSelfContained, lockFileTarget.RuntimeIdentifier, platformLibrary != null);

            return new ProjectContext(lockFile, lockFileTarget, platformLibrary,
                runtimeFrameworks?.Select(i => new ProjectContext.RuntimeFramework(i))?.ToArray(),
                isFrameworkDependent);
        }

        public static bool IsFrameworkDependent(ITaskItem[] runtimeFrameworks, bool isSelfContained, string runtimeIdentifier, bool hasPlatformLibrary)
        {
            return (hasPlatformLibrary || runtimeFrameworks?.Any() == true) &&
                (!isSelfContained || string.IsNullOrEmpty(runtimeIdentifier));
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

        public static HashSet<string> GetProjectFileDependencySet(this LockFile lockFile, string frameworkAlias)
        {
            // Get package name from e.g. Microsoft.VSSDK.BuildTools >= 15.0.25604-Preview4
            static string GetPackageNameFromDependency(string dependency)
            {
                int indexOfWhiteSpace = IndexOfWhiteSpace(dependency);
                if (indexOfWhiteSpace < 0)
                {
                    return dependency;
                }

                return dependency.Substring(0, indexOfWhiteSpace);
            }

            static int IndexOfWhiteSpace(string s)
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
                var groupFrameworkAlias = GetFrameworkAliasForDependencyGroup(group);
                if (string.IsNullOrEmpty(groupFrameworkAlias) || string.IsNullOrEmpty(frameworkAlias) || groupFrameworkAlias.Equals(frameworkAlias) ||
                    NuGetUtils.ParseFrameworkName(groupFrameworkAlias.Split('/').First()).DotNetFrameworkName.Equals(NuGetUtils.ParseFrameworkName(frameworkAlias).DotNetFrameworkName))
                {
                    foreach (string dependency in group.Dependencies)
                    {
                        string packageName = GetPackageNameFromDependency(dependency);
                        set.Add(packageName);
                    }
                }
            }

            return set;
        }

        private static string GetFrameworkAliasForDependencyGroup(ProjectFileDependencyGroup group)
        {
            return group.FrameworkName;
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
        public static bool IsTransitiveProjectReference(this LockFileTargetLibrary library, LockFile lockFile, ref HashSet<string> directProjectDependencies, string frameworkAlias)
        {
            if (!library.IsProject())
            {
                return false;
            }

            if (directProjectDependencies == null)
            {
                directProjectDependencies = lockFile.GetProjectFileDependencySet(frameworkAlias);
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
