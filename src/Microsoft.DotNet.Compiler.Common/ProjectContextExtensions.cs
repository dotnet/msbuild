// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Compilation;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.DotNet.Tools.Common;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli.Compiler.Common
{
    public static class ProjectContextExtensions
    {
        public static string ProjectName(this ProjectContext context) => context.RootProject.Identity.Name;
        public static string GetDisplayName(this ProjectContext context) => $"{context.RootProject.Identity.Name} ({context.TargetFramework})";

        public static void MakeCompilationOutputRunnable(this ProjectContext context, string outputPath, string configuration)
        {
            // REVIEW: This shouldn't be copied on compile
            context
                .ProjectFile
                .Files
                .GetContentFiles()
                .StructuredCopyTo(context.ProjectDirectory, outputPath)
                .RemoveAttribute(FileAttributes.ReadOnly);

            var exporter = context.CreateExporter(configuration);

            if (context.TargetFramework.IsDesktop())
            {
                // On full framework, copy all dependencies to the output path
                exporter
                    .GetDependencies()
                    .SelectMany(e => e.RuntimeAssets())
                    .CopyTo(outputPath);
                    
                // Generate binding redirects
                var outputName = context.GetOutputPathCalculator(outputPath).GetAssemblyPath(configuration);
                context.GenerateBindingRedirects(exporter, outputName);
            }
            else
            {
                exporter
                    .GetDependencies(LibraryType.Package)
                    .WriteDepsTo(Path.Combine(outputPath, context.ProjectFile.Name + FileNameSuffixes.Deps));
                
                // On core clr, only copy project references
                exporter.GetDependencies(LibraryType.Project)
                    .SelectMany(e => e.RuntimeAssets())
                    .CopyTo(outputPath);

                // TODO: Pick a host based on the RID
                CoreHost.CopyTo(outputPath, context.ProjectFile.Name + Constants.ExeSuffix);
            }
        }

        private static IEnumerable<string> StructuredCopyTo(this IEnumerable<string> sourceFiles, string sourceDirectory, string targetDirectory)
        {
            if (sourceFiles == null)
            {
                throw new ArgumentNullException(nameof(sourceFiles));
            }

            sourceDirectory = EnsureTrailingSlash(sourceDirectory);
            targetDirectory = EnsureTrailingSlash(targetDirectory);

            var pathMap = sourceFiles
                .ToDictionary(s => s,
                    s => Path.Combine(targetDirectory,
                        PathUtility.GetRelativePath(sourceDirectory, s)));

            foreach (var targetDir in pathMap.Values
                .Select(Path.GetDirectoryName)
                .Distinct()
                .Where(t => !Directory.Exists(t)))
            {
                Directory.CreateDirectory(targetDir);
            }

            foreach (var sourceFilePath in pathMap.Keys)
            {
                File.Copy(
                    sourceFilePath,
                    pathMap[sourceFilePath],
                    overwrite: true);
            }

            return pathMap.Values;
        }

        private static string EnsureTrailingSlash(string path)
        {
            return EnsureTrailingCharacter(path, Path.DirectorySeparatorChar);
        }

        private static string EnsureTrailingCharacter(string path, char trailingCharacter)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            // if the path is empty, we want to return the original string instead of a single trailing character.
            if (path.Length == 0 || path[path.Length - 1] == trailingCharacter)
            {
                return path;
            }

            return path + trailingCharacter;
        }

        private static IEnumerable<string> RemoveAttribute(this IEnumerable<string> files, FileAttributes attribute)
        {
            foreach (var file in files)
            {
                var fileAttributes = File.GetAttributes(file);
                if ((fileAttributes & attribute) == attribute)
                {
                    File.SetAttributes(file, fileAttributes & ~attribute);
                }
            }

            return files;
        }

        public static void GenerateBindingRedirects(this ProjectContext context, LibraryExporter exporter, string outputName)
        {
            var existingConfig = new DirectoryInfo(context.ProjectDirectory)
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

        public static CommonCompilerOptions GetLanguageSpecificCompilerOptions(this ProjectContext context, NuGetFramework framework, string configurationName)
        {
            var baseOption = context.ProjectFile.GetCompilerOptions(framework, configurationName);

            IReadOnlyList<string> defaultSuppresses;
            var compilerName = context.ProjectFile.CompilerName ?? "csc";
            if (DefaultCompilerWarningSuppresses.Suppresses.TryGetValue(compilerName, out defaultSuppresses))
            {
                baseOption.SuppressWarnings = (baseOption.SuppressWarnings ?? Enumerable.Empty<string>()).Concat(defaultSuppresses).Distinct();
            }

            return baseOption;
        }
    }
}
