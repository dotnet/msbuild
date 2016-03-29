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
using Microsoft.DotNet.ProjectModel.Graph;
using NuGet.Versioning;

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
        public IList<ProjectContext> ProjectContexts { get; set; }
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

            ProjectContexts = SelectContexts(ProjectPath, NugetFramework, Runtime).ToList();
            if (!ProjectContexts.Any())
            {
                string errMsg = $"'{ProjectPath}' cannot be published for '{Framework ?? "<no framework provided>"}' '{Runtime ?? "<no runtime provided>"}'";
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
        /// Publish the project for given 'framework (ex - netstandardapp1.5)' and 'runtimeID (ex - win7-x64)'
        /// </summary>
        /// <param name="context">project that is to be published</param>
        /// <param name="baseOutputPath">Location of published files</param>
        /// <param name="configuration">Debug or Release</param>
        /// <param name="nativeSubdirectories"></param>
        /// <returns>Return 0 if successful else return non-zero</returns>
        private bool PublishProjectContext(ProjectContext context, string buildBasePath, string outputPath, string configuration, bool nativeSubdirectories)
        {
            var target = context.TargetFramework.DotNetFrameworkName;
            if (!string.IsNullOrEmpty(context.RuntimeIdentifier))
            {
                target = $"{target}/{context.RuntimeIdentifier}";
            }
            Reporter.Output.WriteLine($"Publishing {context.RootProject.Identity.Name.Yellow()} for {target.Yellow()}");

            var options = context.ProjectFile.GetCompilerOptions(context.TargetFramework, configuration);
            var outputPaths = context.GetOutputPaths(configuration, buildBasePath, outputPath);

            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = Path.Combine(outputPaths.RuntimeOutputPath, PublishSubfolderName);
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
                "--configuration",
                configuration,
                context.ProjectFile.ProjectDirectory
            };

            if (!string.IsNullOrEmpty(context.RuntimeIdentifier))
            {
                args.Insert(0, context.RuntimeIdentifier);
                args.Insert(0, "--runtime");
            }

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

            var isPortable = string.IsNullOrEmpty(context.RuntimeIdentifier);

            // Collect all exports and organize them
            var exports = exporter.GetAllExports()
                .Where(e => e.Library.Identity.Type.Equals(LibraryType.Package))
                .ToDictionary(e => e.Library.Identity.Name);
            var collectExclusionList = isPortable ? GetExclusionList(context, exports) : new HashSet<string>();

            foreach (var export in exporter.GetAllExports().Where(e => !collectExclusionList.Contains(e.Library.Identity.Name)))
            {
                Reporter.Verbose.WriteLine($"Publishing {export.Library.Identity.ToString().Green().Bold()} ...");

                PublishAssetGroups(export.RuntimeAssemblyGroups, outputPath, nativeSubdirectories: false, includeRuntimeGroups: isPortable);
                PublishAssetGroups(export.NativeLibraryGroups, outputPath, nativeSubdirectories, includeRuntimeGroups: isPortable);
                export.RuntimeAssets.StructuredCopyTo(outputPath, outputPaths.IntermediateOutputDirectoryPath);

                if (options.PreserveCompilationContext.GetValueOrDefault())
                {
                    PublishRefs(export, outputPath);
                }
            }

            if (context.ProjectFile.HasRuntimeOutput(configuration) && !context.TargetFramework.IsDesktop())
            {
                // Get the output paths used by the call to `dotnet build` above (since we didn't pass `--output`, they will be different from
                // our current output paths)
                var buildOutputPaths = context.GetOutputPaths(configuration, buildBasePath);
                PublishFiles(
                    new[] {
                        buildOutputPaths.RuntimeFiles.Deps,
                        buildOutputPaths.RuntimeFiles.DepsJson,
                        buildOutputPaths.RuntimeFiles.RuntimeConfigJson
                    },
                    outputPath);
            }

            var contentFiles = new ContentFiles(context);
            contentFiles.StructuredCopyTo(outputPath);

            // Publish a host if this is an application
            if (options.EmitEntryPoint.GetValueOrDefault() && !string.IsNullOrEmpty(context.RuntimeIdentifier))
            {
                Reporter.Verbose.WriteLine($"Copying native host to output to create fully standalone output.");
                PublishHost(context, outputPath, options);
            }

            RunScripts(context, ScriptNames.PostPublish, contextVariables);

            Reporter.Output.WriteLine($"Published to {outputPath}".Green().Bold());

            return true;
        }

        private HashSet<string> GetExclusionList(ProjectContext context, Dictionary<string, LibraryExport> exports)
        {
            var exclusionList = new HashSet<string>();
            var redistPackages = context.RootProject.Dependencies
                .Where(r => r.Type.Equals(LibraryDependencyType.Platform))
                .ToList();
            if (redistPackages.Count == 0)
            {
                return exclusionList;
            }
            else if (redistPackages.Count > 1)
            {
                throw new InvalidOperationException("Multiple packages with type: \"platform\" were specified!");
            }
            var redistExport = exports[redistPackages[0].Name];

            exclusionList.Add(redistExport.Library.Identity.Name);
            CollectDependencies(exports, redistExport.Library.Dependencies, exclusionList);
            return exclusionList;
        }

        private void CollectDependencies(Dictionary<string, LibraryExport> exports, IEnumerable<LibraryRange> dependencies, HashSet<string> exclusionList)
        {
            foreach (var dependency in dependencies)
            {
                var export = exports[dependency.Name];
                if(export.Library.Identity.Version.Equals(dependency.VersionRange.MinVersion))
                {
                    exclusionList.Add(export.Library.Identity.Name);
                    CollectDependencies(exports, export.Library.Dependencies, exclusionList);
                }
            }
        }

        private static void PublishRefs(LibraryExport export, string outputPath)
        {
            var refsPath = Path.Combine(outputPath, "refs");
            if (!Directory.Exists(refsPath))
            {
                Directory.CreateDirectory(refsPath);
            }

            // Do not copy compilation assembly if it's in runtime assemblies
            var runtimeAssemblies = new HashSet<LibraryAsset>(export.RuntimeAssemblyGroups.GetDefaultAssets());
            foreach (var compilationAssembly in export.CompilationAssemblies)
            {
                if (!runtimeAssemblies.Contains(compilationAssembly))
                {
                    var destFileName = Path.Combine(refsPath, Path.GetFileName(compilationAssembly.ResolvedPath));
                    File.Copy(compilationAssembly.ResolvedPath, destFileName, overwrite: true);
                }
            }
        }

        private static int PublishHost(ProjectContext context, string outputPath, CommonCompilerOptions compilationOptions)
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

                var outputBinaryName = binaryName.Equals(Constants.HostExecutableName)
                    ? compilationOptions.OutputName + Constants.ExeSuffix
                    : binaryName;
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

        private void PublishAssetGroups(IEnumerable<LibraryAssetGroup> groups, string outputPath, bool nativeSubdirectories, bool includeRuntimeGroups)
        {
            foreach (var group in groups.Where(g => includeRuntimeGroups || string.IsNullOrEmpty(g.Runtime)))
            {
                foreach (var file in group.Assets)
                {
                    var destinationDirectory = DetermineFileDestinationDirectory(file, outputPath, nativeSubdirectories);

                    if (!string.IsNullOrEmpty(group.Runtime))
                    {
                        destinationDirectory = Path.Combine(destinationDirectory, Path.GetDirectoryName(file.RelativePath));
                    }

                    if (!Directory.Exists(destinationDirectory))
                    {
                        Directory.CreateDirectory(destinationDirectory);
                    }

                    File.Copy(file.ResolvedPath, Path.Combine(destinationDirectory, file.FileName), overwrite: true);
                }
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

        private IEnumerable<ProjectContext> SelectContexts(string projectPath, NuGetFramework framework, string runtime)
        {
            var allContexts = ProjectContext.CreateContextForEachTarget(projectPath).ToList();
            var frameworks = framework == null ?
                allContexts.Select(c => c.TargetFramework).Distinct().ToArray() :
                new[] { framework };

            if (string.IsNullOrEmpty(runtime))
            {
                // For each framework, find the best matching RID item
                var candidates = PlatformServices.Default.Runtime.GetAllCandidateRuntimeIdentifiers();
                return frameworks.Select(f => FindBestTarget(f, allContexts, candidates));
            }
            else
            {
                return frameworks.SelectMany(f => allContexts.Where(c =>
                    Equals(c.TargetFramework, f) &&
                    string.Equals(c.RuntimeIdentifier, runtime, StringComparison.Ordinal)));
            }
        }

        private ProjectContext FindBestTarget(NuGetFramework f, List<ProjectContext> allContexts, IEnumerable<string> candidates)
        {
            foreach (var candidate in candidates)
            {
                var target = allContexts.FirstOrDefault(c =>
                    Equals(c.TargetFramework, f) &&
                    string.Equals(c.RuntimeIdentifier, candidate, StringComparison.Ordinal));
                if (target != null)
                {
                    return target;
                }
            }

            // No RID-specific target found, use the RID-less target and publish portable
            return allContexts.FirstOrDefault(c =>
                Equals(c.TargetFramework, f) &&
                string.IsNullOrEmpty(c.RuntimeIdentifier));
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
