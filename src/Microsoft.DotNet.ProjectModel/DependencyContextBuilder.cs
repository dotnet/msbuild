// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Compilation;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.DotNet.ProjectModel.Resolution;
using Microsoft.DotNet.ProjectModel.Utilities;
using NuGet.Frameworks;

namespace Microsoft.Extensions.DependencyModel
{
    public static class DependencyContextBuilder
    {
        public static DependencyContext Build(CommonCompilerOptions compilerOptions, LibraryExporter libraryExporter, string configuration, NuGetFramework target, string runtime)
        {
            var dependencies = libraryExporter.GetAllExports();

            // Sometimes we have package and reference assembly with the same name (System.Runtime for example) thats why we
            // deduplicating them prefering reference assembly
            var dependencyLookup = dependencies
                .OrderBy(export => export.Library.Identity.Type == LibraryType.ReferenceAssembly)
                .GroupBy(export => export.Library.Identity.Name)
                .Select(exports => exports.First())
                .Select(export => new Dependency(export.Library.Identity.Name, export.Library.Identity.Version.ToString()))
                .ToDictionary(dependency => dependency.Name);

            return new DependencyContext(target.DotNetFrameworkName, runtime,
                GetCompilationOptions(compilerOptions),
                GetLibraries(dependencies, dependencyLookup, target, configuration, runtime: false).Cast<CompilationLibrary>().ToArray(),
                GetLibraries(dependencies, dependencyLookup, target, configuration, runtime: true).Cast<RuntimeLibrary>().ToArray());
        }

        private static CompilationOptions GetCompilationOptions(CommonCompilerOptions compilerOptions)
        {
            return new CompilationOptions(compilerOptions.Defines,
                compilerOptions.LanguageVersion,
                compilerOptions.Platform,
                compilerOptions.AllowUnsafe,
                compilerOptions.WarningsAsErrors,
                compilerOptions.Optimize,
                compilerOptions.KeyFile,
                compilerOptions.DelaySign,
                compilerOptions.PublicSign,
                compilerOptions.EmitEntryPoint,
                compilerOptions.GenerateXmlDocumentation);
        }

        private static IEnumerable<Library> GetLibraries(IEnumerable<LibraryExport> dependencies,
            IDictionary<string, Dependency> dependencyLookup,
            NuGetFramework target,
            string configuration,
            bool runtime)
        {
            return dependencies.Select(export => GetLibrary(export, target, configuration, runtime, dependencyLookup));
        }

        private static Library GetLibrary(LibraryExport export,
            NuGetFramework target,
            string configuration,
            bool runtime,
            IDictionary<string, Dependency> dependencyLookup)
        {
            var type = export.Library.Identity.Type;

            var serviceable = (export.Library as PackageDescription)?.Library.IsServiceable ?? false;
            var libraryDependencies = new List<Dependency>();

            var libraryAssets = runtime ? export.RuntimeAssemblies : export.CompilationAssemblies;

            foreach (var libraryDependenciesGroup in export.Library.Dependencies.GroupBy(d => d.Name))
            {
                LibraryRange libraryDependency = libraryDependenciesGroup
                    .OrderByDescending(d => d.Target == LibraryType.ReferenceAssembly)
                    .First();

                Dependency dependency;
                if (dependencyLookup.TryGetValue(libraryDependency.Name, out dependency))
                {
                    libraryDependencies.Add(dependency);
                }
            }

            string[] assemblies;
            if (type == LibraryType.Project)
            {
                var isExe = ((ProjectDescription)export.Library)
                    .Project
                    .GetCompilerOptions(target, configuration)
                    .EmitEntryPoint
                    .GetValueOrDefault(false);

                isExe &= target.IsDesktop();

                assemblies = new[] { export.Library.Identity.Name + (isExe ? ".exe" : ".dll") };
            }
            else if (type == LibraryType.ReferenceAssembly)
            {
                assemblies = ResolveReferenceAssembliesPath(libraryAssets);
            }
            else
            {
                assemblies = libraryAssets.Select(libraryAsset => libraryAsset.RelativePath).ToArray();
            }

            if (runtime)
            {
                return new RuntimeLibrary(
                    type.ToString().ToLowerInvariant(),
                    export.Library.Identity.Name,
                    export.Library.Identity.Version.ToString(),
                    export.Library.Hash,
                    assemblies,
                    libraryDependencies.ToArray(),
                    serviceable
                    );
            }
            else
            {
                return new CompilationLibrary(
                    type.ToString().ToLowerInvariant(),
                    export.Library.Identity.Name,
                    export.Library.Identity.Version.ToString(),
                    export.Library.Hash,
                    assemblies,
                    libraryDependencies.ToArray(),
                    serviceable
                   );
            }
        }

        private static string[] ResolveReferenceAssembliesPath(IEnumerable<LibraryAsset> libraryAssets)
        {
            var resolvedPaths = new List<string>();
            var referenceAssembliesPath =
                PathUtility.EnsureTrailingSlash(FrameworkReferenceResolver.Default.ReferenceAssembliesPath);
            foreach (var libraryAsset in libraryAssets)
            {
                // If resolved path is under ReferenceAssembliesPath store it as a relative to it
                // if not, save only assembly name and try to find it somehow later
                if (libraryAsset.ResolvedPath.StartsWith(referenceAssembliesPath))
                {
                    resolvedPaths.Add(libraryAsset.ResolvedPath.Substring(referenceAssembliesPath.Length));
                }
                else
                {
                    resolvedPaths.Add(Path.GetFileName(libraryAsset.ResolvedPath));
                }
            }
            return resolvedPaths.ToArray();
        }
    }
}
