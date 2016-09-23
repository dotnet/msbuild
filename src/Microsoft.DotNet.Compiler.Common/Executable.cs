// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Files;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Compilation;
using Microsoft.DotNet.ProjectModel.Files;
using Microsoft.DotNet.Tools.Common;
using Microsoft.Extensions.DependencyModel;
using NuGet.Frameworks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.LibraryModel;

namespace Microsoft.DotNet.Cli.Compiler.Common
{
    public class Executable
    {
        private readonly ProjectContext _context;

        private readonly LibraryExporter _exporter;

        private readonly string _configuration;

        private readonly OutputPaths _outputPaths;

        private readonly string _runtimeOutputPath;

        private readonly string _intermediateOutputPath;

        private readonly CommonCompilerOptions _compilerOptions;

        public Executable(ProjectContext context, OutputPaths outputPaths, LibraryExporter exporter, string configuration)
            : this(context, outputPaths, outputPaths.RuntimeOutputPath, outputPaths.IntermediateOutputDirectoryPath, exporter, configuration) { }

        public Executable(ProjectContext context, OutputPaths outputPaths, string runtimeOutputPath, string intermediateOutputDirectoryPath, LibraryExporter exporter, string configuration)
        {
            _context = context;
            _outputPaths = outputPaths;
            _runtimeOutputPath = runtimeOutputPath;
            _intermediateOutputPath = intermediateOutputDirectoryPath;
            _exporter = exporter;
            _configuration = configuration;
            _compilerOptions = _context.ProjectFile.GetCompilerOptions(_context.TargetFramework, configuration);
        }

        public void MakeCompilationOutputRunnable()
        {
            CopyContentFiles();
            ExportRuntimeAssets();
        }

        private void VerifyCoreClrPresenceInPackageGraph()
        {
            var isCoreClrPresent = _exporter
                .GetAllExports()
                .SelectMany(e => e.NativeLibraryGroups)
                .SelectMany(g => g.Assets)
                .Select(a => a.FileName)
                .Where(f => Constants.LibCoreClrBinaryNames.Contains(f))
                .Any();

            // coreclr should be present for standalone apps
            if (!isCoreClrPresent)
            {
                throw new InvalidOperationException("Expected coreclr library not found in package graph. Please try running dotnet restore again.");
            }
        }

        private void ExportRuntimeAssets()
        {
            if (_context.TargetFramework.IsDesktop())
            {
                MakeCompilationOutputRunnableForFullFramework();
            }
            else
            {
                MakeCompilationOutputRunnableForCoreCLR();
            }
        }

        private void MakeCompilationOutputRunnableForFullFramework()
        {
            var dependencies = _exporter.GetDependencies();
            CopyAssemblies(dependencies);
            CopyAssets(dependencies);
            GenerateBindingRedirects(_exporter);
        }

        private void MakeCompilationOutputRunnableForCoreCLR()
        {
            WriteDepsFileAndCopyProjectDependencies(_exporter);

            var isRunnable = _compilerOptions.EmitEntryPoint ?? false;

            if (isRunnable && !_context.IsPortable)
            {
                // TODO: Pick a host based on the RID
                VerifyCoreClrPresenceInPackageGraph();
                CoreHost.CopyTo(_runtimeOutputPath, _compilerOptions.OutputName + Constants.ExeSuffix);
            }
        }

        private void CopyContentFiles()
        {
            var contentFiles = new ContentFiles(_context);

            if (_compilerOptions.CopyToOutputInclude != null)
            {
                var includeEntries = IncludeFilesResolver.GetIncludeFiles(
                    _compilerOptions.CopyToOutputInclude,
                    PathUtility.EnsureTrailingSlash(_runtimeOutputPath),
                    diagnostics: null);

                contentFiles.StructuredCopyTo(_runtimeOutputPath, includeEntries);
            }
            else
            {
                contentFiles.StructuredCopyTo(_runtimeOutputPath);
            }
        }

        private void CopyAssemblies(IEnumerable<LibraryExport> libraryExports)
        {
            foreach (var libraryExport in libraryExports)
            {
                libraryExport.RuntimeAssemblyGroups.GetDefaultAssets().CopyTo(_runtimeOutputPath);
                libraryExport.NativeLibraryGroups.GetDefaultAssets().CopyTo(_runtimeOutputPath);

                foreach (var group in libraryExport.ResourceAssemblies.GroupBy(r => r.Locale))
                {
                    var localeSpecificDir = Path.Combine(_runtimeOutputPath, group.Key);
                    if (!Directory.Exists(localeSpecificDir))
                    {
                        Directory.CreateDirectory(localeSpecificDir);
                    }
                    group.Select(r => r.Asset).CopyTo(localeSpecificDir);
                }
            }
        }

        private void CopyAssets(IEnumerable<LibraryExport> libraryExports)
        {
            foreach (var libraryExport in libraryExports)
            {
                libraryExport.RuntimeAssets.StructuredCopyTo(
                    _runtimeOutputPath,
                    _intermediateOutputPath);
            }
        }

        private void WriteDepsFileAndCopyProjectDependencies(LibraryExporter exporter)
        {
            var exports = exporter.GetAllExports().ToList();
            var exportsLookup = exports.ToDictionary(e => e.Library.Identity.Name, StringComparer.OrdinalIgnoreCase);
            var platformExclusionList = _context.GetPlatformExclusionList(exportsLookup);
            var filteredExports = exports.FilterExports(platformExclusionList);

            WriteConfigurationFiles(exports, filteredExports, exports, includeDevConfig: true);

            var projectExports = exporter.GetAllProjectTypeDependencies();
            CopyAssemblies(projectExports);
            CopyAssets(projectExports);

            var packageExports = exporter.GetDependencies(LibraryType.Package);
            CopyAssets(packageExports);
        }

        public void WriteConfigurationFiles(
            IEnumerable<LibraryExport> allExports,
            IEnumerable<LibraryExport> depsRuntimeExports,
            IEnumerable<LibraryExport> depsCompilationExports,
            bool includeDevConfig)
        {
            WriteDeps(depsRuntimeExports, depsCompilationExports);
            if (_context.ProjectFile.HasRuntimeOutput(_configuration))
            {
                WriteRuntimeConfig(allExports);
                if (includeDevConfig)
                {
                    WriteDevRuntimeConfig();
                }
            }
        }

        private void WriteRuntimeConfig(IEnumerable<LibraryExport> allExports)
        {
            if (!_context.TargetFramework.IsDesktop())
            {
                // TODO: Suppress this file if there's nothing to write? RuntimeOutputFiles would have to be updated
                // in order to prevent breaking incremental compilation...

                var json = new JObject();
                var runtimeOptions = new JObject();
                json.Add("runtimeOptions", runtimeOptions);

                WriteFramework(runtimeOptions, allExports);
                WriteRuntimeOptions(runtimeOptions);

                var runtimeConfigJsonFile =
                    Path.Combine(_runtimeOutputPath, _compilerOptions.OutputName + FileNameSuffixes.RuntimeConfigJson);

                using (var writer = new JsonTextWriter(new StreamWriter(File.Create(runtimeConfigJsonFile))))
                {
                    writer.Formatting = Formatting.Indented;
                    json.WriteTo(writer);
                }
            }
        }

        private void WriteFramework(JObject runtimeOptions, IEnumerable<LibraryExport> allExports)
        {
            var redistPackage = _context.PlatformLibrary;
            if (redistPackage != null)
            {
                var packageName = redistPackage.Identity.Name;

                var redistExport = allExports.FirstOrDefault(e => e.Library.Identity.Name.Equals(packageName));
                if (redistExport == null)
                {
                    throw new InvalidOperationException($"Platform package '{packageName}' was not present in the graph.");
                }
                else
                {
                    var framework = new JObject(
                        new JProperty("name", redistExport.Library.Identity.Name),
                        new JProperty("version", redistExport.Library.Identity.Version.ToNormalizedString()));
                    runtimeOptions.Add("framework", framework);
                }
            }
        }

        private void WriteRuntimeOptions(JObject runtimeOptions)
        {
            if (string.IsNullOrEmpty(_context.ProjectFile.RawRuntimeOptions))
            {
                return;
            }

            var runtimeOptionsFromProjectJson = JObject.Parse(_context.ProjectFile.RawRuntimeOptions);
            foreach (var runtimeOption in runtimeOptionsFromProjectJson)
            {
                runtimeOptions.Add(runtimeOption.Key, runtimeOption.Value);
            }
        }

        private void WriteDevRuntimeConfig()
        {
            if (_context.TargetFramework.IsDesktop())
            {
                return;
            }

            var json = new JObject();
            var runtimeOptions = new JObject();
            json.Add("runtimeOptions", runtimeOptions);

            AddAdditionalProbingPaths(runtimeOptions);

            var runtimeConfigDevJsonFile =
                    Path.Combine(_runtimeOutputPath, _compilerOptions.OutputName + FileNameSuffixes.RuntimeConfigDevJson);

            using (var writer = new JsonTextWriter(new StreamWriter(File.Create(runtimeConfigDevJsonFile))))
            {
                writer.Formatting = Formatting.Indented;
                json.WriteTo(writer);
            }
        }

        private void AddAdditionalProbingPaths(JObject runtimeOptions)
        {
            if (_context.LockFile != null)
            {
                var additionalProbingPaths = new JArray();
                foreach (var packageFolder in _context.LockFile.PackageFolders)
                {
                    // DotNetHost doesn't handle additional probing paths with a trailing slash
                    additionalProbingPaths.Add(PathUtility.EnsureNoTrailingDirectorySeparator(packageFolder.Path));
                }

                runtimeOptions.Add("additionalProbingPaths", additionalProbingPaths);
            }
        }

        private void WriteDeps(IEnumerable<LibraryExport> runtimeExports, IEnumerable<LibraryExport> compilationExports)
        {
            Directory.CreateDirectory(_runtimeOutputPath);

            var includeCompile = _compilerOptions.PreserveCompilationContext == true;

            var dependencyContext = new DependencyContextBuilder().Build(
                compilerOptions: includeCompile ? _compilerOptions : null,
                compilationExports: includeCompile ? compilationExports : null,
                runtimeExports: runtimeExports,
                portable: _context.IsPortable,
                target: _context.TargetFramework,
                runtime: _context.RuntimeIdentifier ?? string.Empty);

            var writer = new DependencyContextWriter();
            var depsJsonFilePath = Path.Combine(_runtimeOutputPath, _compilerOptions.OutputName + FileNameSuffixes.DepsJson);
            using (var fileStream = File.Create(depsJsonFilePath))
            {
                writer.Write(dependencyContext, fileStream);
            }
        }

        private void GenerateBindingRedirects(LibraryExporter exporter)
        {
            var outputName = _outputPaths.RuntimeFiles.Assembly;
            var configFile = outputName + Constants.ConfigSuffix;

            var existingConfig = new DirectoryInfo(_context.ProjectDirectory)
                .EnumerateFiles()
                .FirstOrDefault(f => f.Name.Equals("app.config", StringComparison.OrdinalIgnoreCase));

            if (existingConfig != null)
            {
                File.Copy(existingConfig.FullName, configFile, true);
            }

            List<string> configFiles = new List<string>();
            configFiles.Add(configFile);

            foreach (var export in exporter.GetDependencies())
            {
                var dependencyExecutables = export.RuntimeAssemblyGroups.GetDefaultAssets()
                                                .Where(asset => asset.FileName.ToLower().EndsWith(FileNameSuffixes.DotNet.Exe))
                                                .Select(asset => Path.Combine(_runtimeOutputPath, asset.FileName));

                foreach (var executable in dependencyExecutables)
                {
                    configFile = executable + Constants.ConfigSuffix;
                    configFiles.Add(configFile);
                }
            }

            exporter.GetAllExports().GenerateBindingRedirects(configFiles);
        }
    }
}
