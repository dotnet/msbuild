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

        public static string GetOutputPath(this ProjectContext context, string configuration, string currentOutputPath)
        {
            var outputPath = string.Empty;

            if (string.IsNullOrEmpty(currentOutputPath))
            {
                outputPath = Path.Combine(
                    GetDefaultRootOutputPath(context, currentOutputPath),
                    Constants.BinDirectoryName,
                    configuration,
                    context.TargetFramework.GetTwoDigitShortFolderName());
            }
            else
            {
                outputPath = currentOutputPath;
            }

            return outputPath;
        }

        public static string GetIntermediateOutputPath(this ProjectContext context, string configuration, string intermediateOutputValue, string currentOutputPath)
        {
            var intermediateOutputPath = string.Empty;

            if (string.IsNullOrEmpty(intermediateOutputValue))
            {
                intermediateOutputPath = Path.Combine(
                    GetDefaultRootOutputPath(context, currentOutputPath),
                    Constants.ObjDirectoryName,
                    configuration,
                    context.TargetFramework.GetTwoDigitShortFolderName());
            }
            else
            {
                intermediateOutputPath = intermediateOutputValue;
            }

            return intermediateOutputPath;
        }

        public static string GetDefaultRootOutputPath(ProjectContext context, string currentOutputPath)
        {
            var rootOutputPath = string.Empty;

            if (string.IsNullOrEmpty(currentOutputPath))
            {
                rootOutputPath = context.ProjectFile.ProjectDirectory;
            }

            return rootOutputPath;
        }

        public static void MakeCompilationOutputRunnable(this ProjectContext context, string outputPath, string configuration)
        {
            context
                .ProjectFile
                .Files
                .GetContentFiles()
                .StructuredCopyTo(context.ProjectDirectory, outputPath)
                .RemoveAttribute(FileAttributes.ReadOnly);

            var exporter = context.CreateExporter(configuration);

            if (context.TargetFramework.IsDesktop())
            {
                exporter
                    .GetDependencies()
                    .SelectMany(e => e.RuntimeAssets())
                    .CopyTo(outputPath);
            }
            else
            {
                exporter
                    .GetDependencies(LibraryType.Package)
                    .WriteDepsTo(Path.Combine(outputPath, context.ProjectFile.Name + FileNameSuffixes.Deps));

                exporter.GetDependencies(LibraryType.Project)
                    .SelectMany(e => e.RuntimeAssets())
                    .CopyTo(outputPath);

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
