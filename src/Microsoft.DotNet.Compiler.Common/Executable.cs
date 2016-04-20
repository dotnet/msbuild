// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.DotNet.Cli.Compiler.Common;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Files;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Compilation;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.Extensions.DependencyModel;
using NuGet.Frameworks;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Reflection.PortableExecutable;

namespace Microsoft.Dotnet.Cli.Compiler.Common
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
        {
            _context = context;
            _outputPaths = outputPaths;
            _runtimeOutputPath = outputPaths.RuntimeOutputPath;
            _intermediateOutputPath = outputPaths.IntermediateOutputDirectoryPath;
            _exporter = exporter;
            _configuration = configuration;
            _compilerOptions = _context.ProjectFile.GetCompilerOptions(_context.TargetFramework, configuration);
        }

        public void MakeCompilationOutputRunnable()
        {
            CopyContentFiles();
            ExportRuntimeAssets();
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

            var emitEntryPoint = _compilerOptions.EmitEntryPoint ?? false;
            if (emitEntryPoint && !string.IsNullOrEmpty(_context.RuntimeIdentifier))
            {
                // TODO: Pick a host based on the RID
                CoreHost.CopyTo(_runtimeOutputPath, _compilerOptions.OutputName + Constants.ExeSuffix);
            }
        }

        private void CopyContentFiles()
        {
            var contentFiles = new ContentFiles(_context);
            contentFiles.StructuredCopyTo(_runtimeOutputPath);
        }

        private void CopyAssemblies(IEnumerable<LibraryExport> libraryExports)
        {
            foreach (var libraryExport in libraryExports)
            {
                libraryExport.RuntimeAssemblyGroups.GetDefaultAssets().CopyTo(_runtimeOutputPath);
                libraryExport.NativeLibraryGroups.GetDefaultAssets().CopyTo(_runtimeOutputPath);

                foreach(var group in libraryExport.ResourceAssemblies.GroupBy(r => r.Locale))
                {
                    var localeSpecificDir = Path.Combine(_runtimeOutputPath, group.Key);
                    if(!Directory.Exists(localeSpecificDir))
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
            WriteDeps(exporter);
            if (_context.ProjectFile.HasRuntimeOutput(_configuration))
            {
                WriteRuntimeConfig(exporter);
                WriteDevRuntimeConfig(exporter);
            }

            var projectExports = exporter.GetDependencies(LibraryType.Project);
            CopyAssemblies(projectExports);
            CopyAssets(projectExports);

            var packageExports = exporter.GetDependencies(LibraryType.Package);
            CopyAssets(packageExports);
        }

        private void WriteRuntimeConfig(LibraryExporter exporter)
        {
            if (!_context.TargetFramework.IsDesktop())
            {
                // TODO: Suppress this file if there's nothing to write? RuntimeOutputFiles would have to be updated
                // in order to prevent breaking incremental compilation...

                var json = new JObject();
                var runtimeOptions = new JObject();
                json.Add("runtimeOptions", runtimeOptions);

                WriteFramework(runtimeOptions, exporter);
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

        private void WriteFramework(JObject runtimeOptions, LibraryExporter exporter)
        {
            var redistPackage = _context.PlatformLibrary;
            if (redistPackage != null)
            {
                var packageName = redistPackage.Identity.Name;

                var redistExport = exporter.GetAllExports()
                    .FirstOrDefault(e => e.Library.Identity.Name.Equals(packageName));
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

        private void WriteDevRuntimeConfig(LibraryExporter exporter)
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
            var additionalProbingPaths = new JArray(_context.PackagesDirectory);
            runtimeOptions.Add("additionalProbingPaths", additionalProbingPaths);
        }

        public void WriteDeps(LibraryExporter exporter)
        {
            Directory.CreateDirectory(_runtimeOutputPath);

            var includeCompile = _compilerOptions.PreserveCompilationContext == true;

            var exports = exporter.GetAllExports().ToArray();
            var dependencyContext = new DependencyContextBuilder().Build(
                compilerOptions: includeCompile ? _compilerOptions : null,
                compilationExports: includeCompile ? exports : null,
                runtimeExports: exports,
                portable: string.IsNullOrEmpty(_context.RuntimeIdentifier),
                target: _context.TargetFramework,
                runtime: _context.RuntimeIdentifier ?? string.Empty);

            var writer = new DependencyContextWriter();
            var depsJsonFilePath = Path.Combine(_runtimeOutputPath, _compilerOptions.OutputName + FileNameSuffixes.DepsJson);
            using (var fileStream = File.Create(depsJsonFilePath))
            {
                writer.Write(dependencyContext, fileStream);
            }
        }

        public void GenerateBindingRedirects(LibraryExporter exporter)
        {
            var outputName = _outputPaths.RuntimeFiles.Assembly;

            var existingConfig = new DirectoryInfo(_context.ProjectDirectory)
                .EnumerateFiles()
                .FirstOrDefault(f => f.Name.Equals("app.config", StringComparison.OrdinalIgnoreCase));

            XDocument baseAppConfig = null;

            if (existingConfig != null)
            {
                using (var fileStream = File.OpenRead(existingConfig.FullName))
                {
                    baseAppConfig = XDocument.Load(fileStream);
                }
            }

            var appConfig = exporter.GetAllExports().GenerateBindingRedirects(baseAppConfig);

            if (appConfig == null) { return; }

            var path = outputName + ".config";
            using (var stream = File.Create(path))
            {
                appConfig.Save(stream);
            }
        }
    }
}
