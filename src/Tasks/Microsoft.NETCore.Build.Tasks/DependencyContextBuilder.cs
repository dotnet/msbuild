// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Build.Framework;
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
            ITaskItem compilerOptionsItem,
            LockFile lockFile,
            NuGetFramework framework,
            string runtime)
        {
            LockFileTarget lockFileTarget = lockFile.GetTarget(framework, runtime);

            ProjectContext projectContext = lockFileTarget.CreateProjectContext();
            IEnumerable<LockFileTargetLibrary> runtimeExports = projectContext.RuntimeLibraries;

            var dependencyLookup = runtimeExports
               .Select(identity => new Dependency(identity.Name, identity.Version.ToString()))
               .ToDictionary(dependency => dependency.Name, StringComparer.OrdinalIgnoreCase);

            var libraryLookup = lockFile.Libraries.ToDictionary(l => l.Name, StringComparer.OrdinalIgnoreCase);

            var runtimeSignature = GenerateRuntimeSignature(runtimeExports);

            IEnumerable<RuntimeLibrary> runtimeLibraries =
                new[] { GetProjectLibrary(projectName, projectVersion, lockFile, lockFileTarget, dependencyLookup) }
                .Concat(GetLibraries(runtimeExports, libraryLookup, dependencyLookup, runtime: true).Cast<RuntimeLibrary>());

            return new DependencyContext(
                new TargetInfo(framework.DotNetFrameworkName, runtime, runtimeSignature, projectContext.IsPortable),
                GetCompilationOptions(compilerOptionsItem),
                Enumerable.Empty<CompilationLibrary>(), //GetLibraries(compilationExports, dependencyLookup, runtime: false).Cast<CompilationLibrary>(), - https://github.com/dotnet/sdk/issues/11
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

        private RuntimeLibrary GetProjectLibrary(
            string projectName,
            string projectVersion,
            LockFile lockFile,
            LockFileTarget lockFileTarget,
            Dictionary<string, Dependency> dependencyLookup)
        {
            // TODO: What other information about the current project needs to be included here? - https://github.com/dotnet/sdk/issues/12

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

        private static CompilationOptions GetCompilationOptions(ITaskItem compilerOptionsItem)
        {
            if (compilerOptionsItem == null)
            {
                return CompilationOptions.Default;
            }

            return new CompilationOptions(
                compilerOptionsItem.GetMetadata("DefineConstants")?.Split(';'),
                compilerOptionsItem.GetMetadata("LangVersion"),
                compilerOptionsItem.GetMetadata("PlatformTarget"),
                compilerOptionsItem.GetBooleanMetadata("AllowUnsafeBlocks"),
                compilerOptionsItem.GetBooleanMetadata("WarningsAsErrors"),
                compilerOptionsItem.GetBooleanMetadata("Optimize"),
                compilerOptionsItem.GetMetadata("AssemblyOriginatorKeyFile"),
                compilerOptionsItem.GetBooleanMetadata("DelaySign"),
                compilerOptionsItem.GetBooleanMetadata("PublicSign"),
                compilerOptionsItem.GetMetadata("DebugType"),
                "exe".Equals(compilerOptionsItem.GetMetadata("OutputType"), StringComparison.OrdinalIgnoreCase),
                compilerOptionsItem.GetBooleanMetadata("GenerateDocumentationFile"));
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
            // TODO: PreserveCompilationContext - https://github.com/dotnet/sdk/issues/11
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

            // TODO RuntimeTargets - https://github.com/dotnet/sdk/issues/12
            //export.RuntimeTargets.GroupBy(l => l.)

            return assemblyGroups;
        }

        private IReadOnlyList<RuntimeAssetGroup> CreateNativeLibraryGroups(LockFileTargetLibrary export)
        {
            return new[] { new RuntimeAssetGroup(string.Empty, export.NativeLibraries.Select(a => a.Path)) };
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