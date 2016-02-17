// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Cli.Compiler.Common;
using Microsoft.DotNet.ProjectModel.Compilation;
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
                    let metadataName = GetResourceFileMetadataName(project, resourceFile)
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
                    let inputFileToMetadata = resourceFileGroup.ToDictionary(r => r.Key, r => GetResourceFileMetadataName(project, r))
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

        public static string GetResourceFileMetadataName(Project project, KeyValuePair<string, string> resourceFile)
        {
            string resourceName = null;
            string rootNamespace = null;

            string root = PathUtility.EnsureTrailingSlash(project.ProjectDirectory);
            string resourcePath = resourceFile.Key;
            if (string.IsNullOrEmpty(resourceFile.Value))
            {
                //  No logical name, so use the file name
                resourceName = ResourceUtility.GetResourceName(root, resourcePath);
                rootNamespace = project.Name;
            }
            else
            {
                resourceName = ResourceManifestName.EnsureResourceExtension(resourceFile.Value, resourcePath);
                rootNamespace = null;
            }

            var name = ResourceManifestName.CreateManifestName(resourceName, rootNamespace);
            return name;
        }

        // used in incremental compilation
        public static IEnumerable<string> GetCompilationSources(ProjectContext project) => project.ProjectFile.Files.SourceFiles;

        // used in incremental compilation for the key file
        public static CommonCompilerOptions ResolveCompilationOptions(ProjectContext context, string configuration)
        {
            var compilationOptions = context.GetLanguageSpecificCompilerOptions(context.TargetFramework, configuration);

            // Path to strong naming key in environment variable overrides path in project.json
            var environmentKeyFile = Environment.GetEnvironmentVariable(EnvironmentNames.StrongNameKeyFile);

            if (!string.IsNullOrWhiteSpace(environmentKeyFile))
            {
                compilationOptions.KeyFile = environmentKeyFile;
            }
            else if (!string.IsNullOrWhiteSpace(compilationOptions.KeyFile))
            {
                // Resolve full path to key file
                compilationOptions.KeyFile =
                    Path.GetFullPath(Path.Combine(context.ProjectFile.ProjectDirectory, compilationOptions.KeyFile));
            }
            return compilationOptions;
        }

        //used in incremental precondition checks
        public static IEnumerable<string> GetCommandsInvokedByCompile(ProjectContext project)
        {
            return new List<string> {project.ProjectFile.CompilerName, "compile"};
        }
    }
}