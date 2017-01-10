// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyModel;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;

namespace Microsoft.NET.Build.Tasks
{
    public class DependencyContextBuilder
    {
        private readonly VersionFolderPathResolver _versionFolderPathResolver;
        private readonly SingleProjectInfo _mainProjectInfo;
        private readonly ProjectContext _projectContext;
        private IEnumerable<ReferenceInfo> _frameworkReferences;
        private Dictionary<string, SingleProjectInfo> _referenceProjectInfos;
        private IEnumerable<string> _privateAssetPackageIds;
        private CompilationOptions _compilationOptions;
        private string _referenceAssembliesPath;

        public DependencyContextBuilder(SingleProjectInfo mainProjectInfo, ProjectContext projectContext)
        {
            _mainProjectInfo = mainProjectInfo;
            _projectContext = projectContext;

            // This resolver is only used for building file names, so that base path is not required.
            _versionFolderPathResolver = new VersionFolderPathResolver(rootPath: null);
        }

        public DependencyContextBuilder WithFrameworkReferences(IEnumerable<ReferenceInfo> frameworkReferences)
        {
            // note: Framework libraries only export compile-time stuff
            // since they assume the runtime library is present already
            _frameworkReferences = frameworkReferences;
            return this;
        }

        public DependencyContextBuilder WithReferenceProjectInfos(Dictionary<string, SingleProjectInfo> referenceProjectInfos)
        {
            _referenceProjectInfos = referenceProjectInfos;
            return this;
        }

        public DependencyContextBuilder WithPrivateAssets(IEnumerable<string> privateAssetPackageIds)
        {
            _privateAssetPackageIds = privateAssetPackageIds;
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

        public DependencyContext Build()
        {
            bool includeCompilationLibraries = _compilationOptions != null;

            IEnumerable<LockFileTargetLibrary> runtimeExports = _projectContext.GetRuntimeLibraries(_privateAssetPackageIds);
            IEnumerable<LockFileTargetLibrary> compilationExports =
                includeCompilationLibraries ?
                    _projectContext.GetCompileLibraries(_privateAssetPackageIds) :
                    Enumerable.Empty<LockFileTargetLibrary>();

            var dependencyLookup = compilationExports
                .Concat(runtimeExports)
                .Distinct()
                .Select(library => new Dependency(library.Name, library.Version.ToString()))
                .ToDictionary(dependency => dependency.Name, StringComparer.OrdinalIgnoreCase);

            var libraryLookup = new LockFileLookup(_projectContext.LockFile);

            var runtimeSignature = GenerateRuntimeSignature(runtimeExports);

            RuntimeLibrary projectRuntimeLibrary = GetProjectRuntimeLibrary(
                _mainProjectInfo,
                _projectContext,
                dependencyLookup);
            IEnumerable<RuntimeLibrary> runtimeLibraries =
                new[] { projectRuntimeLibrary }
                .Concat(GetLibraries(runtimeExports, libraryLookup, dependencyLookup, runtime: true).Cast<RuntimeLibrary>());

            IEnumerable<CompilationLibrary> compilationLibraries;
            if (includeCompilationLibraries)
            {
                CompilationLibrary projectCompilationLibrary = GetProjectCompilationLibrary(
                    _mainProjectInfo,
                    _projectContext,
                    dependencyLookup);
                compilationLibraries =
                    new[] { projectCompilationLibrary }
                    .Concat(GetFrameworkLibraries())
                    .Concat(GetLibraries(compilationExports, libraryLookup, dependencyLookup, runtime: false).Cast<CompilationLibrary>());
            }
            else
            {
                compilationLibraries = Enumerable.Empty<CompilationLibrary>();
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

        private static string GenerateRuntimeSignature(IEnumerable<LockFileTargetLibrary> runtimeExports)
        {
            var sha1 = SHA1.Create();
            var builder = new StringBuilder();
            var packages = runtimeExports
                .Where(libraryExport => libraryExport.Type == "package");
            var separator = "|";
            foreach (var libraryExport in packages)
            {
                builder.Append(libraryExport.Name);
                builder.Append(separator);
                builder.Append(libraryExport.Version.ToString());
                builder.Append(separator);
            }
            var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));

            builder.Clear();
            foreach (var hashByte in hash)
            {
                builder.AppendFormat("{0:x2}", hashByte);
            }
            return builder.ToString();
        }

        private List<Dependency> GetProjectDependencies(
            ProjectContext projectContext,
            Dictionary<string, Dependency> dependencyLookup)
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

            return dependencies;
        }

        private RuntimeLibrary GetProjectRuntimeLibrary(
            SingleProjectInfo projectInfo,
            ProjectContext projectContext,
            Dictionary<string, Dependency> dependencyLookup)
        {

            RuntimeAssetGroup[] runtimeAssemblyGroups = new[] { new RuntimeAssetGroup(string.Empty, projectInfo.OutputName) };

            List<Dependency> dependencies = GetProjectDependencies(projectContext, dependencyLookup);

            ResourceAssembly[] resourceAssemblies = projectInfo
                .ResourceAssemblies
                .Select(r => new ResourceAssembly(r.RelativePath, r.Culture))
                .ToArray();

            return new RuntimeLibrary(
                type: "project",
                name: projectInfo.Name,
                version: projectInfo.Version,
                hash: string.Empty,
                runtimeAssemblyGroups: runtimeAssemblyGroups,
                nativeLibraryGroups: new RuntimeAssetGroup[] { },
                resourceAssemblies: resourceAssemblies,
                dependencies: dependencies.ToArray(),
                serviceable: false);
        }

        private CompilationLibrary GetProjectCompilationLibrary(
            SingleProjectInfo projectInfo,
            ProjectContext projectContext,
            Dictionary<string, Dependency> dependencyLookup)
        {
            List<Dependency> dependencies = GetProjectDependencies(projectContext, dependencyLookup);

            return new CompilationLibrary(
                type: "project",
                name: projectInfo.Name,
                version: projectInfo.Version,
                hash: string.Empty,
                assemblies: new[] { projectInfo.OutputName },
                dependencies: dependencies.ToArray(),
                serviceable: false);
        }

        private IEnumerable<Library> GetLibraries(
            IEnumerable<LockFileTargetLibrary> exports,
            LockFileLookup libraryLookup,
            IDictionary<string, Dependency> dependencyLookup,
            bool runtime)
        {
            return exports.Select(export => GetLibrary(export, libraryLookup, dependencyLookup, runtime));
        }

        private Library GetLibrary(
            LockFileTargetLibrary export,
            LockFileLookup libraryLookup,
            IDictionary<string, Dependency> dependencyLookup,
            bool runtime)
        {
            var type = export.Type;
            bool isPackage = type == "package";

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
                else if (type == "project")
                {
                    referenceProjectInfo = GetProjectInfo(library);
                }
            }

            if (runtime)
            {
                return new RuntimeLibrary(
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
                IEnumerable<string> assemblies = GetCompileTimeAssemblies(export, referenceProjectInfo);

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

        private IReadOnlyList<RuntimeAssetGroup> CreateRuntimeAssemblyGroups(LockFileTargetLibrary targetLibrary, SingleProjectInfo referenceProjectInfo)
        {
            if (targetLibrary.Type == "project")
            {
                EnsureProjectInfo(referenceProjectInfo, targetLibrary.Name);
                return new[] { new RuntimeAssetGroup(string.Empty, referenceProjectInfo.OutputName) };
            }
            else
            {
                List<RuntimeAssetGroup> assemblyGroups = new List<RuntimeAssetGroup>();

                assemblyGroups.Add(
                    new RuntimeAssetGroup(
                        string.Empty,
                        targetLibrary.RuntimeAssemblies.FilterPlaceHolderFiles().Select(a => a.Path)));

                foreach (var runtimeTargetsGroup in targetLibrary.GetRuntimeTargetsGroups("runtime"))
                {
                    assemblyGroups.Add(
                        new RuntimeAssetGroup(
                            runtimeTargetsGroup.Key,
                            runtimeTargetsGroup.Select(t => t.Path)));
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
                    export.NativeLibraries.FilterPlaceHolderFiles().Select(a => a.Path)));

            foreach (var runtimeTargetsGroup in export.GetRuntimeTargetsGroups("native"))
            {
                nativeGroups.Add(
                    new RuntimeAssetGroup(
                        runtimeTargetsGroup.Key,
                        runtimeTargetsGroup.Select(t => t.Path)));
            }

            return nativeGroups;
        }

        private IEnumerable<ResourceAssembly> CreateResourceAssemblyGroups(LockFileTargetLibrary targetLibrary, SingleProjectInfo referenceProjectInfo)
        {
            if (targetLibrary.Type == "project")
            {
                EnsureProjectInfo(referenceProjectInfo, targetLibrary.Name);
                return referenceProjectInfo
                    .ResourceAssemblies
                    .Select(r => new ResourceAssembly(r.RelativePath, r.Culture));
            }
            else
            {
                return targetLibrary.ResourceAssemblies.FilterPlaceHolderFiles().Select(CreateResourceAssembly);
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
            if (targetLibrary.Type == "project")
            {
                EnsureProjectInfo(referenceProjectInfo, targetLibrary.Name);
                return new[] { referenceProjectInfo.OutputName };
            }
            else
            {
                return targetLibrary
                    .CompileTimeAssemblies
                    .FilterPlaceHolderFiles()
                    .Select(libraryAsset => libraryAsset.Path);
            }
        }

        private IEnumerable<CompilationLibrary> GetFrameworkLibraries()
        {
            return _frameworkReferences
                ?.Select(r => new CompilationLibrary(
                    type: "referenceassembly",
                    name: r.Name,
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

        private static void EnsureProjectInfo(SingleProjectInfo referenceProjectInfo, string libraryName)
        {
            if (referenceProjectInfo == null)
            {
                throw new BuildErrorException(Strings.CannotFindProjectInfo, libraryName);
            }
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
                throw new BuildErrorException(Strings.CannotFindProjectInfo, fullProjectPath);
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