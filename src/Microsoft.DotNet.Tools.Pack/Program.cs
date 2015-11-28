// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using System.Text;
using Microsoft.DotNet.ProjectModel;
using NuGet;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Packaging.Core;
using Microsoft.DotNet.ProjectModel.Graph;
using NuGet.Versioning;
using NuGet.Frameworks;
using Microsoft.DotNet.ProjectModel.Files;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.DotNet.ProjectModel.Utilities;

namespace Microsoft.DotNet.Tools.Compiler
{
    public class Program
    {
        public static int Main(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var app = new CommandLineApplication();
            app.Name = "dotnet compile";
            app.FullName = ".NET Compiler";
            app.Description = "Compiler for the .NET Platform";
            app.HelpOption("-h|--help");

            var output = app.Option("-o|--output <OUTPUT_DIR>", "Directory in which to place outputs", CommandOptionType.SingleValue);
            var intermediateOutput = app.Option("-t|--temp-output <OUTPUT_DIR>", "Directory in which to place temporary outputs", CommandOptionType.SingleValue);
            var configuration = app.Option("-c|--configuration <CONFIGURATION>", "Configuration under which to build", CommandOptionType.SingleValue);
            var project = app.Argument("<PROJECT>", "The project to compile, defaults to the current directory. Can be a path to a project.json or a project directory");

            app.OnExecute(() =>
            {
                // Locate the project and get the name and full path
                var path = project.Value;
                if (string.IsNullOrEmpty(path))
                {
                    path = Directory.GetCurrentDirectory();
                }

                var configValue = configuration.Value() ?? Cli.Utils.Constants.DefaultConfiguration;
                var outputValue = output.Value();

                return BuildPackage(path, configValue, outputValue, intermediateOutput.Value()) ? 1 : 0;
            });

            try
            {
                return app.Execute(args);
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.Error.WriteLine(ex);
#else
                Console.Error.WriteLine(ex.Message);
#endif
                return 1;
            }
        }

        private static bool BuildPackage(string path, string configuration, string outputValue, string intermediateOutputValue)
        {
            var contexts = ProjectContext.CreateContextForEachFramework(path);
            var project = contexts.First().ProjectFile;

            if (project.Files.SourceFiles.Any())
            {
                var argsBuilder = new StringBuilder();
                argsBuilder.Append($"--configuration {configuration}");

                if (!string.IsNullOrEmpty(outputValue))
                {
                    argsBuilder.Append($" --output \"{outputValue}\"");
                }

                if (!string.IsNullOrEmpty(intermediateOutputValue))
                {
                    argsBuilder.Append($" --temp-output \"{intermediateOutputValue}\"");
                }

                argsBuilder.Append($" \"{path}\"");

                var result = Command.Create("dotnet-compile", argsBuilder.ToString())
                       .ForwardStdOut()
                       .ForwardStdErr()
                       .Execute();

                if (result.ExitCode != 0)
                {
                    return false;
                }
            }

            Reporter.Output.WriteLine($"Producing nuget package for {project.Name}");

            var packDiagnostics = new List<DiagnosticMessage>();

            // Things compiled now build the package
            var packageBuilder = CreatePackageBuilder(project);

            // TODO: Report errors for required fields
            // id
            // author
            // description
            foreach (var context in contexts)
            {
                Reporter.Verbose.WriteLine($"Processing {context.TargetFramework.ToString().Yellow()}");
                PopulateDependencies(context, packageBuilder);

                var outputPath = GetOutputPath(context, configuration, outputValue);
                var outputName = GetProjectOutputName(context.ProjectFile, context.TargetFramework, configuration);

                TryAddOutputFile(packageBuilder, context, outputPath, outputName);

                // REVIEW: Do we keep making symbols packages?
                TryAddOutputFile(packageBuilder, context, outputPath, $"{project.Name}.pdb");
                TryAddOutputFile(packageBuilder, context, outputPath, $"{project.Name}.mdb");

                TryAddOutputFile(packageBuilder, context, outputPath, $"{project.Name}.xml");

                Reporter.Verbose.WriteLine("");
            }

            var rootOutputPath = GetOutputPath(project, configuration, outputValue);
            var packageOutputPath = GetPackagePath(project, rootOutputPath);

            if (GeneratePackage(project, packageBuilder, packageOutputPath, packDiagnostics))
            {
                return true;
            }

            return false;
        }

        private static bool GeneratePackage(Project project, PackageBuilder packageBuilder, string nupkg, List<DiagnosticMessage> packDiagnostics)
        {
            foreach (var sharedFile in project.Files.SharedFiles)
            {
                var file = new PhysicalPackageFile();
                file.SourcePath = sharedFile;
                file.TargetPath = Path.Combine("shared", Path.GetFileName(sharedFile));
                packageBuilder.Files.Add(file);
            }

            var root = project.ProjectDirectory;

            if (project.Files.PackInclude != null && project.Files.PackInclude.Any())
            {
                AddPackageFiles(project, project.Files.PackInclude, packageBuilder, packDiagnostics);
            }

            // Write the packages as long as we're still in a success state.
            if (!packDiagnostics.Any(d => d.Severity == DiagnosticMessageSeverity.Error))
            {
                Reporter.Verbose.WriteLine($"Adding package files");
                foreach (var file in packageBuilder.Files.OfType<PhysicalPackageFile>())
                {
                    if (file.SourcePath != null && File.Exists(file.SourcePath))
                    {
                        Reporter.Verbose.WriteLine($"Adding {file.Path.Yellow()}");
                    }
                }

                Directory.CreateDirectory(Path.GetDirectoryName(nupkg));

                using (var fs = File.Create(nupkg))
                {
                    packageBuilder.Save(fs);
                    Reporter.Output.WriteLine($"{project.Name} -> {Path.GetFullPath(nupkg)}");
                }

                return true;
            }

            return false;
        }

        private static void AddPackageFiles(Project project, IEnumerable<PackIncludeEntry> packageFiles, PackageBuilder packageBuilder, IList<DiagnosticMessage> diagnostics)
        {
            var rootDirectory = new DirectoryInfoWrapper(new DirectoryInfo(project.ProjectDirectory));

            foreach (var match in CollectAdditionalFiles(rootDirectory, packageFiles, project.ProjectFilePath, diagnostics))
            {
                packageBuilder.Files.Add(match);
            }
        }

        internal static IEnumerable<PhysicalPackageFile> CollectAdditionalFiles(DirectoryInfoBase rootDirectory, IEnumerable<PackIncludeEntry> projectFileGlobs, string projectFilePath, IList<DiagnosticMessage> diagnostics)
        {
            foreach (var entry in projectFileGlobs)
            {
                // Evaluate the globs on the right
                var matcher = new Matcher();
                matcher.AddIncludePatterns(entry.SourceGlobs);
                var results = matcher.Execute(rootDirectory);
                var files = results.Files.ToList();

                // Check for illegal characters
                if (string.IsNullOrEmpty(entry.Target))
                {
                    diagnostics.Add(new DiagnosticMessage(
                        ErrorCodes.NU1003,
                        $"Invalid '{ProjectFilesCollection.PackIncludePropertyName}' section. The target '{entry.Target}' is invalid, " +
                        "targets must either be a file name or a directory suffixed with '/'. " +
                        "The root directory of the package can be specified by using a single '/' character.",
                        projectFilePath,
                        DiagnosticMessageSeverity.Error,
                        entry.Line,
                        entry.Column));
                    continue;
                }

                if (entry.Target.Split('/').Any(s => s.Equals(".") || s.Equals("..")))
                {
                    diagnostics.Add(new DiagnosticMessage(
                        ErrorCodes.NU1004,
                        $"Invalid '{ProjectFilesCollection.PackIncludePropertyName}' section. " +
                        $"The target '{entry.Target}' contains path-traversal characters ('.' or '..'). " +
                        "These characters are not permitted in target paths.",
                        projectFilePath,
                        DiagnosticMessageSeverity.Error,
                        entry.Line,
                        entry.Column));
                    continue;
                }

                // Check the arity of the left
                if (entry.Target.EndsWith("/"))
                {
                    var dir = entry.Target.Substring(0, entry.Target.Length - 1).Replace('/', Path.DirectorySeparatorChar);

                    foreach (var file in files)
                    {
                        yield return new PhysicalPackageFile()
                        {
                            SourcePath = Path.Combine(rootDirectory.FullName, PathUtility.GetPathWithDirectorySeparator(file.Path)),
                            TargetPath = Path.Combine(dir, PathUtility.GetPathWithDirectorySeparator(file.Stem))
                        };
                    }
                }
                else
                {
                    // It's a file. If the glob matched multiple things, we're sad :(
                    if (files.Count > 1)
                    {
                        // Arity mismatch!
                        string sourceValue = entry.SourceGlobs.Length == 1 ?
                            $"\"{entry.SourceGlobs[0]}\"" :
                            ("[" + string.Join(",", entry.SourceGlobs.Select(v => $"\"{v}\"")) + "]");
                        diagnostics.Add(new DiagnosticMessage(
                            ErrorCodes.NU1005,
                            $"Invalid '{ProjectFilesCollection.PackIncludePropertyName}' section. " +
                            $"The target '{entry.Target}' refers to a single file, but the pattern {sourceValue} " +
                            "produces multiple files. To mark the target as a directory, suffix it with '/'.",
                            projectFilePath,
                            DiagnosticMessageSeverity.Error,
                            entry.Line,
                            entry.Column));
                    }
                    else
                    {
                        yield return new PhysicalPackageFile()
                        {
                            SourcePath = Path.Combine(rootDirectory.FullName, files[0].Path),
                            TargetPath = PathUtility.GetPathWithDirectorySeparator(entry.Target)
                        };
                    }
                }
            }
        }

        private static void TryAddOutputFile(PackageBuilder packageBuilder,
                                             ProjectContext context,
                                             string outputPath,
                                             string filePath)
        {
            var targetPath = Path.Combine("lib", context.TargetFramework.GetTwoDigitShortFolderName(), Path.GetFileName(filePath));
            var sourcePath = Path.Combine(outputPath, filePath);

            if (!File.Exists(sourcePath))
            {
                return;
            }

            packageBuilder.Files.Add(new PhysicalPackageFile
            {
                SourcePath = sourcePath,
                TargetPath = targetPath
            });
        }

        public static void PopulateDependencies(ProjectContext context, PackageBuilder packageBuilder)
        {
            var dependencies = new List<PackageDependency>();
            var project = context.RootProject;

            foreach (var dependency in project.Dependencies)
            {
                if (!dependency.HasFlag(LibraryDependencyTypeFlag.BecomesNupkgDependency))
                {
                    continue;
                }

                // TODO: Efficiency
                var dependencyDescription = context.LibraryManager.GetLibraries().First(l => l.RequestedRanges.Contains(dependency));

                // REVIEW: Can we get this far with unresolved dependencies
                if (dependencyDescription == null || !dependencyDescription.Resolved)
                {
                    continue;
                }

                if (dependencyDescription.Identity.Type == LibraryType.Project &&
                    ((ProjectDescription)dependencyDescription).Project.EmbedInteropTypes)
                {
                    continue;
                }

                if (dependency.Target == LibraryType.ReferenceAssembly)
                {
                    packageBuilder.FrameworkAssemblies.Add(new FrameworkAssemblyReference(dependency.Name, new[] { context.TargetFramework }));

                    Reporter.Verbose.WriteLine($"Adding framework assembly {dependency.Name.Yellow()}");
                }
                else
                {
                    VersionRange dependencyVersion = null;

                    if (dependency.VersionRange == null ||
                        dependency.VersionRange.IsFloating)
                    {
                        dependencyVersion = new VersionRange(dependencyDescription.Identity.Version);
                    }
                    else
                    {
                        dependencyVersion = dependency.VersionRange;
                    }

                    Reporter.Verbose.WriteLine($"Adding dependency {dependency.Name.Yellow()} {VersionUtility.RenderVersion(dependencyVersion).Yellow()}");

                    dependencies.Add(new PackageDependency(dependency.Name, dependencyVersion));
                }
            }

            packageBuilder.DependencySets.Add(new PackageDependencySet(context.TargetFramework, dependencies));
        }

        private static string GetPackagePath(Project project, string outputPath, bool symbols = false)
        {
            string fileName = $"{project.Name}.{project.Version}{(symbols ? ".symbols" : string.Empty)}{NuGet.Constants.PackageExtension}";
            return Path.Combine(outputPath, fileName);
        }

        private static PackageBuilder CreatePackageBuilder(Project project)
        {
            var builder = new PackageBuilder();
            builder.Authors.AddRange(project.Authors);
            builder.Owners.AddRange(project.Owners);

            if (builder.Authors.Count == 0)
            {
                var defaultAuthor = Environment.GetEnvironmentVariable("NUGET_AUTHOR");
                if (string.IsNullOrEmpty(defaultAuthor))
                {
                    builder.Authors.Add(project.Name);
                }
                else
                {
                    builder.Authors.Add(defaultAuthor);
                }
            }

            builder.Description = project.Description ?? project.Name;
            builder.Id = project.Name;
            builder.Version = project.Version;
            builder.Title = project.Title;
            builder.Summary = project.Summary;
            builder.Copyright = project.Copyright;
            builder.RequireLicenseAcceptance = project.RequireLicenseAcceptance;
            builder.ReleaseNotes = project.ReleaseNotes;
            builder.Language = project.Language;
            builder.Tags.AddRange(project.Tags);

            if (!string.IsNullOrEmpty(project.IconUrl))
            {
                builder.IconUrl = new Uri(project.IconUrl);
            }

            if (!string.IsNullOrEmpty(project.ProjectUrl))
            {
                builder.ProjectUrl = new Uri(project.ProjectUrl);
            }

            if (!string.IsNullOrEmpty(project.LicenseUrl))
            {
                builder.LicenseUrl = new Uri(project.LicenseUrl);
            }

            return builder;
        }

        // REVIEW: This code copying kinda sucks
        private static string GetProjectOutputName(Project project, NuGetFramework framework, string configuration)
        {
            var compilationOptions = project.GetCompilerOptions(framework, configuration);
            var outputExtension = ".dll";

            if (framework.IsDesktop() && compilationOptions.EmitEntryPoint.GetValueOrDefault())
            {
                outputExtension = ".exe";
            }

            return project.Name + outputExtension;
        }

        private static string GetOutputPath(Project project, string configuration, string outputOptionValue)
        {
            var outputPath = string.Empty;

            if (string.IsNullOrEmpty(outputOptionValue))
            {
                outputPath = Path.Combine(
                    GetDefaultRootOutputPath(project, outputOptionValue),
                    Cli.Utils.Constants.BinDirectoryName,
                    configuration);
            }
            else
            {
                outputPath = outputOptionValue;
            }

            return outputPath;
        }

        private static string GetOutputPath(ProjectContext context, string configuration, string outputOptionValue)
        {
            var outputPath = string.Empty;

            if (string.IsNullOrEmpty(outputOptionValue))
            {
                outputPath = Path.Combine(
                    GetDefaultRootOutputPath(context.ProjectFile, outputOptionValue),
                    Cli.Utils.Constants.BinDirectoryName,
                    configuration,
                    context.TargetFramework.GetTwoDigitShortFolderName());
            }
            else
            {
                outputPath = outputOptionValue;
            }

            return outputPath;
        }

        private static string GetDefaultRootOutputPath(Project project, string outputOptionValue)
        {
            string rootOutputPath = string.Empty;

            if (string.IsNullOrEmpty(outputOptionValue))
            {
                rootOutputPath = project.ProjectDirectory;
            }

            return rootOutputPath;
        }
    }
}