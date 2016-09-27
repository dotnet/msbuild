// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ProjectModel.Compilation.Preprocessor;
using Microsoft.DotNet.ProjectModel.Files;
using Microsoft.DotNet.ProjectModel.Resolution;
using Microsoft.DotNet.ProjectModel.Utilities;
using Microsoft.DotNet.Tools.Compiler;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.ProjectModel.Compilation
{
    public class LibraryExporter
    {
        private readonly string _configuration;
        private readonly string _runtime;
        private readonly string[] _runtimeFallbacks;
        private readonly ProjectDescription _rootProject;
        private readonly string _buildBasePath;
        private readonly string _solutionRootPath;

        public LibraryExporter(ProjectDescription rootProject,
            LibraryManager manager,
            string configuration,
            string runtime,
            string[] runtimeFallbacks,
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
            _runtimeFallbacks = runtimeFallbacks;
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
            return GetDependencies(null);
        }

        /// <summary>
        /// Gets all exports required by the project, of the specified <see cref="LibraryType"/>, NOT including the project itself
        /// </summary>
        /// <returns></returns>
        public IEnumerable<LibraryExport> GetDependencies(LibraryType? type)
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

                var builder = LibraryExportBuilder.Create(library);
                if (_runtime != null && _runtimeFallbacks != null)
                {
                    // For portable apps that are built with runtime trimming we replace RuntimeAssemblyGroups and NativeLibraryGroups
                    // with single default group that contains asset specific to runtime we are trimming for
                    // based on runtime fallback list
                    builder.WithRuntimeAssemblyGroups(TrimAssetGroups(libraryExport.RuntimeAssemblyGroups, _runtimeFallbacks));
                    builder.WithNativeLibraryGroups(TrimAssetGroups(libraryExport.NativeLibraryGroups, _runtimeFallbacks));
                }
                else
                {
                    builder.WithRuntimeAssemblyGroups(libraryExport.RuntimeAssemblyGroups);
                    builder.WithNativeLibraryGroups(libraryExport.NativeLibraryGroups);
                }

                yield return builder
                    .WithCompilationAssemblies(compilationAssemblies)
                    .WithSourceReferences(sourceReferences)
                    .WithRuntimeAssets(libraryExport.RuntimeAssets)
                    .WithEmbedddedResources(libraryExport.EmbeddedResources)
                    .WithAnalyzerReference(analyzerReferences)
                    .WithResourceAssemblies(libraryExport.ResourceAssemblies)
                    .Build();
            }
        }

        private IEnumerable<LibraryAssetGroup> TrimAssetGroups(IEnumerable<LibraryAssetGroup> runtimeAssemblyGroups,
            string[] runtimeFallbacks)
        {
            LibraryAssetGroup runtimeAssets;
            foreach (var rid in runtimeFallbacks)
            {
                runtimeAssets = runtimeAssemblyGroups.GetRuntimeGroup(rid);
                if (runtimeAssets != null)
                {
                    yield return new LibraryAssetGroup(runtimeAssets.Assets);
                    yield break;
                }
            }

            runtimeAssets = runtimeAssemblyGroups.GetDefaultGroup();
            if (runtimeAssets != null)
            {
                yield return runtimeAssets;
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

            var libraryType = library.Identity.Type;
            if (library is TargetLibraryWithAssets)
            {
                return ExportPackage((TargetLibraryWithAssets)library);
            }
            else if (Equals(LibraryType.Project, libraryType))
            {
                return ExportProject((ProjectDescription)library);
            }
            else
            {
                return ExportFrameworkLibrary(library);
            }
        }

        private LibraryExport ExportPackage(TargetLibraryWithAssets library)
        {
            var builder = LibraryExportBuilder.Create(library);
            builder.AddNativeLibraryGroup(new LibraryAssetGroup(PopulateAssets(library, library.NativeLibraries)));
            builder.AddRuntimeAssemblyGroup(new LibraryAssetGroup(PopulateAssets(library, library.RuntimeAssemblies)));
            builder.WithResourceAssemblies(PopulateResources(library, library.ResourceAssemblies));
            builder.WithCompilationAssemblies(PopulateAssets(library, library.CompileTimeAssemblies));

            if (library.Identity.Type.Equals(LibraryType.Package))
            {
                builder.WithSourceReferences(GetSharedSources((PackageDescription) library));
                builder.WithAnalyzerReference(GetAnalyzerReferences((PackageDescription) library));
            }

            if (library.ContentFiles.Any())
            {
                var parameters = PPFileParameters.CreateForProject(_rootProject.Project);
                Action<Stream, Stream> transform = (input, output) => PPFilePreprocessor.Preprocess(input, output, parameters);

                var sourceCodeLanguage = _rootProject.Project.GetSourceCodeLanguage();
                var languageGroups = library.ContentFiles.GroupBy(file => file.CodeLanguage);

                var selectedGroup = languageGroups.FirstOrDefault(g => g.Key == sourceCodeLanguage) ??
                                    languageGroups.FirstOrDefault(g => g.Key == "any") ??
                                    languageGroups.FirstOrDefault(g => g.Key == null);
                if (selectedGroup != null)
                {
                    foreach (var contentFile in selectedGroup)
                    {
                        if (contentFile.CodeLanguage != null &&
                            contentFile.CodeLanguage != "any" &&
                            string.Compare(contentFile.CodeLanguage, sourceCodeLanguage, StringComparison.OrdinalIgnoreCase) != 0)
                        {
                            continue;
                        }

                        var fileTransform = contentFile.PPOutputPath != null ? transform : null;

                        var fullPath = Path.Combine(library.Path, contentFile.Path);
                        if (contentFile.BuildAction == BuildAction.Compile)
                        {
                            builder.AddSourceReference(LibraryAsset.CreateFromRelativePath(library.Path, contentFile.Path, fileTransform));
                        }
                        else if (contentFile.BuildAction == BuildAction.EmbeddedResource)
                        {
                            builder.AddEmbedddedResource(LibraryAsset.CreateFromRelativePath(library.Path, contentFile.Path, fileTransform));
                        }
                        if (contentFile.CopyToOutput)
                        {
                            builder.AddRuntimeAsset(new LibraryAsset(contentFile.Path, contentFile.OutputPath, fullPath, fileTransform));
                        }
                    }
                }
            }
            if (library.RuntimeTargets.Any())
            {
                foreach (var targetGroup in library.RuntimeTargets.GroupBy(t => t.Runtime))
                {
                    var runtime = new List<LibraryAsset>();
                    var native = new List<LibraryAsset>();

                    foreach (var lockFileRuntimeTarget in targetGroup)
                    {
                        if (string.Equals(lockFileRuntimeTarget.AssetType, "native", StringComparison.OrdinalIgnoreCase))
                        {
                            native.Add(LibraryAsset.CreateFromRelativePath(library.Path, lockFileRuntimeTarget.Path));
                        }
                        else if (string.Equals(lockFileRuntimeTarget.AssetType, "runtime", StringComparison.OrdinalIgnoreCase))
                        {
                            runtime.Add(LibraryAsset.CreateFromRelativePath(library.Path, lockFileRuntimeTarget.Path));
                        }
                    }

                    if (runtime.Any())
                    {
                        builder.AddRuntimeAssemblyGroup(new LibraryAssetGroup(targetGroup.Key, runtime.Where(a => !PackageDependencyProvider.IsPlaceholderFile(a.RelativePath))));
                    }

                    if (native.Any())
                    {
                        builder.AddNativeLibraryGroup(new LibraryAssetGroup(targetGroup.Key, native.Where(a => !PackageDependencyProvider.IsPlaceholderFile(a.RelativePath))));
                    }
                }
            }

            return builder.Build();
        }

        private LibraryExport ExportProject(ProjectDescription project)
        {
            var builder = LibraryExportBuilder.Create(project);
            var compilerOptions = project.Project.GetCompilerOptions(project.TargetFrameworkInfo.FrameworkName, _configuration);

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
                builder.AddRuntimeAssemblyGroup(new LibraryAssetGroup(new[] { compileAsset }));
                if (File.Exists(pdbPath))
                {
                    builder.AddRuntimeAsset(new LibraryAsset(Path.GetFileName(pdbPath), Path.GetFileName(pdbPath), pdbPath));
                }
            }
            else if (HasSourceFiles(project, compilerOptions))
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

                    builder.AddRuntimeAssemblyGroup(new LibraryAssetGroup(new[] { runtimeAssemblyAsset }));
                    builder.WithRuntimeAssets(CollectAssets(outputPaths.RuntimeFiles));
                }
                else
                {
                    builder.AddRuntimeAssemblyGroup(new LibraryAssetGroup(new[] { compilationAssemblyAsset }));
                    builder.WithRuntimeAssets(CollectAssets(outputPaths.CompilationFiles));
                }

                builder.WithResourceAssemblies(outputPaths.CompilationFiles.Resources().Select(r => new LibraryResourceAssembly(
                    LibraryAsset.CreateFromAbsolutePath(outputPaths.CompilationFiles.BasePath, r.Path),
                    r.Locale)));
            }

            builder.WithSourceReferences(project.Project.Files.SharedFiles.Select(f =>
                LibraryAsset.CreateFromAbsolutePath(project.Path, f)
            ));

            return builder.Build();
        }

        private bool HasSourceFiles(ProjectDescription project, CommonCompilerOptions compilerOptions)
        {
            if (compilerOptions.CompileInclude == null)
            {
                return project.Project.Files.SourceFiles.Any();
            }

            var includeFiles = IncludeFilesResolver.GetIncludeFiles(compilerOptions.CompileInclude, "/", diagnostics: null);

            return includeFiles.Any();
        }

        private IEnumerable<LibraryAsset> CollectAssets(CompilationOutputFiles files)
        {
            var assemblyPath = files.Assembly;
            foreach (var path in files.All().Except(files.Resources().Select(r => r.Path)))
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
                builder.WithCompilationAssemblies(new[]
                {
                    new LibraryAsset(library.Identity.Name, null, library.Path)
                });
            }
            return builder.Build();
        }

        private IEnumerable<LibraryAsset> GetSharedSources(PackageDescription package)
        {
            return package
                .PackageLibrary
                .Files
                .Select(f => PathUtility.GetPathWithDirectorySeparator(f))
                .Where(path => path.StartsWith("shared" + Path.DirectorySeparatorChar))
                .Select(path => LibraryAsset.CreateFromRelativePath(package.Path, path));
        }

        private IEnumerable<AnalyzerReference> GetAnalyzerReferences(PackageDescription package)
        {
            var analyzers = package
                .PackageLibrary
                .Files
                .Select(f => PathUtility.GetPathWithDirectorySeparator(f))
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

        private IEnumerable<LibraryResourceAssembly> PopulateResources(TargetLibraryWithAssets library, IEnumerable<LockFileItem> section)
        {
            foreach (var assemblyPath in section.Where(a => !PackageDependencyProvider.IsPlaceholderFile(a.Path)))
            {
                string locale;
                if(!assemblyPath.Properties.TryGetValue(Constants.LocaleLockFilePropertyName, out locale))
                {
                    locale = null;
                }
                yield return new LibraryResourceAssembly(
                    LibraryAsset.CreateFromRelativePath(library.Path, assemblyPath.Path),
                    locale);
            }
        }

        private IEnumerable<LibraryAsset> PopulateAssets(TargetLibraryWithAssets library, IEnumerable<LockFileItem> section)
        {
            foreach (var assemblyPath in section.Where(a => !PackageDependencyProvider.IsPlaceholderFile(a.Path)))
            {
                yield return LibraryAsset.CreateFromRelativePath(library.Path, assemblyPath.Path);
            }
        }

        private static bool LibraryIsOfType(LibraryType? type, LibraryDescription library)
        {
            return type == null || // No type filter was requested
                   library.Identity.Type.Equals(type);     // OR, library type matches requested type
        }
    }
}
