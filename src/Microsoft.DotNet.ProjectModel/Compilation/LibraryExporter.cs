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
                var analyzerReferences = new List<AnalyzerReference>();
                var libraryExport = GetExport(library);


                // We need to filter out source references from non-root libraries,
                // so we rebuild the library export
                foreach (var reference in libraryExport.CompilationAssemblies)
                {
                    if (seenMetadataReferences.Add(reference.Name))
                    {
                        compilationAssemblies.Add(reference);
                    }
                }

                // Source and analyzer references are not transitive
                if (library.Parents.Contains(_rootProject))
                {
                    sourceReferences.AddRange(libraryExport.SourceReferences);
                    analyzerReferences.AddRange(libraryExport.AnalyzerReferences);
                }

                yield return new LibraryExport(library,
                                               compilationAssemblies,
                                               sourceReferences,
                                               libraryExport.RuntimeAssemblies,
                                               libraryExport.RuntimeAssets,
                                               libraryExport.NativeLibraries,
                                               analyzerReferences);
            }
        }

        /// <summary>
        /// Create a LibraryExport from LibraryDescription. 
        /// 
        /// When the library is not resolved the LibraryExport is created nevertheless.
        /// </summary>
        private LibraryExport GetExport(LibraryDescription library)
        {
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

            var analyzers = GetAnalyzerReferences(package);

            return new LibraryExport(package, compileAssemblies,
                sourceReferences, runtimeAssemblies, Array.Empty<string>(), nativeLibraries, analyzers);
        }

        private LibraryExport ExportProject(ProjectDescription project)
        {
            var compileAssemblies = new List<LibraryAsset>();
            var runtimeAssets = new List<string>();
            var sourceReferences = new List<string>();

            if (!string.IsNullOrEmpty(project.TargetFrameworkInfo?.AssemblyPath))
            {
                // Project specifies a pre-compiled binary. We're done!
                var assemblyPath = ResolvePath(project.Project, _configuration, project.TargetFrameworkInfo.AssemblyPath);
                var pdbPath = Path.ChangeExtension(assemblyPath, "pdb");

                var compileAsset = new LibraryAsset(
                    project.Project.Name,
                    null,
                    Path.GetFullPath(Path.Combine(project.Project.ProjectDirectory, assemblyPath)));

                compileAssemblies.Add(compileAsset);
                runtimeAssets.Add(pdbPath);
            }
            else
            {
                var outputCalculator = project.GetOutputPathCalculator();
                var assemblyPath = outputCalculator.GetAssemblyPath(_configuration);
                compileAssemblies.Add(new LibraryAsset(project.Identity.Name, null, assemblyPath));

                foreach (var path in outputCalculator.GetBuildOutputs(_configuration))
                {
                    if (string.Equals(assemblyPath, path))
                    {
                        continue;
                    }

                    runtimeAssets.Add(path);
                }
            }

            // Add shared sources
            foreach (var sharedFile in project.Project.Files.SharedFiles)
            {
                sourceReferences.Add(sharedFile);
            }

            // No support for ref or native in projects, so runtimeAssemblies is
            // just the same as compileAssemblies and nativeLibraries are empty
            // Also no support for analyzer projects
            return new LibraryExport(project, compileAssemblies, sourceReferences,
                compileAssemblies, runtimeAssets, Array.Empty<LibraryAsset>(), Array.Empty<AnalyzerReference>());
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
            // We assume the path is to an assembly. Framework libraries only export compile-time stuff
            // since they assume the runtime library is present already
            return new LibraryExport(
                library,
                string.IsNullOrEmpty(library.Path) ?
                    Array.Empty<LibraryAsset>() :
                    new[] { new LibraryAsset(library.Identity.Name, library.Path, library.Path) },
                Array.Empty<string>(),
                Array.Empty<LibraryAsset>(),
                Array.Empty<string>(),
                Array.Empty<LibraryAsset>(),
                Array.Empty<AnalyzerReference>());
        }

        private IEnumerable<string> GetSharedSources(PackageDescription package)
        {
            return package
                .Library
                .Files
                .Where(path => path.StartsWith("shared" + Path.DirectorySeparatorChar))
                .Select(path => Path.Combine(package.Path, path));
        }

        private IEnumerable<AnalyzerReference> GetAnalyzerReferences(PackageDescription package)
        {
            var analyzers = package
                .Library
                .Files
                .Where(path => path.StartsWith("analyzers" + Path.DirectorySeparatorChar) &&
                               path.EndsWith(".dll"));

            var analyzerRefs = new List<AnalyzerReference>();
            // See https://docs.nuget.org/create/analyzers-conventions for the analyzer
            // NuGet specification
            foreach (var analyzer in analyzers)
            {
                var specifiers = analyzer.Split(Path.DirectorySeparatorChar);

                var assemblyPath = Path.Combine(package.Path, analyzer);

                // $/analyzers/{Framework Name}{Version}/{Supported Architecture}/{Supported Programming Language}/{Analyzer}.dll 
                switch (specifiers.Length)
                {
                    // $/analyzers/{analyzer}.dll
                    case 2:
                        analyzerRefs.Add(new AnalyzerReference(
                            assembly: assemblyPath,
                            framework: null,
                            language: null,
                            runtimeIdentifier: null
                        ));
                        break;

                    // $/analyzers/{framework}/{analyzer}.dll
                    case 3:
                        analyzerRefs.Add(new AnalyzerReference(
                            assembly: assemblyPath,
                            framework: NuGetFramework.Parse(specifiers[1]),
                            language: null,
                            runtimeIdentifier: null
                        ));
                        break;

                    // $/analyzers/{framework}/{language}/{analyzer}.dll
                    case 4:
                        analyzerRefs.Add(new AnalyzerReference(
                            assembly: assemblyPath,
                            framework: NuGetFramework.Parse(specifiers[1]),
                            language: specifiers[2],
                            runtimeIdentifier: null
                        ));
                        break;

                    // $/analyzers/{framework}/{runtime}/{language}/{analyzer}.dll
                    case 5:
                        analyzerRefs.Add(new AnalyzerReference(
                            assembly: assemblyPath,
                            framework: NuGetFramework.Parse(specifiers[1]),
                            language: specifiers[3],
                            runtimeIdentifier: specifiers[2]
                        ));
                        break;

                        // Anything less than 2 specifiers or more than 4 is
                        // illegal according to the specification and will be
                        // ignored
                }
            }
            return analyzerRefs;
        }


        private void PopulateAssets(PackageDescription package, IEnumerable<LockFileItem> section, IList<LibraryAsset> assets)
        {
            foreach (var assemblyPath in section)
            {
                assets.Add(new LibraryAsset(
                    Path.GetFileNameWithoutExtension(assemblyPath),
                    assemblyPath,
                    Path.Combine(package.Path, assemblyPath)));
            }
        }

        private static bool LibraryIsOfType(LibraryType type, LibraryDescription library)
        {
            return type.Equals(LibraryType.Unspecified) || // No type filter was requested 
                   library.Identity.Type.Equals(type);     // OR, library type matches requested type
        }
    }
}
