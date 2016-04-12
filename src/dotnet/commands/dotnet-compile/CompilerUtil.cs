// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Cli.Compiler.Common;
using Microsoft.DotNet.ProjectModel.Compilation;
using Microsoft.DotNet.ProjectModel.Files;
using Microsoft.DotNet.ProjectModel.Resources;
using Microsoft.DotNet.Tools.Common;

// This class is responsible with defining the arguments for the Compile verb.
// It knows how to interpret them and set default values

namespace Microsoft.DotNet.Tools.Compiler
{
    public static class CompilerUtil
    {
        public static string ResolveLanguageId(ProjectContext context)
        {
            var languageId = context.ProjectFile.AnalyzerOptions?.LanguageId;
            if (languageId == null)
            {
                languageId = context.ProjectFile.GetSourceCodeLanguage();
            }

            return languageId;
        }

        public struct NonCultureResgenIO
        {
            public readonly string InputFile;
            public readonly string MetadataName;

            // is non-null only when resgen needs to be invoked (inputFile is .resx)
            public readonly string OutputFile;

            public NonCultureResgenIO(string inputFile, string outputFile, string metadataName)
            {
                InputFile = inputFile;
                OutputFile = outputFile;
                MetadataName = metadataName;
            }
        }

        // used in incremental compilation
        public static List<NonCultureResgenIO> GetNonCultureResources(Project project, string intermediateOutputPath)
        {
            return
                (from resourceFile in project.Files.ResourceFiles
                 let inputFile = resourceFile.Key
                 where string.IsNullOrEmpty(ResourceUtility.GetResourceCultureName(inputFile))
                 let metadataName = GetResourceFileMetadataName(project, resourceFile.Key, resourceFile.Value)
                 let outputFile = ResourceUtility.IsResxFile(inputFile) ? Path.Combine(intermediateOutputPath, metadataName) : null
                 select new NonCultureResgenIO(inputFile, outputFile, metadataName)
                    ).ToList();
        }

        // used in incremental compilation
        public static List<NonCultureResgenIO> GetNonCultureResourcesFromIncludeEntries(
            Project project,
            string intermediateOutputPath,
            CommonCompilerOptions compilationOptions)
        {
            var includeFiles = IncludeFilesResolver.GetIncludeFiles(compilationOptions.EmbedInclude, "/", diagnostics: null);
            return
                (from resourceFile in includeFiles
                    let inputFile = resourceFile.SourcePath
                    where string.IsNullOrEmpty(ResourceUtility.GetResourceCultureName(inputFile))
                    let target = resourceFile.IsCustomTarget ? resourceFile.TargetPath : null
                    let metadataName = GetResourceFileMetadataName(project, resourceFile.SourcePath, target)
                    let outputFile = ResourceUtility.IsResxFile(inputFile) ? Path.Combine(intermediateOutputPath, metadataName) : null
                    select new NonCultureResgenIO(inputFile, outputFile, metadataName)
                    ).ToList();
        }

        public struct CultureResgenIO
        {
            public readonly string Culture;
            public readonly Dictionary<string, string> InputFileToMetadata;
            public readonly string OutputFile;

            public CultureResgenIO(string culture, Dictionary<string, string> inputFileToMetadata, string outputFile)
            {
                Culture = culture;
                InputFileToMetadata = inputFileToMetadata;
                OutputFile = outputFile;
            }
        }

        // used in incremental compilation
        public static List<CultureResgenIO> GetCultureResources(Project project, string outputPath)
        {
            return
                (from resourceFileGroup in project.Files.ResourceFiles.GroupBy(resourceFile => ResourceUtility.GetResourceCultureName(resourceFile.Key))
                 let culture = resourceFileGroup.Key
                 where !string.IsNullOrEmpty(culture)
                 let inputFileToMetadata = resourceFileGroup.ToDictionary(r => r.Key, r => GetResourceFileMetadataName(project, r.Key, r.Value))
                 let resourceOutputPath = Path.Combine(outputPath, culture)
                 let outputFile = Path.Combine(resourceOutputPath, project.Name + ".resources.dll")
                 select new CultureResgenIO(culture, inputFileToMetadata, outputFile)
                    ).ToList();
        }

        // used in incremental compilation
        public static List<CultureResgenIO> GetCultureResourcesFromIncludeEntries(
            Project project,
            string outputPath,
            CommonCompilerOptions compilationOptions)
        {
            var includeFiles = IncludeFilesResolver.GetIncludeFiles(compilationOptions.EmbedInclude, "/", diagnostics: null);
            return
                (from resourceFileGroup in includeFiles
                 .GroupBy(resourceFile => ResourceUtility.GetResourceCultureName(resourceFile.SourcePath))
                    let culture = resourceFileGroup.Key
                    where !string.IsNullOrEmpty(culture)
                    let inputFileToMetadata = resourceFileGroup.ToDictionary(
                        r => r.SourcePath, r => GetResourceFileMetadataName(project, r.SourcePath, r.IsCustomTarget ? r.TargetPath : null))
                    let resourceOutputPath = Path.Combine(outputPath, culture)
                    let outputFile = Path.Combine(resourceOutputPath, project.Name + ".resources.dll")
                    select new CultureResgenIO(culture, inputFileToMetadata, outputFile)
                    ).ToList();
        }

        // used in incremental compilation
        public static IList<string> GetReferencePathsForCultureResgen(List<LibraryExport> dependencies)
        {
            return dependencies.SelectMany(libraryExport => libraryExport.CompilationAssemblies).Select(r => r.ResolvedPath).ToList();
        }

        public static string GetResourceFileMetadataName(Project project, string resourceFileSource, string resourceFileTarget)
        {
            string resourceName = null;
            string rootNamespace = null;

            string root = PathUtility.EnsureTrailingSlash(project.ProjectDirectory);
            string resourcePath = resourceFileSource;
            if (string.IsNullOrEmpty(resourceFileTarget))
            {
                //  No logical name, so use the file name
                resourceName = ResourceUtility.GetResourceName(root, resourcePath);
                rootNamespace = project.Name;
            }
            else
            {
                resourceName = ResourceManifestName.EnsureResourceExtension(resourceFileTarget, resourcePath);
                rootNamespace = null;
            }

            var name = ResourceManifestName.CreateManifestName(resourceName, rootNamespace);
            return name;
        }

        // used in incremental compilation
        public static IEnumerable<string> GetCompilationSources(ProjectContext project, CommonCompilerOptions compilerOptions)
        {
            if (compilerOptions.CompileInclude == null)
            {
                return project.ProjectFile.Files.SourceFiles;
            }

            var includeFiles = IncludeFilesResolver.GetIncludeFiles(compilerOptions.CompileInclude, "/", diagnostics: null);

            return includeFiles.Select(f => f.SourcePath);
        }

        //used in incremental precondition checks
        public static IEnumerable<string> GetCommandsInvokedByCompile(ProjectContext project)
        {
            var compilerOptions = project.ProjectFile.GetCompilerOptions(project.TargetFramework, configurationName: null);
            return new List<string> { compilerOptions.CompilerName, "compile" };
        }
    }
}