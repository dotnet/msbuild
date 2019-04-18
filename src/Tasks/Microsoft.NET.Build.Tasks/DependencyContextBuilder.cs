// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Versioning;

namespace Microsoft.NET.Build.Tasks
{
    internal class DependencyContextBuilder
    {
        private const string _runtimePackPrefix = "runtimepack.";

        private readonly VersionFolderPathResolver _versionFolderPathResolver;
        private readonly SingleProjectInfo _mainProjectInfo;
        private readonly ProjectContext _projectContext;
        private readonly bool _includeRuntimeFileVersions;
        private readonly NuGetPackageResolver _packageResolver;
        private IEnumerable<ReferenceInfo> _referenceAssemblies;
        private IEnumerable<ReferenceInfo> _directReferences;
        private IEnumerable<ReferenceInfo> _dependencyReferences;
        private Dictionary<string, SingleProjectInfo> _referenceProjectInfos;
        private IEnumerable<string> _excludeFromPublishPackageIds;
        private IEnumerable<RuntimePackAssetInfo> _runtimePackAssets = Enumerable.Empty<RuntimePackAssetInfo>();
        private CompilationOptions _compilationOptions;
        private string _referenceAssembliesPath;
        private Dictionary<PackageIdentity, string> _filteredPackages;
        private bool _includeMainProjectInDepsFile = true;
        private HashSet<string> _usedLibraryNames;
        private Dictionary<ReferenceInfo, string> _referenceLibraryNames;
        private Dictionary<string, LockFileTargetLibrary> _compilationTargetLibraries;

        public DependencyContextBuilder(SingleProjectInfo mainProjectInfo, ProjectContext projectContext, bool includeRuntimeFileVersions)
        {
            _mainProjectInfo = mainProjectInfo;
            _projectContext = projectContext;
            _includeRuntimeFileVersions = includeRuntimeFileVersions;

            // This resolver is only used for building file names, so that base path is not required.
            _versionFolderPathResolver = new VersionFolderPathResolver(rootPath: null);

            if (_includeRuntimeFileVersions)
            {
                //  This is used to look up the paths to package files on disk, which is only needed in this class if
                //  it needs to read the file versions
                _packageResolver = NuGetPackageResolver.CreateResolver(projectContext.LockFile);
            }

            if (_projectContext.CompilationLockFileTarget != _projectContext.LockFileTarget)
            {
                _compilationTargetLibraries = _projectContext.CompilationLockFileTarget.Libraries
                    .ToDictionary(l => l.Name, StringComparer.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Keeps track of the Library names being used in the DependencyContext.
        /// </summary>
        /// <remarks>
        /// Since `Reference` and `PackageReference` names can conflict, we need to ensure
        /// each separate Library has a unique name. Since PackageReference names are guaranteed
        /// to be unique amongst other PackageReferences, start with that set, and ensure
        /// Reference names are unique amongst all.
        /// </remarks>
        private HashSet<string> UsedLibraryNames
        {
            get
            {
                if (_usedLibraryNames == null)
                {
                    _usedLibraryNames = new HashSet<string>(
                        _projectContext.LockFile.Libraries.Select(l => l.Name),
                        StringComparer.OrdinalIgnoreCase);
                }

                return _usedLibraryNames;
            }
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

        public DependencyContextBuilder WithMainProjectInDepsFile(bool includeMainProjectInDepsFile)
        {
            _includeMainProjectInDepsFile = includeMainProjectInDepsFile;
            return this;
        }

        public DependencyContextBuilder WithReferenceAssemblies(IEnumerable<ReferenceInfo> referenceAssemblies)
        {
            // note: ReferenceAssembly libraries only export compile-time stuff
            // since they assume the runtime library is present already
            _referenceAssemblies = referenceAssemblies;
            return this;
        }

        public DependencyContextBuilder WithDirectReferences(IEnumerable<ReferenceInfo> directReferences)
        {
            _directReferences = directReferences;
            return this;
        }

        public DependencyContextBuilder WithDependencyReferences(IEnumerable<ReferenceInfo> dependencyReferences)
        {
            _dependencyReferences = dependencyReferences;
            return this;
        }

        public DependencyContextBuilder WithReferenceProjectInfos(Dictionary<string, SingleProjectInfo> referenceProjectInfos)
        {
            _referenceProjectInfos = referenceProjectInfos;
            return this;
        }

        public DependencyContextBuilder WithExcludeFromPublishAssets(IEnumerable<string> excludeFromPublishPackageIds)
        {
            _excludeFromPublishPackageIds = excludeFromPublishPackageIds;
            return this;
        }

        public DependencyContextBuilder WithRuntimePackAssets(IEnumerable<RuntimePackAssetInfo> runtimePackAssets)
        {
            _runtimePackAssets = runtimePackAssets;
            return this;
        }

        public DependencyContextBuilder WithCompilationOptions(CompilationOptions compilationOptions)
        {
            _compilationOptions = compilationOptions;
            return this;
        }

        public DependencyContextBuilder WithReferenceAssembliesPath(string referenceAssembliesPath)
        {
            _referenceAssembliesPath = EnsureTrailingSlash(referenceAssembliesPath);
            return this;
        }

        public DependencyContextBuilder WithPackagesThatWhereFiltered(Dictionary<PackageIdentity, string> packagesThatWhereFiltered)
        {
            _filteredPackages = packagesThatWhereFiltered;
            return this;
        }

        public DependencyContext Build()
        {
            bool includeCompilationLibraries = _compilationOptions != null;

            IEnumerable<LockFileTargetLibrary> runtimeExports = _projectContext.GetRuntimeLibraries(_excludeFromPublishPackageIds);
            IEnumerable<LockFileTargetLibrary> compilationExports =
                includeCompilationLibraries ?
                    _projectContext.GetCompileLibraries(_excludeFromPublishPackageIds) :
                    Enumerable.Empty<LockFileTargetLibrary>();

            var dependencyLookup = compilationExports
                .Concat(runtimeExports)
                .Distinct()
                .Select(library => new Dependency(library.Name, library.Version.ToString()))
                .ToDictionary(dependency => dependency.Name, StringComparer.OrdinalIgnoreCase);

            var libraryLookup = new LockFileLookup(_projectContext.LockFile);

            var runtimeSignature = string.Empty;

            IEnumerable<RuntimeLibrary> runtimeLibraries = Enumerable.Empty<RuntimeLibrary>();
            if (_includeMainProjectInDepsFile)
            {
                runtimeLibraries = runtimeLibraries.Concat(new[]
                {
                    GetProjectRuntimeLibrary(
                        _mainProjectInfo,
                        _projectContext,
                        dependencyLookup,
                        includeCompilationLibraries)
                });
            }
            runtimeLibraries = runtimeLibraries
                .Concat(GetRuntimePackLibraries(_runtimePackAssets))
                .Concat(GetLibraries(runtimeExports, libraryLookup, dependencyLookup, runtime: true).Cast<RuntimeLibrary>())
                .Concat(GetDirectReferenceRuntimeLibraries())
                .Concat(GetDependencyReferenceRuntimeLibraries());

            IEnumerable<CompilationLibrary> compilationLibraries = Enumerable.Empty<CompilationLibrary>();
            if (includeCompilationLibraries)
            {
                if (_includeMainProjectInDepsFile)
                {
                    compilationLibraries = compilationLibraries.Concat(new[]
                    {
                        GetProjectCompilationLibrary(
                            _mainProjectInfo,
                            _projectContext,
                            dependencyLookup,
                            includeCompilationLibraries)
                    });
                }

                compilationLibraries = compilationLibraries
                    .Concat(GetReferenceAssemblyLibraries())
                    .Concat(GetLibraries(compilationExports, libraryLookup, dependencyLookup, runtime: false).Cast<CompilationLibrary>())
                    .Concat(GetDirectReferenceCompilationLibraries());
            }

            var targetInfo = new TargetInfo(
                _projectContext.LockFileTarget.TargetFramework.DotNetFrameworkName,
                _projectContext.LockFileTarget.RuntimeIdentifier,
                runtimeSignature,
                _projectContext.IsPortable);

            return new DependencyContext(
                targetInfo,
                _compilationOptions ?? CompilationOptions.Default,
                compilationLibraries,
                runtimeLibraries,
                new RuntimeFallbacks[] { });
        }

        private List<Dependency> GetProjectDependencies(
            ProjectContext projectContext,
            Dictionary<string, Dependency> dependencyLookup,
            bool includeCompilationLibraries)
        {
            List<Dependency> dependencies = new List<Dependency>();

            foreach (string dependencyName in projectContext.GetTopLevelDependencies())
            {
                Dependency dependency;
                if (dependencyLookup.TryGetValue(dependencyName, out dependency))
                {
                    dependencies.Add(dependency);
                }
            }

            var referenceInfos = Enumerable.Concat(
                includeCompilationLibraries && _referenceAssemblies != null ? 
                    _referenceAssemblies : 
                    Enumerable.Empty<ReferenceInfo>(),
                _directReferences ?? Enumerable.Empty<ReferenceInfo>());

            foreach (ReferenceInfo referenceInfo in referenceInfos)
            {
                dependencies.Add(
                    new Dependency(
                        GetReferenceLibraryName(referenceInfo), 
                        referenceInfo.Version));
            }

            return dependencies;
        }

        private RuntimeLibrary CreateRuntimeLibrary(
            string type,
            string name,
            string version,
            string hash,
            IReadOnlyList<RuntimeAssetGroup> runtimeAssemblyGroups,
            IReadOnlyList<RuntimeAssetGroup> nativeLibraryGroups,
            IEnumerable<ResourceAssembly> resourceAssemblies,
            IEnumerable<Dependency> dependencies,
            bool serviceable,
            string path = null,
            string hashPath = null)
        {
            string runtimeStoreManifestName = null;
            var pkg = new PackageIdentity(name, NuGetVersion.Parse(version));
            _filteredPackages?.TryGetValue(pkg, out runtimeStoreManifestName);

            return new RuntimeLibrary(
                type,
                name: name,
                version: version,
                hash: hash,
                runtimeAssemblyGroups: runtimeAssemblyGroups,
                nativeLibraryGroups: nativeLibraryGroups,
                resourceAssemblies: resourceAssemblies,
                dependencies: dependencies,
                path: path,
                hashPath: hashPath,
                runtimeStoreManifestName: runtimeStoreManifestName,
                serviceable: serviceable);
        }

        private RuntimeLibrary GetProjectRuntimeLibrary(
            SingleProjectInfo projectInfo,
            ProjectContext projectContext,
            Dictionary<string, Dependency> dependencyLookup,
            bool includeCompilationLibraries)
        {
            RuntimeAssetGroup[] runtimeAssemblyGroups = new[] { new RuntimeAssetGroup(string.Empty, projectInfo.OutputName) };

            List<Dependency> dependencies = GetProjectDependencies(projectContext, dependencyLookup, includeCompilationLibraries);
            foreach (var runtimePackGroup in _runtimePackAssets.GroupBy(asset => asset.PackageName + "/" + asset.PackageVersion))
            {
                dependencies.Add(new Dependency(_runtimePackPrefix + runtimePackGroup.First().PackageName, runtimePackGroup.First().PackageVersion));
            }

            return CreateRuntimeLibrary(
                type: "project",
                name: projectInfo.Name,
                version: projectInfo.Version,
                hash: string.Empty,
                runtimeAssemblyGroups: runtimeAssemblyGroups,
                nativeLibraryGroups: new RuntimeAssetGroup[] { },
                resourceAssemblies: CreateResourceAssemblies(projectInfo.ResourceAssemblies),
                dependencies: dependencies.ToArray(),
                serviceable: false);
        }

        private CompilationLibrary GetProjectCompilationLibrary(
            SingleProjectInfo projectInfo,
            ProjectContext projectContext,
            Dictionary<string, Dependency> dependencyLookup,
            bool includeCompilationLibraries)
        {
            List<Dependency> dependencies = GetProjectDependencies(projectContext, dependencyLookup, includeCompilationLibraries);

            return new CompilationLibrary(
                type: "project",
                name: projectInfo.Name,
                version: projectInfo.Version,
                hash: string.Empty,
                assemblies: new[] { projectInfo.OutputName },
                dependencies: dependencies.ToArray(),
                serviceable: false);
        }

        private IEnumerable<RuntimeLibrary> GetRuntimePackLibraries(IEnumerable<RuntimePackAssetInfo> runtimePackAssets)
        {
            return runtimePackAssets.GroupBy(asset => asset.PackageName + "/" + asset.PackageVersion).Select(
                runtimePackAssetGroup =>
                {
                    List<RuntimeAssetGroup> runtimeAssemblyGroups = new List<RuntimeAssetGroup>()
                    {
                        new RuntimeAssetGroup(string.Empty,
                            runtimePackAssetGroup.Where(asset => asset.AssetType == AssetType.Runtime)
                            .Select(asset => CreateRuntimeFile(asset.DestinationSubPath, asset.SourcePath)))
                    };
                    List<RuntimeAssetGroup> nativeLibraryGroups = new List<RuntimeAssetGroup>()
                    {
                        new RuntimeAssetGroup(string.Empty,
                            runtimePackAssetGroup.Where(asset => asset.AssetType == AssetType.Native)
                            .Select(asset => CreateRuntimeFile(asset.DestinationSubPath, asset.SourcePath)))
                    };
                    
                    return new RuntimeLibrary("runtimepack",
                        _runtimePackPrefix + runtimePackAssetGroup.First().PackageName,
                        runtimePackAssetGroup.First().PackageVersion,
                        hash: string.Empty,
                        runtimeAssemblyGroups,
                        nativeLibraryGroups,
                        resourceAssemblies: Enumerable.Empty<ResourceAssembly>(),
                        dependencies: Enumerable.Empty<Dependency>(),
                        serviceable: false);
                });
        }

        private IEnumerable<Library> GetLibraries(
            IEnumerable<LockFileTargetLibrary> exports,
            LockFileLookup libraryLookup,
            IDictionary<string, Dependency> dependencyLookup,
            bool runtime)
        {
            return exports.Select(export => GetLibrary(export, libraryLookup, dependencyLookup, runtime)).Where(l => l != null);
        }

        private Library GetLibrary(
            LockFileTargetLibrary export,
            LockFileLookup libraryLookup,
            IDictionary<string, Dependency> dependencyLookup,
            bool runtime)
        {
            var type = export.Type;
            bool isPackage = export.IsPackage();

            // TEMPORARY: All packages are serviceable in RC2
            // See https://github.com/dotnet/cli/issues/2569
            var serviceable = isPackage;
            var libraryDependencies = new HashSet<Dependency>();

            foreach (PackageDependency libraryDependency in export.Dependencies)
            {
                Dependency dependency;
                if (dependencyLookup.TryGetValue(libraryDependency.Id, out dependency))
                {
                    libraryDependencies.Add(dependency);
                }
            }

            string hash = string.Empty;
            string path = null;
            string hashPath = null;
            LockFileLibrary library;
            SingleProjectInfo referenceProjectInfo = null;
            if (libraryLookup.TryGetLibrary(export, out library))
            {
                if (isPackage)
                {
                    if (!string.IsNullOrEmpty(library.Sha512))
                    {
                        hash = "sha512-" + library.Sha512;
                        hashPath = _versionFolderPathResolver.GetHashFileName(export.Name, export.Version);
                    }

                    path = library.Path;
                }
                else if (export.IsProject())
                {
                    referenceProjectInfo = GetProjectInfo(library);

                    if (referenceProjectInfo is UnreferencedProjectInfo)
                    {
                        // unreferenced ProjectInfos will be added later as simple dll dependencies
                        return null;
                    }

                    if (runtime)
                    {
                        // DependencyReferences do not get passed to the compilation, so we should only
                        // process them when getting the runtime libraries.

                        foreach (var dependencyReference in referenceProjectInfo.DependencyReferences)
                        {
                            libraryDependencies.Add(
                                new Dependency(
                                    GetReferenceLibraryName(dependencyReference),
                                    dependencyReference.Version));
                        }
                    }
                }
            }

            if (runtime)
            {
                return CreateRuntimeLibrary(
                    type.ToLowerInvariant(),
                    export.Name,
                    export.Version.ToString(),
                    hash,
                    CreateRuntimeAssemblyGroups(export, referenceProjectInfo),
                    CreateNativeLibraryGroups(export),
                    CreateResourceAssemblyGroups(export, referenceProjectInfo),
                    libraryDependencies,
                    serviceable,
                    path,
                    hashPath);
            }
            else
            {
                IEnumerable<string> assemblies = Enumerable.Empty<string>();

                //  In some situations, the assets file will include compilation assets under the RID-specific
                //  target, but not under the RID-less target.  The RID-less target is what is used for project
                //  compilation, so make sure we get those assets when writing the compile references to the assets
                //  file.
                //  This can happen when the runtime graph adds dependencies which don't have compile assets excluded.
                //  This was encountered with the 4.3.0 System.Security.Claims, System.Security.Principal.Windows, and
                //  System.Threading.Overlapped packages.
                LockFileTargetLibrary exportWithCompileAssets;
                if (_compilationTargetLibraries != null)
                {
                    _compilationTargetLibraries.TryGetValue(export.Name, out exportWithCompileAssets);
                }
                else
                {
                    exportWithCompileAssets = export;
                }
                if (exportWithCompileAssets != null)
                {
                    assemblies = GetCompileTimeAssemblies(exportWithCompileAssets, referenceProjectInfo);
                }

                return new CompilationLibrary(
                    type.ToLowerInvariant(),
                    export.Name,
                    export.Version.ToString(),
                    hash,
                    assemblies,
                    libraryDependencies,
                    serviceable,
                    path,
                    hashPath);
            }
        }

        private RuntimeFile CreateRuntimeFile(LockFileTargetLibrary library, LockFileItem item)
        {
            //  _packageResolver will be null if _includeRuntimeFileVersions is false, hence the "?."
            var itemFullPath = _packageResolver?.ResolvePackageAssetPath(library, item.Path);
            return CreateRuntimeFile(item.Path, itemFullPath);
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

        private IReadOnlyList<RuntimeAssetGroup> CreateRuntimeAssemblyGroups(LockFileTargetLibrary targetLibrary, SingleProjectInfo referenceProjectInfo)
        {
            if (targetLibrary.IsProject() && !(referenceProjectInfo is UnreferencedProjectInfo))
            {
                return new[] { new RuntimeAssetGroup(string.Empty, referenceProjectInfo.OutputName) };
            }
            else
            {
                List<RuntimeAssetGroup> assemblyGroups = new List<RuntimeAssetGroup>();

                assemblyGroups.Add(
                    new RuntimeAssetGroup(
                        string.Empty,
                        targetLibrary.RuntimeAssemblies.FilterPlaceholderFiles().Select(a => CreateRuntimeFile(targetLibrary, a))));

                foreach (var runtimeTargetsGroup in targetLibrary.GetRuntimeTargetsGroups("runtime"))
                {
                    assemblyGroups.Add(
                        new RuntimeAssetGroup(
                            runtimeTargetsGroup.Key,
                            runtimeTargetsGroup.Select(t => CreateRuntimeFile(targetLibrary, t))));
                }

                return assemblyGroups;
            }
        }

        private IReadOnlyList<RuntimeAssetGroup> CreateNativeLibraryGroups(LockFileTargetLibrary export)
        {
            List<RuntimeAssetGroup> nativeGroups = new List<RuntimeAssetGroup>();

            nativeGroups.Add(
                new RuntimeAssetGroup(
                    string.Empty,
                    export.NativeLibraries.FilterPlaceholderFiles().Select(a => CreateRuntimeFile(export, a))));

            foreach (var runtimeTargetsGroup in export.GetRuntimeTargetsGroups("native"))
            {
                nativeGroups.Add(
                    new RuntimeAssetGroup(
                        runtimeTargetsGroup.Key,
                        runtimeTargetsGroup.Select(t => CreateRuntimeFile(export, t))));
            }

            return nativeGroups;
        }

        private IEnumerable<ResourceAssembly> CreateResourceAssemblyGroups(LockFileTargetLibrary targetLibrary, SingleProjectInfo referenceProjectInfo)
        {
            if (targetLibrary.IsProject() && !(referenceProjectInfo is UnreferencedProjectInfo))
            {
                return CreateResourceAssemblies(referenceProjectInfo.ResourceAssemblies);
            }
            else
            {
                return targetLibrary.ResourceAssemblies.FilterPlaceholderFiles().Select(CreateResourceAssembly);
            }
        }

        private ResourceAssembly CreateResourceAssembly(LockFileItem resourceAssembly)
        {
            string locale;
            if (!resourceAssembly.Properties.TryGetValue("locale", out locale))
            {
                locale = null;
            }

            return new ResourceAssembly(resourceAssembly.Path, locale);
        }

        private IEnumerable<string> GetCompileTimeAssemblies(LockFileTargetLibrary targetLibrary, SingleProjectInfo referenceProjectInfo)
        {
            if (targetLibrary.IsProject() && !(referenceProjectInfo is UnreferencedProjectInfo))
            {
                return new[] { referenceProjectInfo.OutputName };
            }
            else
            {
                return targetLibrary
                    .CompileTimeAssemblies
                    .FilterPlaceholderFiles()
                    .Select(libraryAsset => libraryAsset.Path);
            }
        }

        private IEnumerable<CompilationLibrary> GetReferenceAssemblyLibraries()
        {
            return _referenceAssemblies
                ?.Select(r => new CompilationLibrary(
                    type: "referenceassembly",
                    name: GetReferenceLibraryName(r),
                    version: r.Version,
                    hash: string.Empty,
                    assemblies: new[] { ResolveFrameworkReferencePath(r.FullPath) },
                    dependencies: Enumerable.Empty<Dependency>(),
                    serviceable: false))
                ??
                Enumerable.Empty<CompilationLibrary>();
        }

        private string ResolveFrameworkReferencePath(string fullPath)
        {
            // If resolved path is under ReferenceAssembliesPath store it as a relative to it
            // if not, save only assembly name and try to find it somehow later
            if (!string.IsNullOrEmpty(_referenceAssembliesPath) &&
                fullPath?.StartsWith(_referenceAssembliesPath) == true)
            {
                return fullPath.Substring(_referenceAssembliesPath.Length);
            }

            return Path.GetFileName(fullPath);
        }

        private IEnumerable<RuntimeLibrary> GetReferenceRuntimeLibraries(IEnumerable<ReferenceInfo> references)
        {
            return references
                ?.Select(r => CreateRuntimeLibrary(
                    type: "reference",
                    name: GetReferenceLibraryName(r),
                    version: r.Version,
                    hash: string.Empty,
                    runtimeAssemblyGroups: new[] { new RuntimeAssetGroup(string.Empty, new[] { CreateRuntimeFile(r.FileName, r.FullPath) }) },
                    nativeLibraryGroups: new RuntimeAssetGroup[] { },
                    resourceAssemblies: CreateResourceAssemblies(r.ResourceAssemblies),
                    dependencies: Enumerable.Empty<Dependency>(),
                    serviceable: false))
                ??
                Enumerable.Empty<RuntimeLibrary>();
        }

        private IEnumerable<CompilationLibrary> GetReferenceCompilationLibraries(IEnumerable<ReferenceInfo> references)
        {
            return references
                ?.Select(r => new CompilationLibrary(
                    type: "reference",
                    name: GetReferenceLibraryName(r),
                    version: r.Version,
                    hash: string.Empty,
                    assemblies: new[] { r.FileName },
                    dependencies: Enumerable.Empty<Dependency>(),
                    serviceable: false))
                ??
                Enumerable.Empty<CompilationLibrary>();
        }

        private IEnumerable<RuntimeLibrary> GetDirectReferenceRuntimeLibraries()
        {
            return GetReferenceRuntimeLibraries(_directReferences);
        }

        private IEnumerable<CompilationLibrary> GetDirectReferenceCompilationLibraries()
        {
            return GetReferenceCompilationLibraries(_directReferences);
        }

        private IEnumerable<RuntimeLibrary> GetDependencyReferenceRuntimeLibraries()
        {
            return GetReferenceRuntimeLibraries(_dependencyReferences);
        }

        private string GetReferenceLibraryName(ReferenceInfo reference)
        {
            if (!ReferenceLibraryNames.TryGetValue(reference, out string name))
            {
                // Reference names can conflict with PackageReference names, so
                // ensure that the Reference names are unique when creating libraries
                name = GetUniqueReferenceName(reference.Name);

                ReferenceLibraryNames.Add(reference, name);
                UsedLibraryNames.Add(name);
            }

            return name;
        }

        private string GetUniqueReferenceName(string name)
        {
            if (UsedLibraryNames.Contains(name))
            {
                string startingName = $"{name}.Reference";
                name = startingName;

                int suffix = 1;
                while (UsedLibraryNames.Contains(name))
                {
                    name = $"{startingName}{suffix++}";
                }
            }

            return name;
        }

        private static IEnumerable<ResourceAssembly> CreateResourceAssemblies(IEnumerable<ResourceAssemblyInfo> resourceAssemblyInfos)
        {
            return resourceAssemblyInfos
                .Select(r => new ResourceAssembly(r.RelativePath, r.Culture));
        }

        private SingleProjectInfo GetProjectInfo(LockFileLibrary library)
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

        private static string EnsureTrailingSlash(string path)
        {
            return EnsureTrailingCharacter(path, Path.DirectorySeparatorChar);
        }

        private static string EnsureTrailingCharacter(string path, char trailingCharacter)
        {
            // if the path is empty, we want to return the original string instead of a single trailing character.
            if (string.IsNullOrEmpty(path) || path[path.Length - 1] == trailingCharacter)
            {
                return path;
            }

            return path + trailingCharacter;
        }
    }
}
