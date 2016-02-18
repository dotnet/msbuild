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

        private readonly OutputPaths _outputPaths;

        private readonly LibraryExporter _exporter;

        public Executable(ProjectContext context, OutputPaths outputPaths, LibraryExporter exporter)
        {
            _context = context;
            _outputPaths = outputPaths;
            _exporter = exporter;
        }

        public void MakeCompilationOutputRunnable()
        {
            if (string.IsNullOrEmpty(_context.RuntimeIdentifier))
            {
                throw new InvalidOperationException($"Can not make output runnable for framework {_context.TargetFramework}, because it doesn't have runtime target");
            }

            var outputPath = _outputPaths.RuntimeOutputPath;

            CopyContentFiles(outputPath);

            ExportRuntimeAssets(outputPath);
        }

        private void ExportRuntimeAssets(string outputPath)
        {
            if (_context.TargetFramework.IsDesktop())
            {
                MakeCompilationOutputRunnableForFullFramework(outputPath);
            }
            else
            {
                MakeCompilationOutputRunnableForCoreCLR(outputPath);
            }
        }

        private void MakeCompilationOutputRunnableForFullFramework(
            string outputPath)
        {
            CopyAllDependencies(outputPath, _exporter.GetDependencies());
            GenerateBindingRedirects(_exporter);
        }

        private void MakeCompilationOutputRunnableForCoreCLR(string outputPath)
        {
            WriteDepsFileAndCopyProjectDependencies(_exporter, _context.ProjectFile.Name, outputPath);

            // TODO: Pick a host based on the RID
            CoreHost.CopyTo(outputPath, _context.ProjectFile.Name + Constants.ExeSuffix);
        }

        private void CopyContentFiles(string outputPath)
        {
            var contentFiles = new ContentFiles(_context);
            contentFiles.StructuredCopyTo(outputPath);
        }

        private static void CopyAllDependencies(string outputPath, IEnumerable<LibraryExport> libraryExports)
        {
            foreach (var libraryExport in libraryExports)
            {
                libraryExport.RuntimeAssemblies.CopyTo(outputPath);
                libraryExport.NativeLibraries.CopyTo(outputPath);
                libraryExport.RuntimeAssets.StructuredCopyTo(outputPath);
            }
        }

        private static void WriteDepsFileAndCopyProjectDependencies(
            LibraryExporter exporter,
            string projectFileName,
            string outputPath)
        {
            exporter
                .GetDependencies(LibraryType.Package)
                .WriteDepsTo(Path.Combine(outputPath, projectFileName + FileNameSuffixes.Deps));

            var projectExports = exporter.GetDependencies(LibraryType.Project);

            CopyAllDependencies(outputPath, projectExports);
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
