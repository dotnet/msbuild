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
using NuGet.Frameworks;

namespace Microsoft.Dotnet.Cli.Compiler.Common
{
    public class Executable
    {
        private readonly ProjectContext _context;

        private readonly LibraryExporter _exporter;

        private readonly OutputPaths _outputPaths;

        private readonly string _runtimeOutputPath;

        private readonly string _intermediateOutputPath;

        public Executable(ProjectContext context, OutputPaths outputPaths, LibraryExporter exporter)
        {
            _context = context;
            _outputPaths = outputPaths;
            _runtimeOutputPath = outputPaths.RuntimeOutputPath;
            _intermediateOutputPath = outputPaths.IntermediateOutputDirectoryPath;
            _exporter = exporter;
        }

        public void MakeCompilationOutputRunnable()
        {
            if (string.IsNullOrEmpty(_context.RuntimeIdentifier))
            {
                throw new InvalidOperationException($"Can not make output runnable for framework {_context.TargetFramework}, because it doesn't have runtime target");
            }

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

            // TODO: Pick a host based on the RID
            CoreHost.CopyTo(_runtimeOutputPath, _context.ProjectFile.Name + Constants.ExeSuffix);
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
            exporter
                .GetDependencies(LibraryType.Package)
                .WriteDepsTo(Path.Combine(_runtimeOutputPath, _context.ProjectFile.Name + FileNameSuffixes.Deps));

            var projectExports = exporter.GetDependencies(LibraryType.Project);
            CopyAssemblies(projectExports);
            CopyAssets(projectExports);

            var packageExports = exporter.GetDependencies(LibraryType.Package);
            CopyAssets(packageExports);
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
