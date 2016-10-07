// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
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
        private IEnumerable<string> _privateAssetPackageIds;

        public DependencyContextBuilder()
        {
            // This resolver is only used for building file names, so that base path is not required.
            _versionFolderPathResolver = new VersionFolderPathResolver(path: null);
        }

        public DependencyContextBuilder WithPrivateAssets(IEnumerable<string> privateAssetPackageIds)
        {
            _privateAssetPackageIds = privateAssetPackageIds;
            return this;
        }

        public DependencyContext Build(
            SingleProjectInfo mainProjectInfo,
            CompilationOptions compilationOptions,
            LockFile lockFile,
            NuGetFramework framework,
            string runtime)
        {
            bool includeCompilationLibraries = compilationOptions != null;

            ProjectContext projectContext = lockFile.CreateProjectContext(mainProjectInfo.ProjectPath, framework, runtime);
            IEnumerable<LockFileTargetLibrary> runtimeExports = projectContext.GetRuntimeLibraries(_privateAssetPackageIds);
            IEnumerable<LockFileTargetLibrary> compilationExports =
                includeCompilationLibraries ?
                    projectContext.GetCompileLibraries(_privateAssetPackageIds) :
                    Enumerable.Empty<LockFileTargetLibrary>();

            var dependencyLookup = compilationExports
                .Concat(runtimeExports)
                .Distinct()
                .Select(library => new Dependency(library.Name, library.Version.ToString()))
                .ToDictionary(dependency => dependency.Name, StringComparer.OrdinalIgnoreCase);

            var libraryLookup = lockFile.Libraries.ToDictionary(l => l.Name, StringComparer.OrdinalIgnoreCase);

            var runtimeSignature = GenerateRuntimeSignature(runtimeExports);

            RuntimeLibrary projectRuntimeLibrary = GetProjectRuntimeLibrary(
                mainProjectInfo,
                projectContext,
                dependencyLookup);
            IEnumerable<RuntimeLibrary> runtimeLibraries =
                new[] { projectRuntimeLibrary }
                .Concat(GetLibraries(runtimeExports, libraryLookup, dependencyLookup, runtime: true).Cast<RuntimeLibrary>());

            IEnumerable<CompilationLibrary> compilationLibraries;
            if (includeCompilationLibraries)
            {
                CompilationLibrary projectCompilationLibrary = GetProjectCompilationLibrary(
                    mainProjectInfo,
                    projectContext,
                    dependencyLookup);
                compilationLibraries =
                    new[] { projectCompilationLibrary }
                    .Concat(GetLibraries(compilationExports, libraryLookup, dependencyLookup, runtime: false).Cast<CompilationLibrary>());
            }
            else
            {
                compilationLibraries = Enumerable.Empty<CompilationLibrary>();
            }

            return new DependencyContext(
                new TargetInfo(framework.DotNetFrameworkName, runtime, runtimeSignature, projectContext.IsPortable),
                compilationOptions ?? CompilationOptions.Default,
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

            RuntimeAssetGroup[] runtimeAssemblyGroups = new[] { new RuntimeAssetGroup(string.Empty, projectInfo.GetOutputName()) };

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
                assemblies: new[] { projectInfo.GetOutputName() },
                dependencies: dependencies.ToArray(),
                serviceable: false);
        }

        private IEnumerable<Library> GetLibraries(
            IEnumerable<LockFileTargetLibrary> exports,
            IDictionary<string, LockFileLibrary> libraryLookup,
            IDictionary<string, Dependency> dependencyLookup,
            bool runtime)
        {
            return exports.Select(export => GetLibrary(export, libraryLookup, dependencyLookup, runtime));
        }

        private Library GetLibrary(
            LockFileTargetLibrary export,
            IDictionary<string, LockFileLibrary> libraryLookup,
            IDictionary<string, Dependency> dependencyLookup,
            bool runtime)
        {
            var type = export.Type;

            // TEMPORARY: All packages are serviceable in RC2
            // See https://github.com/dotnet/cli/issues/2569
            var serviceable = export.Type == "package";
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
            if (libraryLookup.TryGetValue(export.Name, out library))
            {
                if (!string.IsNullOrEmpty(library.Sha512))
                {
                    hash = "sha512-" + library.Sha512;
                    hashPath = _versionFolderPathResolver.GetHashFileName(export.Name, export.Version);
                }

                path = library.Path;
            }

            if (runtime)
            {
                return new RuntimeLibrary(
                    type.ToLowerInvariant(),
                    export.Name,
                    export.Version.ToString(),
                    hash,
                    CreateRuntimeAssemblyGroups(export),
                    CreateNativeLibraryGroups(export),
                    export.ResourceAssemblies.FilterPlaceHolderFiles().Select(CreateResourceAssembly),
                    libraryDependencies,
                    serviceable,
                    path,
                    hashPath);
            }
            else
            {
                IEnumerable<string> assemblies = export
                    .CompileTimeAssemblies
                    .FilterPlaceHolderFiles()
                    .Select(libraryAsset => libraryAsset.Path);

                return new CompilationLibrary(
                    type.ToString().ToLowerInvariant(),
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

        private IReadOnlyList<RuntimeAssetGroup> CreateRuntimeAssemblyGroups(LockFileTargetLibrary export)
        {
            List<RuntimeAssetGroup> assemblyGroups = new List<RuntimeAssetGroup>();

            assemblyGroups.Add(
                new RuntimeAssetGroup(
                    string.Empty,
                    export.RuntimeAssemblies.FilterPlaceHolderFiles().Select(a => a.Path)));

            foreach (var runtimeTargetsGroup in export.GetRuntimeTargetsGroups("runtime"))
            {
                assemblyGroups.Add(
                    new RuntimeAssetGroup(
                        runtimeTargetsGroup.Key,
                        runtimeTargetsGroup.Select(t => t.Path)));
            }

            return assemblyGroups;
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

        private ResourceAssembly CreateResourceAssembly(LockFileItem resourceAssembly)
        {
            string locale;
            if (!resourceAssembly.Properties.TryGetValue("locale", out locale))
            {
                locale = null;
            }

            return new ResourceAssembly(resourceAssembly.Path, locale);
        }
    }
}