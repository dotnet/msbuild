// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Extensions.ProjectModel.Graph;
using Microsoft.Extensions.ProjectModel.Resolution;
using Microsoft.Extensions.ProjectModel.Utilities;
using NuGet.Frameworks;

namespace Microsoft.Extensions.ProjectModel.Compilation
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

        public IEnumerable<LibraryExport> GetAllExports()
        {
            // Export all but the main project
            return ExportLibraries(_ => true);
        }

        public IEnumerable<LibraryExport> GetCompilationDependencies()
        {
            // Export all but the main project
            return ExportLibraries(library => library != _rootProject);
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
            else
            {
                // Add the project output to the metadata references, if there is source code
                var outputPath = GetOutputPath(project);
                if (project.Project.Files.SourceFiles.Any())
                {
                    compileAssemblies.Add(new LibraryAsset(
                        project.Project.Name,
                        outputPath,
                        Path.Combine(project.Project.ProjectDirectory, outputPath)));
                }
            }

            // Add shared sources
            foreach (var sharedFile in project.Project.Files.SharedFiles)
            {
                sourceReferences.Add(sharedFile);
            }

            // No support for ref or native in projects, so runtimeAssemblies is just the same as compileAssemblies and nativeLibraries are empty
            return new LibraryExport(project, compileAssemblies, sourceReferences, compileAssemblies, Enumerable.Empty<LibraryAsset>());
        }

        private string GetOutputPath(ProjectDescription project)
        {
            var compilationOptions = project.Project.GetCompilerOptions(project.Framework, _configuration);
            return Path.Combine(
                "bin", // This can't access the Constant in Cli Utils. But the output path stuff is temporary right now anyway
                _configuration,
                project.Framework.GetTwoDigitShortFolderName(),
                project.Project.Name + ".dll");
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
                if (PathUtility.IsPlaceholderFile(assemblyPath))
                {
                    continue;
                }

                assets.Add(new LibraryAsset(
                    Path.GetFileNameWithoutExtension(assemblyPath),
                    assemblyPath,
                    Path.Combine(package.Path, assemblyPath)));
            }
        }
    }
}
