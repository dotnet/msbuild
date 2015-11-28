// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.DotNet.ProjectModel.Resolution;
using Microsoft.DotNet.ProjectModel.Utilities;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectModel.Compilation
{
    public class LibraryExporter
    {
        private readonly string _configuration;
        private readonly ProjectDescription _rootProject;

        public LibraryExporter(ProjectDescription rootProject, LibraryManager manager, string configuration)
        {
            if (string.IsNullOrEmpty(configuration))
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            LibraryManager = manager;
            _configuration = configuration;
            _rootProject = rootProject;
        }

        public LibraryManager LibraryManager { get; }

        /// <summary>
        /// Gets all the exports specified by this project, including the root project itself
        /// </summary>
        public IEnumerable<LibraryExport> GetAllExports()
        {
            return ExportLibraries(_ => true);
        }

        /// <summary>
        /// Gets all exports required by the project, NOT including the project itself
        /// </summary>
        /// <returns></returns>
        public IEnumerable<LibraryExport> GetDependencies()
        {
            return GetDependencies(LibraryType.Unspecified);
        }

        /// <summary>
        /// Gets all exports required by the project, of the specified <see cref="LibraryType"/>, NOT including the project itself
        /// </summary>
        /// <returns></returns>
        public IEnumerable<LibraryExport> GetDependencies(LibraryType type)
        {
            // Export all but the main project
            return ExportLibraries(library =>
                library != _rootProject &&
                LibraryIsOfType(type, library));
        }

        /// <summary>
        /// Retrieves a list of <see cref="LibraryExport"/> objects representing the assets
        /// required from other libraries to compile this project.
        /// </summary>
        private IEnumerable<LibraryExport> ExportLibraries(Func<LibraryDescription, bool> condition)
        {
            var seenMetadataReferences = new HashSet<string>();

            // Iterate over libraries in the library manager
            foreach (var library in LibraryManager.GetLibraries())
            {
                if (!condition(library))
                {
                    continue;
                }

                var compilationAssemblies = new List<LibraryAsset>();
                var sourceReferences = new List<string>();

                var libraryExport = GetExport(library);

                if (libraryExport != null)
                {
                    // We need to filter out source references from non-root libraries,
                    //  so we rebuild the library export
                    foreach (var reference in libraryExport.CompilationAssemblies)
                    {
                        if (seenMetadataReferences.Add(reference.Name))
                        {
                            compilationAssemblies.Add(reference);
                        }
                    }

                    if (library.Parents.Contains(_rootProject))
                    {
                        // Only process source references for direct dependencies
                        foreach (var sourceReference in libraryExport.SourceReferences)
                        {
                            sourceReferences.Add(sourceReference);
                        }
                    }

                    yield return new LibraryExport(library, compilationAssemblies, sourceReferences, libraryExport.RuntimeAssemblies, libraryExport.NativeLibraries);
                }
            }
        }

        private LibraryExport GetExport(LibraryDescription library)
        {
            // Don't even try to export unresolved libraries
            if (!library.Resolved)
            {
                return null;
            }

            if (Equals(LibraryType.Package, library.Identity.Type))
            {
                return ExportPackage((PackageDescription)library);
            }
            else if (Equals(LibraryType.Project, library.Identity.Type))
            {
                return ExportProject((ProjectDescription)library);
            }
            else
            {
                return ExportFrameworkLibrary(library);
            }
        }

        private LibraryExport ExportPackage(PackageDescription package)
        {
            var nativeLibraries = new List<LibraryAsset>();
            PopulateAssets(package, package.Target.NativeLibraries, nativeLibraries);

            var runtimeAssemblies = new List<LibraryAsset>();
            PopulateAssets(package, package.Target.RuntimeAssemblies, runtimeAssemblies);

            var compileAssemblies = new List<LibraryAsset>();
            PopulateAssets(package, package.Target.CompileTimeAssemblies, compileAssemblies);

            var sourceReferences = new List<string>();
            foreach (var sharedSource in GetSharedSources(package))
            {
                sourceReferences.Add(sharedSource);
            }

            return new LibraryExport(package, compileAssemblies, sourceReferences, runtimeAssemblies, nativeLibraries);
        }

        private LibraryExport ExportProject(ProjectDescription project)
        {
            var compileAssemblies = new List<LibraryAsset>();
            var sourceReferences = new List<string>();

            if (!string.IsNullOrEmpty(project.TargetFrameworkInfo?.AssemblyPath))
            {
                // Project specifies a pre-compiled binary. We're done!
                var assemblyPath = ResolvePath(project.Project, _configuration, project.TargetFrameworkInfo.AssemblyPath);
                compileAssemblies.Add(new LibraryAsset(
                    project.Project.Name,
                    assemblyPath,
                    Path.Combine(project.Project.ProjectDirectory, assemblyPath)));
            }

            // Add shared sources
            foreach (var sharedFile in project.Project.Files.SharedFiles)
            {
                sourceReferences.Add(sharedFile);
            }

            // No support for ref or native in projects, so runtimeAssemblies is just the same as compileAssemblies and nativeLibraries are empty
            return new LibraryExport(project, compileAssemblies, sourceReferences, compileAssemblies, Enumerable.Empty<LibraryAsset>());
        }

        private static string ResolvePath(Project project, string configuration, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            path = PathUtility.GetPathWithDirectorySeparator(path);

            path = path.Replace("{configuration}", configuration);

            return path;
        }

        private LibraryExport ExportFrameworkLibrary(LibraryDescription library)
        {
            if (string.IsNullOrEmpty(library.Path))
            {
                return null;
            }

            // We assume the path is to an assembly. Framework libraries only export compile-time stuff
            // since they assume the runtime library is present already
            return new LibraryExport(
                library,
                new[] { new LibraryAsset(library.Identity.Name, library.Path, library.Path) },
                Enumerable.Empty<string>(),
                Enumerable.Empty<LibraryAsset>(),
                Enumerable.Empty<LibraryAsset>());
        }

        private IEnumerable<string> GetSharedSources(PackageDescription package)
        {
            return package
                .Library
                .Files
                .Where(path => path.StartsWith("shared" + Path.DirectorySeparatorChar))
                .Select(path => Path.Combine(package.Path, path));
        }


        private void PopulateAssets(PackageDescription package, IEnumerable<LockFileItem> section, IList<LibraryAsset> assets)
        {
            foreach (var assemblyPath in section)
            {
                if (IsPlaceholderFile(assemblyPath))
                {
                    continue;
                }

                assets.Add(new LibraryAsset(
                    Path.GetFileNameWithoutExtension(assemblyPath),
                    assemblyPath,
                    Path.Combine(package.Path, assemblyPath)));
            }
        }

        private static bool IsPlaceholderFile(string path)
        {
            return string.Equals(Path.GetFileName(path), "_._", StringComparison.Ordinal);
        }

        private static bool LibraryIsOfType(LibraryType type, LibraryDescription library)
        {
            return type.Equals(LibraryType.Unspecified) || // No type filter was requested 
                   library.Identity.Type.Equals(type);     // OR, library type matches requested type
        }
    }
}
