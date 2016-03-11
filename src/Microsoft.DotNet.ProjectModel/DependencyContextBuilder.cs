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
    public class DependencyContextBuilder
    {
        private readonly string _referenceAssembliesPath;

        public DependencyContextBuilder() : this(FrameworkReferenceResolver.Default.ReferenceAssembliesPath)
        {
        }

        public DependencyContextBuilder(string referenceAssembliesPath)
        {
            _referenceAssembliesPath = referenceAssembliesPath;
        }

        public DependencyContext Build(CommonCompilerOptions compilerOptions,
            IEnumerable<LibraryExport> compilationExports,
            IEnumerable<LibraryExport> runtimeExports,
            bool portable,
            NuGetFramework target,
            string runtime)
        {
            if (compilationExports == null)
            {
                compilationExports = Enumerable.Empty<LibraryExport>();
            }

            var dependencyLookup = compilationExports
                .Concat(runtimeExports)
                .Select(export => export.Library.Identity)
                .Distinct()
                .Select(identity => new Dependency(identity.Name, identity.Version.ToString()))
                .ToDictionary(dependency => dependency.Name);

            var compilationOptions = compilerOptions != null
                ? GetCompilationOptions(compilerOptions)
                : CompilationOptions.Default;
            return new DependencyContext(
                target.DotNetFrameworkName,
                runtime,
                portable,
                compilationOptions,
                GetLibraries(compilationExports, dependencyLookup, runtime: false).Cast<CompilationLibrary>(),
                GetLibraries(runtimeExports, dependencyLookup, runtime: true).Cast<RuntimeLibrary>(),
                new KeyValuePair<string, string[]>[0]);
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
                compilerOptions.DebugType,
                compilerOptions.EmitEntryPoint,
                compilerOptions.GenerateXmlDocumentation);
        }

        private IEnumerable<Library> GetLibraries(IEnumerable<LibraryExport> exports,
            IDictionary<string, Dependency> dependencyLookup,
            bool runtime)
        {
            return exports.Select(export => GetLibrary(export, runtime, dependencyLookup));
        }

        private Library GetLibrary(LibraryExport export,
            bool runtime,
            IDictionary<string, Dependency> dependencyLookup)
        {
            var type = export.Library.Identity.Type;

            var serviceable = (export.Library as PackageDescription)?.Library.IsServiceable ?? false;
            var libraryDependencies = new HashSet<Dependency>();

            var libraryAssets = runtime ? export.RuntimeAssemblies : export.CompilationAssemblies;

            foreach (var libraryDependency in export.Library.Dependencies)
            {
                // skip build time dependencies
                if (!libraryDependency.Type.HasFlag(
                        LibraryDependencyTypeFlag.MainReference |
                        LibraryDependencyTypeFlag.MainExport |
                        LibraryDependencyTypeFlag.RuntimeComponent |
                        LibraryDependencyTypeFlag.BecomesNupkgDependency))
                {
                    continue;
                }

                Dependency dependency;
                if (dependencyLookup.TryGetValue(libraryDependency.Name, out dependency))
                {
                    libraryDependencies.Add(dependency);
                }
            }

            IEnumerable<string> assemblies;
            if (type == LibraryType.ReferenceAssembly)
            {
                assemblies = ResolveReferenceAssembliesPath(libraryAssets);
            }
            else
            {
                assemblies = libraryAssets.Select(libraryAsset => libraryAsset.RelativePath);
            }

            if (runtime)
            {
                return new RuntimeLibrary(
                    type.ToString().ToLowerInvariant(),
                    export.Library.Identity.Name,
                    export.Library.Identity.Version.ToString(),
                    export.Library.Hash,
                    assemblies.Select(RuntimeAssembly.Create),
                    export.ResourceAssemblies.Select(CreateResourceAssembly),
                    export.RuntimeTargets.Select(CreateRuntimeTarget),
                    libraryDependencies,
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
                    libraryDependencies,
                    serviceable
                   );
            }
        }

        private ResourceAssembly CreateResourceAssembly(LibraryResourceAssembly resourceAssembly)
        {
            return new ResourceAssembly(
                path: resourceAssembly.Asset.RelativePath,
                locale: resourceAssembly.Locale
                );
        }

        private RuntimeTarget CreateRuntimeTarget(LibraryRuntimeTarget runtimeTarget)
        {
            return new RuntimeTarget(
                runtime: runtimeTarget.Runtime,
                assemblies: runtimeTarget.RuntimeAssemblies.Select(a => RuntimeAssembly.Create(a.RelativePath)),
                nativeLibraries: runtimeTarget.NativeLibraries.Select(l => l.RelativePath)
                );
        }

        private IEnumerable<string> ResolveReferenceAssembliesPath(IEnumerable<LibraryAsset> libraryAssets)
        {
            var referenceAssembliesPath =
                PathUtility.EnsureTrailingSlash(_referenceAssembliesPath);
            foreach (var libraryAsset in libraryAssets)
            {
                // If resolved path is under ReferenceAssembliesPath store it as a relative to it
                // if not, save only assembly name and try to find it somehow later
                if (libraryAsset.ResolvedPath.StartsWith(referenceAssembliesPath))
                {
                    yield return libraryAsset.ResolvedPath.Substring(referenceAssembliesPath.Length);
                }
                else
                {
                    yield return Path.GetFileName(libraryAsset.ResolvedPath);
                }
            }
        }
    }
}
