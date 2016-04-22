// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Compiler.Common;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Files;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Compilation;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.DotNet.ProjectModel.Utilities;
using Microsoft.DotNet.Tools.Common;
using Microsoft.Extensions.PlatformAbstractions;
using NuGet.Frameworks;

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
        public WorkspaceContext Workspace { get; set; }
        public IList<ProjectContext> ProjectContexts { get; set; }
        public string VersionSuffix { get; set; }
        public int NumberOfProjects { get; private set; }
        public int NumberOfPublishedProjects { get; private set; }
        public bool ShouldBuild { get; set; }

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
        /// Publish the project for given 'framework (ex - netcoreapp1.0)' and 'runtimeID (ex - win7-x64)'
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
            if (ShouldBuild && !InvokeBuildOnProject(context, buildBasePath, configuration))
            {
                return false;
            }

            // Use a library exporter to collect publish assets
            var exporter = context.CreateExporter(configuration, buildBasePath);

            // Collect all exports and organize them
            var packageExports = exporter.GetAllExports()
                .Where(e => e.Library.Identity.Type.Equals(LibraryType.Package))
                .ToDictionary(e => e.Library.Identity.Name);
            var collectExclusionList = context.IsPortable ? GetExclusionList(context, packageExports) : new HashSet<string>();

            // Get the output paths used by the call to `dotnet build` above (since we didn't pass `--output`, they will be different from
            // our current output paths)
            var buildOutputPaths = context.GetOutputPaths(configuration, buildBasePath);

            var exports = exporter.GetAllExports();
            foreach (var export in exports.Where(e => !collectExclusionList.Contains(e.Library.Identity.Name)))
            {
                Reporter.Verbose.WriteLine($"publish: Publishing {export.Library.Identity.ToString().Green().Bold()} ...");

                PublishAssetGroups(export.RuntimeAssemblyGroups, outputPath, nativeSubdirectories: false, includeRuntimeGroups: context.IsPortable);
                PublishAssetGroups(export.NativeLibraryGroups, outputPath, nativeSubdirectories, includeRuntimeGroups: context.IsPortable);

                var runtimeAssetsToCopy = export.RuntimeAssets.Where(a => ShouldCopyExportRuntimeAsset(context, buildOutputPaths, export, a));
                runtimeAssetsToCopy.StructuredCopyTo(outputPath, outputPaths.IntermediateOutputDirectoryPath);

                foreach(var resourceAsset in export.ResourceAssemblies)
                {
                    var dir = Path.Combine(outputPath, resourceAsset.Locale);
                    if(!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    File.Copy(resourceAsset.Asset.ResolvedPath, Path.Combine(dir, resourceAsset.Asset.FileName), overwrite: true);
                }
            }

            if (context.ProjectFile.HasRuntimeOutput(configuration) && !context.TargetFramework.IsDesktop())
            {
                PublishFiles(
                    new[] {
                        buildOutputPaths.RuntimeFiles.DepsJson,
                        buildOutputPaths.RuntimeFiles.RuntimeConfigJson
                    },
                    outputPath);
            }

            if (options.PreserveCompilationContext.GetValueOrDefault())
            {
                foreach (var export in exports)
                {
                    PublishRefs(export, outputPath, !collectExclusionList.Contains(export.Library.Identity.Name));
                }
            }

            var contentFiles = new ContentFiles(context);
            contentFiles.StructuredCopyTo(outputPath);

            // Publish a host if this is an application
            if (options.EmitEntryPoint.GetValueOrDefault() && !string.IsNullOrEmpty(context.RuntimeIdentifier))
            {
                Reporter.Verbose.WriteLine($"publish: Renaming native host in output to create fully standalone output.");
                RenamePublishedHost(context, outputPath, options);
            }

            RunScripts(context, ScriptNames.PostPublish, contextVariables);

            Reporter.Output.WriteLine($"publish: Published to {outputPath}".Green().Bold());

            return true;
        }

        /// <summary>
        /// Filters which export's RuntimeAssets should get copied to the output path.
        /// </summary>
        /// <returns>
        /// True if the asset should be copied to the output path; otherwise, false.
        /// </returns>
        private static bool ShouldCopyExportRuntimeAsset(ProjectContext context, OutputPaths buildOutputPaths, LibraryExport export, LibraryAsset asset)
        {
            // The current project has the host .exe in its runtime assets, but it shouldn't be copied
            // to the output path during publish. The host will come from the export that has the real host in it.

            if (context.RootProject.Identity == export.Library.Identity)
            {
                if (asset.ResolvedPath == buildOutputPaths.RuntimeFiles.Executable)
                {
                    return false;
                }
            }

            return true;
        }

        private bool InvokeBuildOnProject(ProjectContext context, string buildBasePath, string configuration)
        {
            var args = new List<string>()
            {
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

            return result == 0;
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

        private static void PublishRefs(LibraryExport export, string outputPath, bool deduplicate)
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
                if (!deduplicate || !runtimeAssemblies.Contains(compilationAssembly))
                {
                    var destFileName = Path.Combine(refsPath, Path.GetFileName(compilationAssembly.ResolvedPath));
                    File.Copy(compilationAssembly.ResolvedPath, destFileName, overwrite: true);
                }
            }
        }

        private static int RenamePublishedHost(ProjectContext context, string outputPath, CommonCompilerOptions compilationOptions)
        {
            if (context.TargetFramework.IsDesktop())
            {
                return 0;
            }

            var publishedHostFile = ResolvePublishedHostFile(outputPath);
            if (publishedHostFile == null)
            {
                Reporter.Output.WriteLine($"publish: warning: host executable not available in dependencies, using host for current platform");
                // TODO should this be an error?

                CoreHost.CopyTo(outputPath, compilationOptions.OutputName + Constants.ExeSuffix);
                return 0;
            }

            var publishedHostExtension = Path.GetExtension(publishedHostFile);
            var renamedHostName = compilationOptions.OutputName + publishedHostExtension;
            var renamedHostFile = Path.Combine(outputPath, renamedHostName);

            try
            {
                Reporter.Verbose.WriteLine($"publish: renaming published host {publishedHostFile} to {renamedHostFile}");
                File.Copy(publishedHostFile, renamedHostFile, true);
                File.Delete(publishedHostFile);
            }
            catch (Exception e)
            {
                Reporter.Error.WriteLine($"publish: Failed to rename {publishedHostFile} to {renamedHostFile}: {e.Message}");
                return 1;
            }

            return 0;
        }

        private static string ResolvePublishedHostFile(string outputPath)
        {
            var tryExtensions = new string[] { "", ".exe" };

            foreach (var extension in tryExtensions)
            {
                var hostFile = Path.Combine(outputPath, Constants.PublishedHostExecutableName + extension);
                if (File.Exists(hostFile))
                {
                    Reporter.Verbose.WriteLine($"resolved published host: {hostFile}");
                    return hostFile;
                }
            }

            Reporter.Verbose.WriteLine($"failed to resolve published host in: {outputPath}");
            return null;
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

                    Reporter.Verbose.WriteLine($"Publishing file {Path.GetFileName(file.RelativePath)} to {destinationDirectory}");
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
            if (projectPath.EndsWith("project.json"))
            {
                if (File.Exists(projectPath) == false)
                    throw new InvalidProjectException($"'{projectPath}' does not exist");
            }
            else if (File.Exists(Path.Combine(projectPath, "project.json")) == false)
            {
                throw new InvalidProjectException($"'{projectPath}' does not contain a project.json file");
            }

            var contexts = Workspace.GetProjectContextCollection(projectPath).FrameworkOnlyContexts;

            contexts = framework == null ?
                contexts :
                contexts.Where(c => Equals(c.TargetFramework, framework));

            var rids = string.IsNullOrEmpty(runtime) ?
                PlatformServices.Default.Runtime.GetAllCandidateRuntimeIdentifiers() :
                new[] { runtime };

            return contexts.Select(c => Workspace.GetRuntimeContext(c, rids));
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
