// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Compilation;
using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Compiler.Common;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.DotNet.Files;
using Microsoft.DotNet.Tools.Common;
using Microsoft.DotNet.ProjectModel.Utilities;

namespace Microsoft.DotNet.Tools.Publish
{
    public partial class PublishCommand
    {
        private const string PublishSubfolderName = "publish";

        public string ProjectPath { get; set; }
        public string Configuration { get; set; }
        public string BuildBasePath { get; set; }
        public string OutputPath { get; set; }
        public string Framework { get; set; }
        public string Runtime { get; set; }
        public bool NativeSubdirectories { get; set; }
        public NuGetFramework NugetFramework { get; set; }
        public IEnumerable<ProjectContext> ProjectContexts { get; set; }
        public string VersionSuffix { get; set; }
        public int NumberOfProjects { get; private set; }
        public int NumberOfPublishedProjects { get; private set; }

        public bool TryPrepareForPublish()
        {
            if (Framework != null)
            {
                NugetFramework = NuGetFramework.Parse(Framework);

                if (NugetFramework.IsUnsupported)
                {
                    Reporter.Output.WriteLine($"Unsupported framework {Framework}.".Red());
                    return false;
                }
            }

            ProjectContexts = SelectContexts(ProjectPath, NugetFramework, Runtime);
            if (!ProjectContexts.Any())
            {
                string errMsg = $"'{ProjectPath}' cannot be published  for '{Framework ?? "<no framework provided>"}' '{Runtime ?? "<no runtime provided>"}'";
                Reporter.Output.WriteLine(errMsg.Red());
                return false;
            }

            return true;
        }

        public void PublishAllProjects()
        {
            NumberOfPublishedProjects = 0;
            NumberOfProjects = 0;

            foreach (var project in ProjectContexts)
            {
                if (PublishProjectContext(project, BuildBasePath, OutputPath, Configuration, NativeSubdirectories))
                {
                    NumberOfPublishedProjects++;
                }

                NumberOfProjects++;
            }
        }

        /// <summary>
        /// Publish the project for given 'framework (ex - dnxcore50)' and 'runtimeID (ex - win7-x64)'
        /// </summary>
        /// <param name="context">project that is to be published</param>
        /// <param name="baseOutputPath">Location of published files</param>
        /// <param name="configuration">Debug or Release</param>
        /// <param name="nativeSubdirectories"></param>
        /// <returns>Return 0 if successful else return non-zero</returns>
        private bool PublishProjectContext(ProjectContext context, string buildBasePath, string outputPath, string configuration, bool nativeSubdirectories)
        {
            Reporter.Output.WriteLine($"Publishing {context.RootProject.Identity.Name.Yellow()} for {context.TargetFramework.DotNetFrameworkName.Yellow()}/{context.RuntimeIdentifier.Yellow()}");

            var options = context.ProjectFile.GetCompilerOptions(context.TargetFramework, configuration);

            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = Path.Combine(context.GetOutputPaths(configuration, buildBasePath, outputPath).RuntimeOutputPath, PublishSubfolderName);
            }

            var contextVariables = new Dictionary<string, string>
            {
                { "publish:ProjectPath", context.ProjectDirectory },
                { "publish:Configuration", configuration },
                { "publish:OutputPath", outputPath },
                { "publish:TargetFramework", context.TargetFramework.GetShortFolderName() },
                { "publish:FullTargetFramework", context.TargetFramework.DotNetFrameworkName },
                { "publish:Runtime", context.RuntimeIdentifier },
            };

            RunScripts(context, ScriptNames.PrePublish, contextVariables);

            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            // Compile the project (and transitively, all it's dependencies)
            var args = new List<string>() {
                "--framework",
                $"{context.TargetFramework.DotNetFrameworkName}",
                "--runtime",
                context.RuntimeIdentifier,
                "--configuration",
                configuration,
                context.ProjectFile.ProjectDirectory
            };

            if (!string.IsNullOrEmpty(VersionSuffix))
            {
                args.Add("--version-suffix");
                args.Add(VersionSuffix);
            }

            if (!string.IsNullOrEmpty(buildBasePath))
            {
                args.Add("--build-base-path");
                args.Add(buildBasePath);
            }

            var result = Build.BuildCommand.Run(args.ToArray());
            if (result != 0)
            {
                return false;
            }

            // Use a library exporter to collect publish assets
            var exporter = context.CreateExporter(configuration);

            foreach (var export in exporter.GetAllExports())
            {
                Reporter.Verbose.WriteLine($"Publishing {export.Library.Identity.ToString().Green().Bold()} ...");

                PublishFiles(export.RuntimeAssemblies, outputPath, nativeSubdirectories: false);
                PublishFiles(export.NativeLibraries, outputPath, nativeSubdirectories);
                export.RuntimeAssets.StructuredCopyTo(outputPath);

                if (options.PreserveCompilationContext.GetValueOrDefault())
                {
                    PublishRefs(export, outputPath);
                }
            }

            var contentFiles = new ContentFiles(context);
            contentFiles.StructuredCopyTo(outputPath);

            // Publish a host if this is an application
            if (options.EmitEntryPoint.GetValueOrDefault())
            {
                Reporter.Verbose.WriteLine($"Making {context.ProjectFile.Name.Cyan()} runnable ...");
                PublishHost(context, outputPath);
            }

            RunScripts(context, ScriptNames.PostPublish, contextVariables);

            Reporter.Output.WriteLine($"Published to {outputPath}".Green().Bold());

            return true;
        }

        private static void PublishRefs(LibraryExport export, string outputPath)
        {
            var refsPath = Path.Combine(outputPath, "refs");
            if (!Directory.Exists(refsPath))
            {
                Directory.CreateDirectory(refsPath);
            }

            // Do not copy compilation assembly if it's in runtime assemblies
            var runtimeAssemblies = new HashSet<LibraryAsset>(export.RuntimeAssemblies);
            foreach (var compilationAssembly in export.CompilationAssemblies)
            {
                if (!runtimeAssemblies.Contains(compilationAssembly))
                {
                    var destFileName = Path.Combine(refsPath, Path.GetFileName(compilationAssembly.ResolvedPath));
                    File.Copy(compilationAssembly.ResolvedPath, destFileName, overwrite: true);
                }
            }
        }

        private static int PublishHost(ProjectContext context, string outputPath)
        {
            if (context.TargetFramework.IsDesktop())
            {
                return 0;
            }

            foreach (var binaryName in Constants.HostBinaryNames)
            {
                var hostBinaryPath = Path.Combine(AppContext.BaseDirectory, binaryName);
                if (!File.Exists(hostBinaryPath))
                {
                    Reporter.Error.WriteLine($"Cannot find {binaryName} in the dotnet directory.".Red());
                    return 1;
                }

                var outputBinaryName = binaryName.Equals(Constants.HostExecutableName) ? (context.ProjectFile.Name + Constants.ExeSuffix) : binaryName;
                var outputBinaryPath = Path.Combine(outputPath, outputBinaryName);

                File.Copy(hostBinaryPath, outputBinaryPath, overwrite: true);
            }

            return 0;
        }

        private static void PublishFiles(IEnumerable<string> files, string outputPath)
        {
            foreach (var file in files)
            {
                var targetPath = Path.Combine(outputPath, Path.GetFileName(file));
                File.Copy(file, targetPath, overwrite: true);
            }
        }

        private static void PublishFiles(IEnumerable<LibraryAsset> files, string outputPath, bool nativeSubdirectories)
        {
            foreach (var file in files)
            {
                var destinationDirectory = DetermineFileDestinationDirectory(file, outputPath, nativeSubdirectories);

                if (!Directory.Exists(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                File.Copy(file.ResolvedPath, Path.Combine(destinationDirectory, Path.GetFileName(file.ResolvedPath)), overwrite: true);
            }
        }

        private static string DetermineFileDestinationDirectory(LibraryAsset file, string outputPath, bool nativeSubdirectories)
        {
            var destinationDirectory = outputPath;

            if (nativeSubdirectories)
            {
                destinationDirectory = Path.Combine(outputPath, GetNativeRelativeSubdirectory(file.RelativePath));
            }

            return destinationDirectory;
        }

        private static string GetNativeRelativeSubdirectory(string filepath)
        {
            string directoryPath = Path.GetDirectoryName(filepath);

            string[] parts = directoryPath.Split(new string[] { "native" }, 2, StringSplitOptions.None);

            if (parts.Length != 2)
            {
                throw new Exception("Unrecognized Native Directory Format: " + filepath);
            }

            string candidate = parts[1];
            candidate = candidate.TrimStart(new char[] { '/', '\\' });

            return candidate;
        }

        private static IEnumerable<ProjectContext> SelectContexts(string projectPath, NuGetFramework framework, string runtime)
        {
            var allContexts = ProjectContext.CreateContextForEachTarget(projectPath);
            if (string.IsNullOrEmpty(runtime))
            {
                // Nothing was specified, so figure out what the candidate runtime identifiers are and try each of them
                // Temporary until #619 is resolved
                foreach (var candidate in PlatformServices.Default.Runtime.GetAllCandidateRuntimeIdentifiers())
                {
                    var contexts = GetMatchingProjectContexts(allContexts, framework, candidate);
                    if (contexts.Any())
                    {
                        return contexts;
                    }
                }
                return Enumerable.Empty<ProjectContext>();
            }
            else
            {
                return GetMatchingProjectContexts(allContexts, framework, runtime);
            }
        }

        /// <summary>
        /// Return the matching framework/runtime ProjectContext.
        /// If 'framework' or 'runtimeIdentifier' is null or empty then it matches with any.
        /// </summary>
        private static IEnumerable<ProjectContext> GetMatchingProjectContexts(IEnumerable<ProjectContext> contexts, NuGetFramework framework, string runtimeIdentifier)
        {
            foreach (var context in contexts)
            {
                if (context.TargetFramework == null || string.IsNullOrEmpty(context.RuntimeIdentifier))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(runtimeIdentifier) || string.Equals(runtimeIdentifier, context.RuntimeIdentifier, StringComparison.OrdinalIgnoreCase))
                {
                    if (framework == null || framework.Equals(context.TargetFramework))
                    {
                        yield return context;
                    }
                }
            }
        }

        private static void CopyContents(ProjectContext context, string outputPath)
        {
            var contentFiles = context.ProjectFile.Files.GetContentFiles();
            Copy(contentFiles, context.ProjectDirectory, outputPath);
        }

        private static void Copy(IEnumerable<string> contentFiles, string sourceDirectory, string targetDirectory)
        {
            if (contentFiles == null)
            {
                throw new ArgumentNullException(nameof(contentFiles));
            }

            sourceDirectory = PathUtility.EnsureTrailingSlash(sourceDirectory);
            targetDirectory = PathUtility.EnsureTrailingSlash(targetDirectory);

            foreach (var contentFilePath in contentFiles)
            {
                Reporter.Verbose.WriteLine($"Publishing {contentFilePath.Green().Bold()} ...");

                var fileName = Path.GetFileName(contentFilePath);

                var targetFilePath = contentFilePath.Replace(sourceDirectory, targetDirectory);
                var targetFileParentFolder = Path.GetDirectoryName(targetFilePath);

                // Create directory before copying a file
                if (!Directory.Exists(targetFileParentFolder))
                {
                    Directory.CreateDirectory(targetFileParentFolder);
                }

                File.Copy(
                    contentFilePath,
                    targetFilePath,
                    overwrite: true);

                // clear read-only bit if set
                var fileAttributes = File.GetAttributes(targetFilePath);
                if ((fileAttributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    File.SetAttributes(targetFilePath, fileAttributes & ~FileAttributes.ReadOnly);
                }
            }
        }

        private static void RunScripts(ProjectContext context, string name, Dictionary<string, string> contextVariables)
        {
            foreach (var script in context.ProjectFile.Scripts.GetOrEmpty(name))
            {
                ScriptExecutor.CreateCommandForScript(context.ProjectFile, script, contextVariables)
                    .ForwardStdErr()
                    .ForwardStdOut()
                    .Execute();
            }
        }
    }
}
