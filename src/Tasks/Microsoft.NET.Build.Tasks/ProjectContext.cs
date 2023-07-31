// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using System.Diagnostics;

namespace Microsoft.NET.Build.Tasks
{
    internal class ProjectContext
    {
        public class RuntimeFramework
        {
            public string Name { get; set; }
            public string Version { get; set; }

            public RuntimeFramework() { }

            public RuntimeFramework(ITaskItem item)
            {
                Name = item.ItemSpec;
                Version = item.GetMetadata(MetadataKeys.Version);
            }
        }

        private const string NetCorePlatformLibrary = "Microsoft.NETCore.App";
        private readonly LockFile _lockFile;
        private readonly LockFileTarget _lockFileTarget;
        internal HashSet<PackageIdentity> PackagesToBeFiltered { get; set; }

        /// <summary>
        /// A value indicating that this project runs on a shared system-wide framework.
        /// (ex. Microsoft.NETCore.App for .NET Core)
        /// </summary>
        public bool IsFrameworkDependent { get; }

        /// <summary>
        /// A value indicating that this project is portable across operating systems, processor architectures, etc.
        /// </summary>
        /// <remarks>
        /// Returns <c>true</c> for projects running on shared frameworks (<see cref="IsFrameworkDependent" />)
        /// that do not target a specific RID.
        /// </remarks>
        public bool IsPortable => IsFrameworkDependent && string.IsNullOrEmpty(_lockFileTarget.RuntimeIdentifier);

        public LockFileTargetLibrary PlatformLibrary { get; }

        public RuntimeFramework[] RuntimeFrameworks { get; }

        public LockFile LockFile => _lockFile;
        public LockFileTarget LockFileTarget => _lockFileTarget;

        public LockFileTarget CompilationLockFileTarget { get; }

        public ProjectContext(LockFile lockFile, LockFileTarget lockFileTarget,
            //  Trimmed from publish output, and if there are no runtimeFrameworks, written to runtimeconfig.json
            LockFileTargetLibrary platformLibrary,
            //  Written to runtimeconfig.json
            RuntimeFramework[] runtimeFrameworks,
            bool isFrameworkDependent)
        {
            Debug.Assert(lockFile != null);
            Debug.Assert(lockFileTarget != null);
            if (isFrameworkDependent)
            {
                Debug.Assert(platformLibrary != null || 
                    (runtimeFrameworks != null && runtimeFrameworks.Any()));
            }

            _lockFile = lockFile;
            _lockFileTarget = lockFileTarget;
            if (string.IsNullOrEmpty(lockFileTarget.RuntimeIdentifier))
            {
                CompilationLockFileTarget = lockFileTarget;
            }
            else
            {
                var frameworkAlias = lockFile.GetLockFileTargetAlias(lockFileTarget);
                CompilationLockFileTarget = lockFile.GetTargetAndThrowIfNotFound(frameworkAlias, null);
            }

            PlatformLibrary = platformLibrary;
            RuntimeFrameworks = runtimeFrameworks;
            IsFrameworkDependent = isFrameworkDependent;
        }

        public IEnumerable<LockFileTargetLibrary> GetRuntimeLibraries(IEnumerable<string> excludeFromPublishPackageIds)
        {
            IEnumerable<LockFileTargetLibrary> runtimeLibraries = _lockFileTarget.Libraries;
            Dictionary<string, LockFileTargetLibrary> libraryLookup =
                runtimeLibraries.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);

            HashSet<string> allExclusionList = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (IsFrameworkDependent && PlatformLibrary != null)
            {
                allExclusionList.UnionWith(_lockFileTarget.GetPlatformExclusionList(PlatformLibrary, libraryLookup));

                // If the platform library is not Microsoft.NETCore.App, treat it as an implicit dependency.
                // This makes it so Microsoft.AspNet.* 2.x platforms also exclude Microsoft.NETCore.App files.
                if (PlatformLibrary.Name.Length > 0 && !String.Equals(PlatformLibrary.Name, NetCorePlatformLibrary, StringComparison.OrdinalIgnoreCase))
                {
                    var library = _lockFileTarget.GetLibrary(NetCorePlatformLibrary);
                    if (library != null)
                    {
                        allExclusionList.UnionWith(_lockFileTarget.GetPlatformExclusionList(library, libraryLookup));
                    }
                }
            }

            if (excludeFromPublishPackageIds?.Any() == true)
            {
                HashSet<string> excludeFromPublishList =
                    GetExcludeFromPublishList(
                        excludeFromPublishPackageIds,
                        libraryLookup);

                allExclusionList.UnionWith(excludeFromPublishList);
            }

            if (PackagesToBeFiltered != null)
            {
                var filterLookup = new Dictionary<string, HashSet<PackageIdentity>>(StringComparer.OrdinalIgnoreCase);
                foreach (var pkg in PackagesToBeFiltered)
                {
                    HashSet<PackageIdentity> packageinfos;
                    if (filterLookup.TryGetValue(pkg.Id, out packageinfos))
                    {
                        packageinfos.Add(pkg);
                    }
                    else
                    {
                        packageinfos = new HashSet<PackageIdentity>();
                        packageinfos.Add(pkg);
                        filterLookup.Add(pkg.Id, packageinfos);
                    }
                }

                allExclusionList.UnionWith(GetPackagesToBeFiltered(filterLookup, libraryLookup));
            }

            return runtimeLibraries.Filter(allExclusionList).ToArray();
        }

        internal IEnumerable<PackageIdentity> GetTransitiveList(string package, bool ignoreIfNotFound = false)
        {
            LockFileTargetLibrary platformLibrary = _lockFileTarget.GetLibrary(package);
            if (platformLibrary == null && ignoreIfNotFound)
            {
                return Enumerable.Empty<PackageIdentity>();
            }
            IEnumerable<LockFileTargetLibrary> runtimeLibraries = _lockFileTarget.Libraries;
            Dictionary<string, LockFileTargetLibrary> libraryLookup =
                runtimeLibraries.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);

            return  _lockFileTarget.GetTransitivePackagesList(platformLibrary, libraryLookup);
        }

        public IEnumerable<LockFileTargetLibrary> GetCompileLibraries(IEnumerable<string> compileExcludeFromPublishPackageIds)
        {
            IEnumerable<LockFileTargetLibrary> compileLibraries = _lockFileTarget.Libraries;

            if (compileExcludeFromPublishPackageIds?.Any() == true)
            {
                Dictionary<string, LockFileTargetLibrary> libraryLookup =
                    compileLibraries.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);

                HashSet<string> excludeFromPublishList =
                    GetExcludeFromPublishList(
                        compileExcludeFromPublishPackageIds,
                        libraryLookup);

                compileLibraries = compileLibraries.Filter(excludeFromPublishList);
            }

            return compileLibraries.ToArray();
        }

        public IEnumerable<string> GetTopLevelDependencies()
        {
            return GetTopLevelDependencies(LockFile, LockFileTarget);
        }

        static public IEnumerable<string> GetTopLevelDependencies(LockFile lockFile, LockFileTarget lockFileTarget)
        {
            Dictionary<string, LockFileTargetLibrary> libraryLookup =
                lockFileTarget.Libraries.ToDictionary(l => l.Name, StringComparer.OrdinalIgnoreCase);

            string lockFileTargetFramework = lockFileTarget.Name.Split('/')[0];

            return lockFile
                .ProjectFileDependencyGroups
                .Where(dg => dg.FrameworkName == string.Empty ||
                             dg.FrameworkName == lockFileTargetFramework)
                .SelectMany(g => g.Dependencies)
                .Select(projectFileDependency =>
                {
                    int separatorIndex = projectFileDependency.IndexOf(' ');
                    string libraryName = separatorIndex > 0 ?
                        projectFileDependency.Substring(0, separatorIndex) :
                        projectFileDependency;

                    if (!string.IsNullOrEmpty(libraryName) && libraryLookup.ContainsKey(libraryName))
                    {
                        return libraryName;
                    }

                    return null;
                })
                .Where(libraryName => libraryName != null)
                .ToArray();
        }

        public HashSet<string> GetExcludeFromPublishList(
            IEnumerable<string> excludeFromPublishPackageIds,
            IDictionary<string, LockFileTargetLibrary> libraryLookup)
        {
            var nonExcludeFromPublishAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var nonExcludeFromPublishAssetsToSearch = new Stack<string>();
            var excludeFromPublishAssetsToSearch = new Stack<string>();

            // Start with the top-level dependencies, and put them into "private" or "non-private" buckets
            var excludeFromPublishPackagesLookup = new HashSet<string>(excludeFromPublishPackageIds, StringComparer.OrdinalIgnoreCase);
            foreach (var topLevelDependency in GetTopLevelDependencies())
            {
                if (!excludeFromPublishPackagesLookup.Contains(topLevelDependency))
                {
                    nonExcludeFromPublishAssetsToSearch.Push(topLevelDependency);
                    nonExcludeFromPublishAssets.Add(topLevelDependency);
                }
                else
                {
                    excludeFromPublishAssetsToSearch.Push(topLevelDependency);
                }
            }

            LockFileTargetLibrary library;
            string libraryName;

            // Walk all the non-private assets' dependencies and mark them as non-private
            while (nonExcludeFromPublishAssetsToSearch.Count > 0)
            {
                libraryName = nonExcludeFromPublishAssetsToSearch.Pop();
                if (libraryLookup.TryGetValue(libraryName, out library))
                {
                    foreach (var dependency in library.Dependencies)
                    {
                        if (!nonExcludeFromPublishAssets.Contains(dependency.Id))
                        {
                            nonExcludeFromPublishAssetsToSearch.Push(dependency.Id);
                            nonExcludeFromPublishAssets.Add(dependency.Id);
                        }
                    }
                }
            }

            // Go through assets marked private and their dependencies
            // For libraries not marked as non-private, mark them down as private
            var assetsToExcludeFromPublish = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (excludeFromPublishAssetsToSearch.Count > 0)
            {
                libraryName = excludeFromPublishAssetsToSearch.Pop();
                if (libraryLookup.TryGetValue(libraryName, out library))
                {
                    assetsToExcludeFromPublish.Add(libraryName);

                    foreach (var dependency in library.Dependencies)
                    {
                        if (!nonExcludeFromPublishAssets.Contains(dependency.Id))
                        {
                            excludeFromPublishAssetsToSearch.Push(dependency.Id);
                        }
                    }
                }
            }

            return assetsToExcludeFromPublish;
        }
        private static HashSet<string> GetPackagesToBeFiltered(
          IDictionary<string, HashSet<PackageIdentity>> packagesToBeFiltered,
          IDictionary<string, LockFileTargetLibrary> packagesToBePublished)
        {
            var exclusionList = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in packagesToBePublished)
            {
                HashSet<PackageIdentity> librarySet;

                if (packagesToBeFiltered.TryGetValue(entry.Key, out librarySet))
                {
                    LockFileTargetLibrary dependency = entry.Value;
                    foreach (var library in librarySet)
                    {
                        if (dependency.Version.Equals(library.Version))
                        {
                            exclusionList.Add(entry.Key);
                            break;
                        }
                    }
                }
            }

            return exclusionList;
        }
    }
}
