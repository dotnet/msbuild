// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.DotNet.Cli.Compiler.Common;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Compilation;
using Microsoft.DotNet.ProjectModel.Graph;
using NuGet.Frameworks;

namespace Microsoft.Dotnet.Cli.Compiler.Common
{
    public class Executable
    {
        private readonly ProjectContext _context;

        private readonly OutputPathCalculator _calculator;

        public Executable(ProjectContext context, OutputPathCalculator calculator)
        {
            _context = context;

            _calculator = calculator;
        }

        public void MakeCompilationOutputRunnable(string configuration)
        {
            var outputPath = _calculator.GetOutputDirectoryPath(configuration);

            CopyContentFiles(outputPath);

            ExportRuntimeAssets(outputPath, configuration);
        }

        private void ExportRuntimeAssets(string outputPath, string configuration)
        {
            var exporter = _context.CreateExporter(configuration);

            if (_context.TargetFramework.IsDesktop())
            {
                MakeCompilationOutputRunnableForFullFramework(outputPath, configuration, exporter);
            }
            else
            {
                MakeCompilationOutputRunnableForCoreCLR(outputPath, exporter);
            }
        }        

        private void MakeCompilationOutputRunnableForFullFramework(
            string outputPath,
            string configuration,
            LibraryExporter exporter)
        {
            CopyAllDependencies(outputPath, exporter);

            GenerateBindingRedirects(exporter, configuration);
        }        

        private void MakeCompilationOutputRunnableForCoreCLR(string outputPath, LibraryExporter exporter)
        {
            WriteDepsFileAndCopyProjectDependencies(exporter, _context.ProjectFile.Name, outputPath);

            // TODO: Pick a host based on the RID
            CoreHost.CopyTo(outputPath, _context.ProjectFile.Name + Constants.ExeSuffix);
        }

        private void CopyContentFiles(string outputPath)
        {
            var contentFiles = new ContentFiles(_context);
            contentFiles.StructuredCopyTo(outputPath);
        }

        private static void CopyAllDependencies(string outputPath, LibraryExporter exporter)
        {
            exporter
                .GetDependencies()
                .SelectMany(e => e.RuntimeAssets())
                .CopyTo(outputPath);
        }

        private static void WriteDepsFileAndCopyProjectDependencies(
            LibraryExporter exporter,
            string projectFileName,
            string outputPath)
        {
            exporter
                .GetDependencies(LibraryType.Package)
                .WriteDepsTo(Path.Combine(outputPath, projectFileName + FileNameSuffixes.Deps));

            exporter
                .GetDependencies(LibraryType.Project)
                .SelectMany(e => e.RuntimeAssets())
                .CopyTo(outputPath);
        }                        

        public void GenerateBindingRedirects(LibraryExporter exporter, string configuration)
        {
            var outputName = _calculator.GetAssemblyPath(configuration);

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
