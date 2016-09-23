// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Compilation;
using Microsoft.DotNet.ProjectModel.Resolution;
using Microsoft.DotNet.ProjectModel.Utilities;
using NuGet.Frameworks;
using NuGet.LibraryModel;

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
                .ToDictionary(dependency => dependency.Name, StringComparer.OrdinalIgnoreCase);

            var compilationOptions = compilerOptions != null
                ? GetCompilationOptions(compilerOptions)
                : CompilationOptions.Default;

            var runtimeSignature = GenerateRuntimeSignature(runtimeExports);

            return new DependencyContext(
                new TargetInfo(target.DotNetFrameworkName, runtime, runtimeSignature, portable),
                compilationOptions,
                GetLibraries(compilationExports, dependencyLookup, runtime: false).Cast<CompilationLibrary>(),
                GetLibraries(runtimeExports, dependencyLookup, runtime: true).Cast<RuntimeLibrary>(),
                new RuntimeFallbacks[] {});
        }

        private static string GenerateRuntimeSignature(IEnumerable<LibraryExport> runtimeExports)
        {
            var sha1 = SHA1.Create();
            var builder = new StringBuilder();
            var packages = runtimeExports
                .Where(libraryExport => libraryExport.Library.Identity.Type == LibraryType.Package);
            var seperator = "|";
            foreach (var libraryExport in packages)
            {
                builder.Append(libraryExport.Library.Identity.Name);
                builder.Append(seperator);
                builder.Append(libraryExport.Library.Identity.Version.ToString());
                builder.Append(seperator);
            }
            var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));

            builder.Clear();
            foreach (var b in hash)
            {
                builder.AppendFormat("{0:x2}", b);
            }
            return builder.ToString();
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

            // TEMPORARY: All packages are serviceable in RC2
            // See https://github.com/dotnet/cli/issues/2569
            var serviceable = (export.Library as PackageDescription) != null;
            var libraryDependencies = new HashSet<Dependency>();

            foreach (var libraryDependency in export.Library.Dependencies)
            {
                // skip build time dependencies
                if (libraryDependency.Type.Equals(LibraryDependencyType.Build))
                {
                    continue;
                }

                Dependency dependency;
                if (dependencyLookup.TryGetValue(libraryDependency.Name, out dependency))
                {
                    libraryDependencies.Add(dependency);
                }
            }

            if (runtime)
            {
                return new RuntimeLibrary(
                    type.ToString().ToLowerInvariant(),
                    export.Library.Identity.Name,
                    export.Library.Identity.Version.ToString(),
                    export.Library.Hash,
                    export.RuntimeAssemblyGroups.Select(CreateRuntimeAssetGroup).ToArray(),
                    export.NativeLibraryGroups.Select(CreateRuntimeAssetGroup).ToArray(),
                    export.ResourceAssemblies.Select(CreateResourceAssembly),
                    libraryDependencies,
                    serviceable,
                    GetLibraryPath(export.Library),
                    GetLibraryHashPath(export.Library));
            }
            else
            {
                IEnumerable<string> assemblies;
                if (type == LibraryType.Reference)
                {
                    assemblies = ResolveReferenceAssembliesPath(export.CompilationAssemblies);
                }
                else
                {
                    assemblies = export.CompilationAssemblies.Select(libraryAsset => libraryAsset.RelativePath);
                }

                return new CompilationLibrary(
                    type.ToString().ToLowerInvariant(),
                    export.Library.Identity.Name,
                    export.Library.Identity.Version.ToString(),
                    export.Library.Hash,
                    assemblies,
                    libraryDependencies,
                    serviceable,
                    GetLibraryPath(export.Library),
                    GetLibraryHashPath(export.Library));
            }
        }

        private string GetLibraryPath(LibraryDescription description)
        {
            var packageDescription = description as PackageDescription;

            if (packageDescription != null)
            {
                // This is the relative path appended to a NuGet packages directory to find the directory containing
                // the package assets. This string should only be mastered by NuGet.
                return packageDescription.PackageLibrary?.Path;
            }

            return null;
        }

        private string GetLibraryHashPath(LibraryDescription description)
        {
            var packageDescription = description as PackageDescription;

            if (packageDescription != null)
            {
                // This hash path appended to the package path (much like package assets). This string should only be
                // mastered by NuGet.
                return packageDescription.HashPath;
            }

            return null;
        }

        private RuntimeAssetGroup CreateRuntimeAssetGroup(LibraryAssetGroup libraryAssetGroup)
        {
            return new RuntimeAssetGroup(
                libraryAssetGroup.Runtime,
                libraryAssetGroup.Assets.Select(a => a.RelativePath));
        }

        private ResourceAssembly CreateResourceAssembly(LibraryResourceAssembly resourceAssembly)
        {
            return new ResourceAssembly(
                path: resourceAssembly.Asset.RelativePath,
                locale: resourceAssembly.Locale
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
