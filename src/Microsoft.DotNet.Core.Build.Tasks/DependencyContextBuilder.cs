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

namespace Microsoft.DotNet.Core.Build.Tasks
{
    public class DependencyContextBuilder
    {
        public DependencyContextBuilder()
        {
        }

        public DependencyContext Build(
            string projectName,
            string projectVersion,
            CompilationOptions compilerOptions,
            LockFile lockFile,
            NuGetFramework framework,
            string runtime)
        {
            LockFileTarget lockFileTarget = lockFile.GetTarget(framework, runtime);
            IEnumerable<LockFileTargetLibrary> runtimeExports = lockFileTarget.Libraries;

            // TODO: get this from the lock file once https://github.com/NuGet/Home/issues/2695 is fixed.
            var platformPackageName = "Microsoft.NETCore.App";
            var platformExport = lockFileTarget
                .Libraries
                .FirstOrDefault(e => e.Name.Equals(platformPackageName, StringComparison.OrdinalIgnoreCase));

            bool portable = platformExport != null;
            if (portable)
            {
                runtimeExports = FilterPlatformDependencies(runtimeExports, platformExport);
            }

            var dependencyLookup = runtimeExports
               .Select(identity => new Dependency(identity.Name, identity.Version.ToString()))
               .ToDictionary(dependency => dependency.Name, StringComparer.OrdinalIgnoreCase);

            var libraryLookup = lockFile.Libraries.ToDictionary(l => l.Name, StringComparer.OrdinalIgnoreCase);

            var runtimeSignature = GenerateRuntimeSignature(runtimeExports);

            IEnumerable<RuntimeLibrary> runtimeLibraries =
                new[] { GetProjectLibrary(projectName, projectVersion, lockFile, lockFileTarget, dependencyLookup) }
                .Concat(GetLibraries(runtimeExports, libraryLookup, dependencyLookup, runtime: true).Cast<RuntimeLibrary>());

            return new DependencyContext(
                new TargetInfo(framework.DotNetFrameworkName, runtime, runtimeSignature, portable),
                compilerOptions ?? CompilationOptions.Default,
                Enumerable.Empty<CompilationLibrary>(), //GetLibraries(compilationExports, dependencyLookup, runtime: false).Cast<CompilationLibrary>(),
                runtimeLibraries,
                new RuntimeFallbacks[] { });
        }

        private static IEnumerable<LockFileTargetLibrary> FilterPlatformDependencies(
            IEnumerable<LockFileTargetLibrary> runtimeExports,
            LockFileTargetLibrary platformExport)
        {
            var exportsLookup = runtimeExports.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);

            HashSet<string> exclusionList = GetPlatformExclusionList(platformExport, exportsLookup);

            return runtimeExports.Where(e => !exclusionList.Contains(e.Name));
        }

        private static HashSet<string> GetPlatformExclusionList(
            LockFileTargetLibrary platformExport,
            IDictionary<string, LockFileTargetLibrary> exportsLookup)
        {
            var exclusionList = new HashSet<string>();

            exclusionList.Add(platformExport.Name);
            CollectDependencies(exportsLookup, platformExport.Dependencies, exclusionList);

            return exclusionList;
        }

        private static void CollectDependencies(
            IDictionary<string, LockFileTargetLibrary> exportsLookup,
            IEnumerable<PackageDependency> dependencies,
            HashSet<string> exclusionList)
        {
            foreach (PackageDependency dependency in dependencies)
            {
                LockFileTargetLibrary export = exportsLookup[dependency.Id];
                if (export.Version.Equals(dependency.VersionRange.MinVersion))
                {
                    exclusionList.Add(export.Name);
                    CollectDependencies(exportsLookup, export.Dependencies, exclusionList);
                }
            }
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

        private RuntimeLibrary GetProjectLibrary(
            string projectName,
            string projectVersion,
            LockFile lockFile,
            LockFileTarget lockFileTarget,
            Dictionary<string, Dependency> dependencyLookup)
        {
            RuntimeAssetGroup[] runtimeAssemblyGroups = new[] { new RuntimeAssetGroup(string.Empty, $"{projectName}.dll") };

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

            return new RuntimeLibrary(
                type: "project",
                name: projectName,
                version: projectVersion,
                hash: string.Empty,
                runtimeAssemblyGroups: runtimeAssemblyGroups,
                nativeLibraryGroups: new RuntimeAssetGroup[] { },
                resourceAssemblies: new ResourceAssembly[] { },
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
            LockFileLibrary library;
            if (libraryLookup.TryGetValue(export.Name, out library))
            {
                hash = "sha512-" + library.Sha512;
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
                    serviceable
                    );
            }
            //else
            //{
            //    IEnumerable<string> assemblies = export
            //        .CompilationAssemblies
            //        .Select(libraryAsset => libraryAsset.RelativePath);

            //    return new CompilationLibrary(
            //        type.ToString().ToLowerInvariant(),
            //        export.Library.Identity.Name,
            //        export.Library.Identity.Version.ToString(),
            //        export.Library.Hash,
            //        assemblies,
            //        libraryDependencies,
            //        serviceable);
            //}
            return null;
        }

        private IReadOnlyList<RuntimeAssetGroup> CreateRuntimeAssemblyGroups(LockFileTargetLibrary export)
        {
            List<RuntimeAssetGroup> assemblyGroups = new List<RuntimeAssetGroup>();

            assemblyGroups.Add(
                new RuntimeAssetGroup(string.Empty, export.RuntimeAssemblies.Select(a => a.Path)));

            // TODO RuntimeTargets
            //export.RuntimeTargets.GroupBy(l => l.)

            return assemblyGroups;
        }

        private IReadOnlyList<RuntimeAssetGroup> CreateNativeLibraryGroups(LockFileTargetLibrary export)
        {
            return new[] { new RuntimeAssetGroup(string.Empty, export.NativeLibraries.Select(a => a.Path)) };
        }


        private ResourceAssembly CreateResourceAssembly(LockFileItem resourceAssembly)
        {
            // TODO: implement
            return null;

            //return new ResourceAssembly(
            //    path: resourceAssembly.Path,
            //    locale: resourceAssembly.Locale
            //    );
        }
    }
}