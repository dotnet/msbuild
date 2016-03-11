// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ProjectModel.Compilation.Preprocessor;
using Microsoft.DotNet.ProjectModel.Files;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.DotNet.ProjectModel.Resolution;
using Microsoft.DotNet.ProjectModel.Utilities;
using Microsoft.DotNet.Tools.Compiler;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectModel.Compilation
{
    public class LibraryExporter
    {
        private readonly string _configuration;
        private readonly string _runtime;
        private readonly ProjectDescription _rootProject;
        private readonly string _buildBasePath;
        private readonly string _solutionRootPath;

        public LibraryExporter(ProjectDescription rootProject,
            LibraryManager manager,
            string configuration,
            string runtime,
            string buildBasePath,
            string solutionRootPath)
        {
            if (string.IsNullOrEmpty(configuration))
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            LibraryManager = manager;
            _configuration = configuration;
            _runtime = runtime;
            _buildBasePath = buildBasePath;
            _solutionRootPath = solutionRootPath;
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
                var sourceReferences = new List<LibraryAsset>();
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

                yield return LibraryExportBuilder.Create(library)
                    .WithCompilationAssemblies(compilationAssemblies)
                    .WithSourceReferences(sourceReferences)
                    .WithRuntimeAssemblies(libraryExport.RuntimeAssemblies)
                    .WithRuntimeAssets(libraryExport.RuntimeAssets)
                    .WithNativeLibraries(libraryExport.NativeLibraries)
                    .WithEmbedddedResources(libraryExport.EmbeddedResources)
                    .WithAnalyzerReference(analyzerReferences)
                    .WithResourceAssemblies(libraryExport.ResourceAssemblies)
                    .WithRuntimeTargets(libraryExport.RuntimeTargets)
                    .Build();
            }
        }

        /// <summary>
        /// Create a LibraryExport from LibraryDescription.
        ///
        /// When the library is not resolved the LibraryExport is created nevertheless.
        /// </summary>
        private LibraryExport GetExport(LibraryDescription library)
        {
            if (!library.Resolved)
            {
                // For a unresolved project reference returns a export with empty asset.
                return LibraryExportBuilder.Create(library).Build();
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
            var builder = LibraryExportBuilder.Create(package);
            builder.WithNativeLibraries(PopulateAssets(package, package.NativeLibraries));
            builder.WithRuntimeAssemblies(PopulateAssets(package, package.RuntimeAssemblies));
            builder.WithCompilationAssemblies(PopulateAssets(package, package.CompileTimeAssemblies));
            builder.WithSourceReferences(GetSharedSources(package));
            builder.WithAnalyzerReference(GetAnalyzerReferences(package));

            if (package.ContentFiles.Any())
            {
                var parameters = PPFileParameters.CreateForProject(_rootProject.Project);
                Action<Stream, Stream> transform = (input, output) => PPFilePreprocessor.Preprocess(input, output, parameters);

                var sourceCodeLanguage = _rootProject.Project.GetSourceCodeLanguage();
                var languageGroups = package.ContentFiles.GroupBy(file => file.CodeLanguage);
                var selectedGroup = languageGroups.FirstOrDefault(g => g.Key == sourceCodeLanguage) ??
                                    languageGroups.FirstOrDefault(g => g.Key == null);
                if (selectedGroup != null)
                {
                    foreach (var contentFile in selectedGroup)
                    {
                        if (contentFile.CodeLanguage != null &&
                            string.Compare(contentFile.CodeLanguage, sourceCodeLanguage, StringComparison.OrdinalIgnoreCase) != 0)
                        {
                            continue;
                        }

                        var fileTransform = contentFile.PPOutputPath != null ? transform : null;

                        var fullPath = Path.Combine(package.Path, contentFile.Path);
                        if (contentFile.BuildAction == BuildAction.Compile)
                        {
                            builder.AddSourceReference(LibraryAsset.CreateFromRelativePath(package.Path, contentFile.Path, fileTransform));
                        }
                        else if (contentFile.BuildAction == BuildAction.EmbeddedResource)
                        {
                            builder.AddEmbedddedResource(LibraryAsset.CreateFromRelativePath(package.Path, contentFile.Path, fileTransform));
                        }
                        if (contentFile.CopyToOutput)
                        {
                            builder.AddRuntimeAsset(new LibraryAsset(contentFile.Path, contentFile.OutputPath, fullPath, fileTransform));
                        }
                    }
                }
            }
            if (package.RuntimeTargets.Any())
            {
                foreach (var targetGroup in package.RuntimeTargets.GroupBy(t => t.Runtime))
                {
                    var runtime = new List<LibraryAsset>();
                    var native = new List<LibraryAsset>();

                    foreach (var lockFileRuntimeTarget in targetGroup)
                    {
                        if (string.Equals(lockFileRuntimeTarget.AssetType, "native", StringComparison.OrdinalIgnoreCase))
                        {
                            native.Add(LibraryAsset.CreateFromRelativePath(package.Path, lockFileRuntimeTarget.Path));
                        }
                        else if (string.Equals(lockFileRuntimeTarget.AssetType, "runtime", StringComparison.OrdinalIgnoreCase))
                        {
                            runtime.Add(LibraryAsset.CreateFromRelativePath(package.Path, lockFileRuntimeTarget.Path));
                        }
                    }

                    builder.AddRuntimeTarget(new LibraryRuntimeTarget(targetGroup.Key, runtime, native));
                }
            }

            return builder.Build();
        }

        private LibraryExport ExportProject(ProjectDescription project)
        {
            var builder = LibraryExportBuilder.Create(project);

            if (!string.IsNullOrEmpty(project.TargetFrameworkInfo?.AssemblyPath))
            {
                // Project specifies a pre-compiled binary. We're done!
                var assemblyPath = ResolvePath(project.Project, _configuration, project.TargetFrameworkInfo.AssemblyPath);
                var pdbPath = Path.ChangeExtension(assemblyPath, "pdb");

                var compileAsset = new LibraryAsset(
                    project.Project.Name,
                    Path.GetFileName(assemblyPath),
                    assemblyPath);

                builder.AddCompilationAssembly(compileAsset);
                builder.AddRuntimeAssembly(compileAsset);
                if (File.Exists(pdbPath))
                {
                    builder.AddRuntimeAsset(new LibraryAsset(Path.GetFileName(pdbPath), Path.GetFileName(pdbPath), pdbPath));
                }
            }
            else if (project.Project.Files.SourceFiles.Any())
            {
                var outputPaths = project.GetOutputPaths(_buildBasePath, _solutionRootPath, _configuration, _runtime);

                var compilationAssembly = outputPaths.CompilationFiles.Assembly;
                var compilationAssemblyAsset = LibraryAsset.CreateFromAbsolutePath(
                    outputPaths.CompilationFiles.BasePath,
                    compilationAssembly);

                builder.AddCompilationAssembly(compilationAssemblyAsset);

                if (ExportsRuntime(project))
                {
                    var runtimeAssemblyAsset = LibraryAsset.CreateFromAbsolutePath(
                        outputPaths.RuntimeFiles.BasePath,
                        outputPaths.RuntimeFiles.Assembly);

                    builder.AddRuntimeAssembly(runtimeAssemblyAsset);
                    builder.WithRuntimeAssets(CollectAssets(outputPaths.RuntimeFiles));
                }
                else
                {
                    builder.AddRuntimeAssembly(compilationAssemblyAsset);
                    builder.WithRuntimeAssets(CollectAssets(outputPaths.CompilationFiles));
                }
            }

            builder.WithSourceReferences(project.Project.Files.SharedFiles.Select(f =>
                LibraryAsset.CreateFromAbsolutePath(project.Path, f)
            ));

            return builder.Build();
        }

        private IEnumerable<LibraryAsset> CollectAssets(CompilationOutputFiles files)
        {
            var assemblyPath = files.Assembly;
            foreach (var path in files.All())
            {
                if (string.Equals(assemblyPath, path))
                {
                    continue;
                }
                yield return LibraryAsset.CreateFromAbsolutePath(files.BasePath, path);
            }
        }

        private bool ExportsRuntime(ProjectDescription project)
        {
            return project == _rootProject &&
                   !string.IsNullOrWhiteSpace(_runtime) &&
                   project.Project.HasRuntimeOutput(_configuration);
        }

        private static string ResolvePath(Project project, string configuration, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            path = PathUtility.GetPathWithDirectorySeparator(path);

            path = path.Replace("{configuration}", configuration);

            return Path.Combine(project.ProjectDirectory, path);
        }

        private LibraryExport ExportFrameworkLibrary(LibraryDescription library)
        {
            // We assume the path is to an assembly. Framework libraries only export compile-time stuff
            // since they assume the runtime library is present already
            var builder = LibraryExportBuilder.Create(library);
            if (!string.IsNullOrEmpty(library.Path))
            {
                builder.WithCompilationAssemblies(new []
                {
                    new LibraryAsset(library.Identity.Name, null, library.Path)
                });
            }
            return builder.Build();
        }

        private IEnumerable<LibraryAsset> GetSharedSources(PackageDescription package)
        {
            return package
                .Library
                .Files
                .Where(path => path.StartsWith("shared" + Path.DirectorySeparatorChar))
                .Select(path => LibraryAsset.CreateFromRelativePath(package.Path, path));
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


        private IEnumerable<LibraryAsset> PopulateAssets(PackageDescription package, IEnumerable<LockFileItem> section)
        {
            foreach (var assemblyPath in section)
            {
                yield return LibraryAsset.CreateFromRelativePath(package.Path, assemblyPath.Path);
            }
        }

        private static bool LibraryIsOfType(LibraryType type, LibraryDescription library)
        {
            return type.Equals(LibraryType.Unspecified) || // No type filter was requested
                   library.Identity.Type.Equals(type);     // OR, library type matches requested type
        }
    }
}
