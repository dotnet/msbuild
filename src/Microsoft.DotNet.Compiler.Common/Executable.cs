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

namespace Microsoft.Dotnet.Cli.Compiler.Common
{
    public class Executable
    {
        // GROOOOOSS
        private static readonly string RedistPackageName = "Microsoft.NETCore.App";

        private readonly ProjectContext _context;

        private readonly LibraryExporter _exporter;

        private readonly string _configuration;

        private readonly OutputPaths _outputPaths;

        private readonly string _runtimeOutputPath;

        private readonly string _intermediateOutputPath;

        public Executable(ProjectContext context, OutputPaths outputPaths, LibraryExporter exporter, string configuration)
        {
            _context = context;
            _outputPaths = outputPaths;
            _runtimeOutputPath = outputPaths.RuntimeOutputPath;
            _intermediateOutputPath = outputPaths.IntermediateOutputDirectoryPath;
            _exporter = exporter;
            _configuration = configuration;
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

            var emitEntryPoint = _context.ProjectFile.GetCompilerOptions(_context.TargetFramework, _configuration).EmitEntryPoint ?? false;
            if (emitEntryPoint && !string.IsNullOrEmpty(_context.RuntimeIdentifier))
            {
                // TODO: Pick a host based on the RID
                CoreHost.CopyTo(_runtimeOutputPath, _context.ProjectFile.Name + Constants.ExeSuffix);
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
                libraryExport.RuntimeAssemblies.CopyTo(_runtimeOutputPath);
                libraryExport.NativeLibraries.CopyTo(_runtimeOutputPath);
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
            WriteRuntimeConfig(exporter);

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

                var redistExport = exporter
                    .GetAllExports()
                    .FirstOrDefault(l => l.Library.Identity.Name.Equals(RedistPackageName, StringComparison.OrdinalIgnoreCase));
                if (redistExport != null)
                {
                    var framework = new JObject(
                        new JProperty("name", redistExport.Library.Identity.Name),
                        new JProperty("version", redistExport.Library.Identity.Version.ToNormalizedString()));
                    runtimeOptions.Add("framework", framework);
                }

                var runtimeConfigJsonFile = Path.Combine(_runtimeOutputPath, _context.ProjectFile.Name + FileNameSuffixes.RuntimeConfigJson);
                using (var writer = new JsonTextWriter(new StreamWriter(File.Create(runtimeConfigJsonFile))))
                {
                    writer.Formatting = Formatting.Indented;
                    json.WriteTo(writer);
                }
            }
        }

        public void WriteDeps(LibraryExporter exporter)
        {
            var path = Path.Combine(_runtimeOutputPath, _context.ProjectFile.Name + FileNameSuffixes.Deps);
            CreateDirectoryIfNotExists(path);
            File.WriteAllLines(path, exporter
                .GetDependencies(LibraryType.Package)
                .SelectMany(GenerateLines));

            var compilerOptions = _context.ResolveCompilationOptions(_configuration);
            var includeCompile = compilerOptions.PreserveCompilationContext == true;

            var exports = exporter.GetAllExports().ToArray();
            var dependencyContext = new DependencyContextBuilder().Build(
                compilerOptions: includeCompile ? compilerOptions : null,
                compilationExports: includeCompile ? exports : null,
                runtimeExports: exports,
                portable: string.IsNullOrEmpty(_context.RuntimeIdentifier),
                target: _context.TargetFramework,
                runtime: _context.RuntimeIdentifier ?? string.Empty);

            var writer = new DependencyContextWriter();
            var depsJsonFile = Path.Combine(_runtimeOutputPath, _context.ProjectFile.Name + FileNameSuffixes.DepsJson);
            using (var fileStream = File.Create(depsJsonFile))
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


        private static void CreateDirectoryIfNotExists(string path)
        {
            var depsFile = new FileInfo(path);
            depsFile.Directory.Create();
        }

        private static IEnumerable<string> GenerateLines(LibraryExport export)
        {
            return GenerateLines(export, export.RuntimeAssemblies, "runtime")
                .Union(GenerateLines(export, export.NativeLibraries, "native"));
        }

        private static IEnumerable<string> GenerateLines(LibraryExport export, IEnumerable<LibraryAsset> items, string type)
        {
            return items.Select(i => DepsFormatter.EscapeRow(new[]
            {
                export.Library.Identity.Type.Value,
                export.Library.Identity.Name,
                export.Library.Identity.Version.ToNormalizedString(),
                export.Library.Hash,
                type,
                i.Name,
                i.RelativePath
            }));
        }
    }
}
