// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyModel;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;

namespace Microsoft.NETCore.Build.Tasks
{
    public class DependencyContextBuilder
    {
        public DependencyContextBuilder()
        {
        }

        public DependencyContext Build(
            string projectName,
            string projectVersion,
            CompilationOptions compilationOptions,
            LockFile lockFile,
            NuGetFramework framework,
            string runtime)
        {
            bool includeCompilationLibraries = compilationOptions != null;

            LockFileTarget lockFileTarget = lockFile.GetTarget(framework, runtime);

            ProjectContext projectContext = lockFileTarget.CreateProjectContext();
            IEnumerable<LockFileTargetLibrary> runtimeExports = projectContext.GetRuntimeLibraries();
            IEnumerable<LockFileTargetLibrary> compilationExports =
                includeCompilationLibraries ?
                    projectContext.GetCompileLibraries() :
                    Enumerable.Empty<LockFileTargetLibrary>();

            var dependencyLookup = compilationExports
                .Concat(runtimeExports)
                .Distinct()
                .Select(library => new Dependency(library.Name, library.Version.ToString()))
                .ToDictionary(dependency => dependency.Name, StringComparer.OrdinalIgnoreCase);

            var libraryLookup = lockFile.Libraries.ToDictionary(l => l.Name, StringComparer.OrdinalIgnoreCase);

            var runtimeSignature = GenerateRuntimeSignature(runtimeExports);

            var projectRuntimeLibrary = (RuntimeLibrary)GetProjectLibrary(
                projectName,
                projectVersion,
                lockFile,
                lockFileTarget,
                dependencyLookup,
                runtime: true);
            IEnumerable<RuntimeLibrary> runtimeLibraries =
                new[] { projectRuntimeLibrary }
                .Concat(GetLibraries(runtimeExports, libraryLookup, dependencyLookup, runtime: true).Cast<RuntimeLibrary>());

            IEnumerable<CompilationLibrary> compilationLibraries;
            if (includeCompilationLibraries)
            {
                var projectCompilationLibrary = (CompilationLibrary)GetProjectLibrary(
                    projectName,
                    projectVersion,
                    lockFile,
                    lockFileTarget,
                    dependencyLookup,
                    runtime: false);
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

        private Library GetProjectLibrary(
            string projectName,
            string projectVersion,
            LockFile lockFile,
            LockFileTarget lockFileTarget,
            Dictionary<string, Dependency> dependencyLookup,
            bool runtime)
        {
            // TODO: What other information about the current project needs to be included here? - https://github.com/dotnet/sdk/issues/12

            List<Dependency> dependencies = new List<Dependency>();

            IEnumerable<ProjectFileDependencyGroup> projectFileDependencies = lockFile
                .ProjectFileDependencyGroups
                .Where(dg => dg.FrameworkName == string.Empty ||
                             dg.FrameworkName == lockFileTarget.TargetFramework.DotNetFrameworkName);

            foreach (string projectFileDependency in projectFileDependencies.SelectMany(dg => dg.Dependencies))
            {
                int separatorIndex = projectFileDependency.IndexOf(' ');
                string dependencyName = separatorIndex > 0 ?
                    projectFileDependency.Substring(0, separatorIndex) :
                    projectFileDependency;

                Dependency dependency;
                if (dependencyLookup.TryGetValue(dependencyName, out dependency))
                {
                    dependencies.Add(dependency);
                }
            }

            string type = "project";
            string projectAssemblyName = $"{projectName}.dll";

            if (runtime)
            {
                RuntimeAssetGroup[] runtimeAssemblyGroups = new[] { new RuntimeAssetGroup(string.Empty, projectAssemblyName) };

                return new RuntimeLibrary(
                    type: type,
                    name: projectName,
                    version: projectVersion,
                    hash: string.Empty,
                    runtimeAssemblyGroups: runtimeAssemblyGroups,
                    nativeLibraryGroups: new RuntimeAssetGroup[] { },
                    resourceAssemblies: new ResourceAssembly[] { },
                    dependencies: dependencies.ToArray(),
                    serviceable: false);
            }
            else
            {
                return new CompilationLibrary(
                    type: type,
                    name: projectName,
                    version: projectVersion,
                    hash: string.Empty,
                    assemblies: new[] { projectAssemblyName },
                    dependencies: dependencies.ToArray(),
                    serviceable: false);
            }
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
            LockFileLibrary library;
            if (libraryLookup.TryGetValue(export.Name, out library))
            {
                if (!string.IsNullOrEmpty(library.Sha512))
                {
                    hash = "sha512-" + library.Sha512;
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
                    export.ResourceAssemblies.Select(CreateResourceAssembly),
                    libraryDependencies,
                    serviceable,
                    path);
            }
            else
            {
                IEnumerable<string> assemblies = export
                    .CompileTimeAssemblies
                    .Select(libraryAsset => libraryAsset.Path)
                    .FilterPlaceHolderFiles();

                return new CompilationLibrary(
                    type.ToString().ToLowerInvariant(),
                    export.Name,
                    export.Version.ToString(),
                    hash,
                    assemblies,
                    libraryDependencies,
                    serviceable,
                    path);
            }
        }

        private IReadOnlyList<RuntimeAssetGroup> CreateRuntimeAssemblyGroups(LockFileTargetLibrary export)
        {
            List<RuntimeAssetGroup> assemblyGroups = new List<RuntimeAssetGroup>();

            assemblyGroups.Add(
                new RuntimeAssetGroup(
                    string.Empty,
                    export.RuntimeAssemblies.Select(a => a.Path).FilterPlaceHolderFiles()));

            // TODO RuntimeTargets - https://github.com/dotnet/sdk/issues/12
            //export.RuntimeTargets.GroupBy(l => l.)

            return assemblyGroups;
        }

        private IReadOnlyList<RuntimeAssetGroup> CreateNativeLibraryGroups(LockFileTargetLibrary export)
        {
            return new[] {
                new RuntimeAssetGroup(
                    string.Empty, 
                    export.NativeLibraries.Select(a => a.Path).FilterPlaceHolderFiles()
                    )
            };
        }

        private ResourceAssembly CreateResourceAssembly(LockFileItem resourceAssembly)
        {
            // TODO: implement - https://github.com/dotnet/sdk/issues/12
            return null;

            //return new ResourceAssembly(
            //    path: resourceAssembly.Path,
            //    locale: resourceAssembly.Locale
            //    );
        }
    }
}