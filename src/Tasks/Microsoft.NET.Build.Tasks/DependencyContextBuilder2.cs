using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.DependencyModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace Microsoft.NET.Build.Tasks
{
    internal class DependencyContextBuilder2
    {
        private readonly SingleProjectInfo _mainProjectInfo;
        private readonly bool _includeRuntimeFileVersions;
        private IEnumerable<ReferenceInfo> _referenceAssemblies;
        private IEnumerable<ReferenceInfo> _directReferences;
        private Dictionary<string, SingleProjectInfo> _referenceProjectInfos;
        private IEnumerable<string> _excludeFromPublishPackageIds;
        private Dictionary<string, List<RuntimePackAssetInfo>> _runtimePackAssets;
        private bool _includeMainProjectInDepsFile = true;
        private Dictionary<string, DependencyLibrary> _dependencyLibraries;
        private Dictionary<string, List<LibraryDependency>> _libraryDependencies;
        private List<string> _mainProjectDependencies;
        private HashSet<PackageIdentity> _packagesToBeFiltered;
        private bool _isFrameworkDependent;
        private string _platformLibrary;
        private HashSet<string> _usedLibraryNames;

        private Dictionary<ReferenceInfo, string> _referenceLibraryNames;

        public DependencyContextBuilder2(SingleProjectInfo mainProjectInfo, ProjectContext projectContext, bool includeRuntimeFileVersions)
        {
            _mainProjectInfo = mainProjectInfo;
            _includeRuntimeFileVersions = includeRuntimeFileVersions;

            var libraryLookup = projectContext.LockFile.Libraries.ToDictionary(l => l.Name, StringComparer.OrdinalIgnoreCase);

            _dependencyLibraries = projectContext.LockFileTarget.Libraries
                .Select(lockFileTargetLibrary =>
                {
                    var dependencyLibrary = new DependencyLibrary(lockFileTargetLibrary.Name, lockFileTargetLibrary.Version, lockFileTargetLibrary.Type);

                    if (libraryLookup.TryGetValue(dependencyLibrary.Name, out var library))
                    {
                        dependencyLibrary.Sha512 = library.Sha512;
                        dependencyLibrary.Path = library.Path;
                        dependencyLibrary.MSBuildProject = library.MSBuildProject;
                    }

                    return dependencyLibrary;
                }).ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);

            _libraryDependencies = new Dictionary<string, List<LibraryDependency>>(StringComparer.OrdinalIgnoreCase);
            foreach (var library in projectContext.LockFileTarget.Libraries)
            {
                _libraryDependencies[library.Name] = library.Dependencies
                    .Select(d => new LibraryDependency()
                    {
                        Name = d.Id,
                        MinVersion = d.VersionRange.MinVersion
                    }).ToList();
            }

            _mainProjectDependencies = projectContext.GetTopLevelDependencies().ToList();
            _packagesToBeFiltered = projectContext.PackagesToBeFiltered;

            _isFrameworkDependent = projectContext.IsFrameworkDependent;
            _platformLibrary = projectContext.PlatformLibrary?.Name;

            _usedLibraryNames = new HashSet<string>(_dependencyLibraries.Keys, StringComparer.OrdinalIgnoreCase);
        }

        private Dictionary<ReferenceInfo, string> ReferenceLibraryNames
        {
            get
            {
                if (_referenceLibraryNames == null)
                {
                    _referenceLibraryNames = new Dictionary<ReferenceInfo, string>();
                }

                return _referenceLibraryNames;
            }
        }

        public DependencyContextBuilder2 WithReferenceAssemblies(IEnumerable<ReferenceInfo> referenceAssemblies)
        {
            // note: ReferenceAssembly libraries only export compile-time stuff
            // since they assume the runtime library is present already
            _referenceAssemblies = referenceAssemblies;
            return this;
        }

        public DependencyContextBuilder2 WithDirectReferences(IEnumerable<ReferenceInfo> directReferences)
        {
            _directReferences = directReferences;
            return this;
        }

        public DependencyContextBuilder2 WithReferenceProjectInfos(Dictionary<string, SingleProjectInfo> referenceProjectInfos)
        {
            _referenceProjectInfos = referenceProjectInfos;
            return this;
        }

        public DependencyContextBuilder2 WithMainProjectInDepsFile(bool includeMainProjectInDepsFile)
        {
            _includeMainProjectInDepsFile = includeMainProjectInDepsFile;
            return this;
        }

        public DependencyContextBuilder2 WithExcludeFromPublishAssets(IEnumerable<string> excludeFromPublishPackageIds)
        {
            _excludeFromPublishPackageIds = excludeFromPublishPackageIds;
            return this;
        }

        public DependencyContextBuilder2 WithRuntimePackAssets(IEnumerable<RuntimePackAssetInfo> runtimePackAssets)
        {
            _runtimePackAssets = new Dictionary<string, List<RuntimePackAssetInfo>>();
            foreach (var runtimePackGroup in runtimePackAssets.GroupBy(a => a.PackageName))
            {
                var dependencyLibrary = new DependencyLibrary(runtimePackGroup.Key,
                    NuGetVersion.Parse(runtimePackGroup.First().PackageVersion),
                    "runtimepack");

                _dependencyLibraries.Add(dependencyLibrary.Name, dependencyLibrary);

                _runtimePackAssets[dependencyLibrary.Name] = runtimePackGroup.ToList();
            }
            return this;
        }

        public DependencyContext Build()
        {
            List<RuntimeLibrary> runtimeLibraries = new List<RuntimeLibrary>();

            if (_includeMainProjectInDepsFile)
            {
                runtimeLibraries.Add(GetProjectRuntimeLibrary());
            }

            runtimeLibraries.AddRange(GetRuntimePackLibraries());

            // This resolver is only used for building file names, so that base path is not required.
            var versionFolderPathResolver = new VersionFolderPathResolver(rootPath: null);

            foreach (var library in GetFilteredLibraries())
            {
                bool serviceable = true;

                var libraryDependencies = new HashSet<Dependency>();
                foreach (var dependency in _libraryDependencies[library.Name])
                {
                    if (_dependencyLibraries.TryGetValue(library.Name, out var libraryDependency))
                    {
                        libraryDependencies.Add(libraryDependency.Dependency);
                    }
                }

                string hash = string.Empty;
                string path = null;
                string hashPath = null;
                SingleProjectInfo referenceProjectInfo = null;

                if (library.Type == "package")
                {
                    // TEMPORARY: All packages are serviceable in RC2
                    // See https://github.com/dotnet/cli/issues/2569
                    serviceable = true;
                    if (!string.IsNullOrEmpty(library.Sha512))
                    {
                        hash = "sha512-" + library.Sha512;
                        hashPath = versionFolderPathResolver.GetHashFileName(library.Name, library.Version);
                    }

                    path = library.Path;
                }
                else if (library.Type == "project")
                {
                    serviceable = false;
                    referenceProjectInfo = GetProjectInfo(library);
                    if (referenceProjectInfo is UnreferencedProjectInfo)
                    {
                        // unreferenced ProjectInfos will be added later as simple dll dependencies
                        continue;
                    }

                    foreach (var dependencyReference in referenceProjectInfo.DependencyReferences)
                    {
                        libraryDependencies.Add(
                            new Dependency(
                                GetReferenceLibraryName(dependencyReference),
                                dependencyReference.Version));
                    }
                }

                List<RuntimeAssetGroup> runtimeAssemblyGroups = new List<RuntimeAssetGroup>();
                if (library.Type == "project" && !(referenceProjectInfo is UnreferencedProjectInfo))
                {
                    runtimeAssemblyGroups.Add(new RuntimeAssetGroup(string.Empty, referenceProjectInfo.OutputName));
                }
                else
                {
                    //runtimeAssemblyGroups.Add(new RuntimeAssetGroup(string.Empty,))
                }

                var runtimeLibrary = new RuntimeLibrary(
                    type: library.Type,
                    name: library.Name,
                    version: library.Version.ToString(),
                    hash: hash,
                    runtimeAssemblyGroups: runtimeAssemblyGroups,
                    nativeLibraryGroups: nativeLibraryGroups,
                    resourceAssemblies: resourceAssemblies,
                    dependencies: libraryDependencies,
                    path: path,
                    hashPath: hashPath,
                    runtimeStoreManifestName: GetRuntimeStoreManifestName(library.Name, library.Version.ToString()),
                    serviceable: serviceable);
            }

            throw new NotImplementedException();
        }

        private RuntimeLibrary GetProjectRuntimeLibrary()
        {
            RuntimeAssetGroup[] runtimeAssemblyGroups = new[] { new RuntimeAssetGroup(string.Empty, _mainProjectInfo.OutputName) };

            List<Dependency> dependencies = new List<Dependency>();
            foreach (var dependencyName in _mainProjectDependencies)
            {
                if (_dependencyLibraries.TryGetValue(dependencyName, out var dependencyLibrary))
                {
                    dependencies.Add(dependencyLibrary.Dependency);
                }
            }

            if (_directReferences != null)
            {
                foreach (var directReference in _directReferences)
                {
                    dependencies.Add(
                        new Dependency(
                            GetReferenceLibraryName(directReference),
                            directReference.Version));
                }
            }
            if (_runtimePackAssets != null)
            {
                foreach (var runtimePackName in _runtimePackAssets.Keys)
                {
                    dependencies.Add(_dependencyLibraries[runtimePackName].Dependency);
                }
            }

            return new RuntimeLibrary(
                type: "project",
                name: _mainProjectInfo.Name,
                version: _mainProjectInfo.Version,
                hash: string.Empty,
                runtimeAssemblyGroups: runtimeAssemblyGroups,
                nativeLibraryGroups: Array.Empty<RuntimeAssetGroup>(),
                resourceAssemblies: CreateResourceAssemblies(_mainProjectInfo.ResourceAssemblies),
                dependencies: dependencies,
                path: null,
                hashPath: null,
                runtimeStoreManifestName: GetRuntimeStoreManifestName(_mainProjectInfo.Name, _mainProjectInfo.Version),
                serviceable: false);
        }

        private IEnumerable<RuntimeLibrary> GetRuntimePackLibraries()
        {
            if (_runtimePackAssets == null)
            {
                return Enumerable.Empty<RuntimeLibrary>();
            }
            return _runtimePackAssets.Select(runtimePack =>
            {
                var runtimeAssemblyGroup = new RuntimeAssetGroup(string.Empty,
                    runtimePack.Value.Where(asset => asset.AssetType == AssetType.Runtime)
                    .Select(asset => CreateRuntimeFile(asset.DestinationSubPath, asset.SourcePath)));

                var nativeLibraryGroup = new RuntimeAssetGroup(string.Empty,
                    runtimePack.Value.Where(asset => asset.AssetType == AssetType.Native)
                    .Select(asset => CreateRuntimeFile(asset.DestinationSubPath, asset.SourcePath)));

                return new RuntimeLibrary(
                    type: "runtimepack",
                    name: runtimePack.Key,
                    version: runtimePack.Value.First().PackageVersion,
                    hash: string.Empty,
                    runtimeAssemblyGroups: new[] { runtimeAssemblyGroup },
                    nativeLibraryGroups: new[] { nativeLibraryGroup },
                    resourceAssemblies: Enumerable.Empty<ResourceAssembly>(),
                    dependencies: Enumerable.Empty<Dependency>(),
                    serviceable: false);
            });
        }

        private RuntimeFile CreateRuntimeFile(string path, string fullPath)
        {
            if (_includeRuntimeFileVersions)
            {
                string fileVersion = FileUtilities.GetFileVersion(fullPath).ToString();
                string assemblyVersion = FileUtilities.TryGetAssemblyVersion(fullPath)?.ToString();
                return new RuntimeFile(path, assemblyVersion, fileVersion);
            }
            else
            {
                return new RuntimeFile(path, null, null);
            }
        }

        private static IEnumerable<ResourceAssembly> CreateResourceAssemblies(IEnumerable<ResourceAssemblyInfo> resourceAssemblyInfos)
        {
            return resourceAssemblyInfos
                .Select(r => new ResourceAssembly(r.RelativePath, r.Culture));
        }

        private SingleProjectInfo GetProjectInfo(DependencyLibrary library)
        {
            string projectPath = library.MSBuildProject;
            if (string.IsNullOrEmpty(projectPath))
            {
                throw new BuildErrorException(Strings.CannotFindProjectInfo, library.Name);
            }

            string mainProjectDirectory = Path.GetDirectoryName(_mainProjectInfo.ProjectPath);
            string fullProjectPath = Path.GetFullPath(Path.Combine(mainProjectDirectory, projectPath));

            SingleProjectInfo referenceProjectInfo = null;
            if (_referenceProjectInfos?.TryGetValue(fullProjectPath, out referenceProjectInfo) != true ||
                referenceProjectInfo == null)
            {
                return UnreferencedProjectInfo.Default;
            }

            return referenceProjectInfo;
        }

        private IEnumerable<DependencyLibrary> GetFilteredLibraries()
        {
            Dictionary<string, DependencyLibrary> libraries = _dependencyLibraries;

            HashSet<string> allExclusionList = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (_isFrameworkDependent && !string.IsNullOrEmpty(_platformLibrary))
            {
                //  Exclude platform library and dependencies.
                allExclusionList.Add(_platformLibrary);

                Stack<LibraryDependency> dependenciesToWalk = new Stack<LibraryDependency>(_libraryDependencies[_platformLibrary]);

                while (dependenciesToWalk.Any())
                {
                    var dependency = dependenciesToWalk.Pop();
                    if (allExclusionList.Contains(dependency.Name))
                    {
                        continue;
                    }

                    //  Resolved version of library has to match dependency version exactly, so that we
                    //  don't exclude newer versions of libraries that are part of the platform
                    if (_dependencyLibraries[dependency.Name].Version == dependency.MinVersion)
                    {
                        allExclusionList.Add(dependency.Name);
                        foreach (var newDependency in _libraryDependencies[dependency.Name])
                        {
                            dependenciesToWalk.Push(newDependency);
                        }
                    }
                }
            }

            if (_packagesToBeFiltered != null)
            {
                foreach (var packageToFilter in _packagesToBeFiltered)
                {
                    if (_dependencyLibraries.TryGetValue(packageToFilter.Id, out var library))
                    {
                        if (library.Type == "package" &&
                            _dependencyLibraries[packageToFilter.Id].Version == packageToFilter.Version)
                        {
                            allExclusionList.Add(packageToFilter.Id);
                        }
                    }
                }
            }

            if (_excludeFromPublishPackageIds != null)
            {
                //  Include transitive dependencies of all top-level dependencies which are not
                //  excluded from publish

                Dictionary<string, DependencyLibrary> includedDependencies = new Dictionary<string, DependencyLibrary>();

                Stack<string> dependenciesToWalk = new Stack<string>(
                    _mainProjectDependencies.Except(_excludeFromPublishPackageIds, StringComparer.OrdinalIgnoreCase));

                while (dependenciesToWalk.Any())
                {
                    var dependencyName = dependenciesToWalk.Pop();
                    if (!includedDependencies.ContainsKey(dependencyName))
                    {
                        includedDependencies.Add(dependencyName, _dependencyLibraries[dependencyName]);
                        foreach (var newDependency in _libraryDependencies[dependencyName])
                        {
                            dependenciesToWalk.Push(newDependency.Name);
                        }
                    }
                }

                libraries = includedDependencies;
            }

            if (allExclusionList.Any())
            {
                return libraries.Values.Where(l => !allExclusionList.Contains(l.Name)).ToList();
            }
            else
            {
                return libraries.Values;
            }
        }

        private string GetReferenceLibraryName(ReferenceInfo reference)
        {
            if (!ReferenceLibraryNames.TryGetValue(reference, out string name))
            {
                // Reference names can conflict with PackageReference names, so
                // ensure that the Reference names are unique when creating libraries
                name = GetUniqueReferenceName(reference.Name);

                ReferenceLibraryNames.Add(reference, name);
                _usedLibraryNames.Add(name);
            }

            return name;
        }

        private string GetUniqueReferenceName(string name)
        {
            if (_usedLibraryNames.Contains(name))
            {
                string startingName = $"{name}.Reference";
                name = startingName;

                int suffix = 1;
                while (_usedLibraryNames.Contains(name))
                {
                    name = $"{startingName}{suffix++}";
                }
            }

            return name;
        }

        private string GetRuntimeStoreManifestName(string packageName, string packageVersion)
        {
            throw new NotImplementedException();
        }

        private class DependencyLibrary
        {
            public string Name { get; }
            public NuGetVersion Version { get; }
            public string Type { get; }
            public Dependency Dependency { get; }
            public string Sha512 { get; set; }
            public string Path { get; set; }
            public string MSBuildProject { get; set; }

            public DependencyLibrary(string name, NuGetVersion version, string type)
            {
                Name = name;
                Version = version;
                Type = type;
                Dependency = new Dependency(name, version.ToString());
            }
        }

        private struct LibraryDependency
        {
            public string Name { get; set; }
            public NuGetVersion MinVersion { get; set; }
        }
    }
}
