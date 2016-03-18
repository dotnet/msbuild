// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Files;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.DotNet.ProjectModel.Utilities;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using NuGet;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Microsoft.DotNet.Cli.Compiler.Common;
using Microsoft.DotNet.ProjectModel.Resources;
using Microsoft.DotNet.Tools.Pack;
using PackageBuilder = NuGet.PackageBuilder;

namespace Microsoft.DotNet.Tools.Compiler
{
    public class PackageGenerator
    {
        protected ArtifactPathsCalculator ArtifactPathsCalculator { get; }
        
        protected Project Project { get; }

        protected string Configuration { get; }

        protected PackageBuilder PackageBuilder { get; private set; }

        public PackageGenerator(Project project, string configuration, ArtifactPathsCalculator artifactPathsCalculator)
        {
            ArtifactPathsCalculator = artifactPathsCalculator;
            Project = project;
            Configuration = configuration;
        }        

        public bool BuildPackage(IEnumerable<ProjectContext> contexts, List<DiagnosticMessage> packDiagnostics)
        {

            Reporter.Output.WriteLine($"Producing nuget package \"{GetPackageName()}\" for {Project.Name}");

            PackageBuilder = CreatePackageBuilder(Project);

            // TODO: Report errors for required fields
            // id
            // author
            // description
            foreach (var context in contexts)
            {
                Reporter.Verbose.WriteLine($"Processing {context.TargetFramework.ToString().Yellow()}");
                ProcessContext(context);
                Reporter.Verbose.WriteLine("");
            }

            var packageOutputPath = Path.Combine(
                ArtifactPathsCalculator.PackageOutputPath,
                GetPackageName() + NuGet.Constants.PackageExtension);

            if (GeneratePackage(packageOutputPath, packDiagnostics))
            {
                return true;
            }

            return false;
        }

        protected virtual void ProcessContext(ProjectContext context)
        {
            PopulateDependencies(context);

            var inputFolder = ArtifactPathsCalculator.InputPathForContext(context);
            var outputName = GetProjectOutputName(context.TargetFramework);

            var resourceCultures = context.ProjectFile.Files.ResourceFiles
                    .Select(resourceFile => ResourceUtility.GetResourceCultureName(resourceFile.Key))
                    .Distinct();

            foreach (var culture in resourceCultures)
            {
                if (string.IsNullOrEmpty(culture))
                {
                    continue;
                }

                var resourceFilePath = Path.Combine(culture, $"{Project.Name}.resources.dll");
                TryAddOutputFile(context, inputFolder, resourceFilePath);
            }

            TryAddOutputFile(context, inputFolder, outputName);
            TryAddOutputFile(context, inputFolder, $"{Project.Name}.xml");
            TryAddOutputFile(context, inputFolder, $"{Project.Name}.runtimeconfig.json");
        }

        protected virtual bool GeneratePackage(string nupkg, List<DiagnosticMessage> packDiagnostics)
        {
            foreach (var sharedFile in Project.Files.SharedFiles)
            {
                var file = new PhysicalPackageFile();
                file.SourcePath = sharedFile;
                file.TargetPath = Path.Combine("shared", Path.GetFileName(sharedFile));
                PackageBuilder.Files.Add(file);
            }

            if (Project.Files.PackInclude != null && Project.Files.PackInclude.Any())
            {
                AddPackageFiles(Project.Files.PackInclude, packDiagnostics);
            }

            // Write the packages as long as we're still in a success state.
            if (!packDiagnostics.Any(d => d.Severity == DiagnosticMessageSeverity.Error))
            {
                Reporter.Verbose.WriteLine($"Adding package files");
                foreach (var file in PackageBuilder.Files.OfType<PhysicalPackageFile>())
                {
                    if (file.SourcePath != null && File.Exists(file.SourcePath))
                    {
                        Reporter.Verbose.WriteLine($"Adding {file.Path.Yellow()}");
                    }
                }

                Directory.CreateDirectory(Path.GetDirectoryName(nupkg));

                using (var fs = File.Create(nupkg))
                {
                    PackageBuilder.Save(fs);
                    Reporter.Output.WriteLine($"{Project.Name} -> {Path.GetFullPath(nupkg)}");
                }

                return true;
            }

            return false;
        }

        private void AddPackageFiles(IEnumerable<PackIncludeEntry> packageFiles, IList<DiagnosticMessage> diagnostics)
        {
            var rootDirectory = new DirectoryInfoWrapper(new DirectoryInfo(Project.ProjectDirectory));

            foreach (var match in CollectAdditionalFiles(rootDirectory, packageFiles, Project.ProjectFilePath, diagnostics))
            {
                PackageBuilder.Files.Add(match);
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

        protected void TryAddOutputFile(ProjectContext context,
            string outputPath,
            string filePath)
        {
            var targetPath = Path.Combine("lib", context.TargetFramework.GetTwoDigitShortFolderName(), filePath);
            var sourcePath = Path.Combine(outputPath, filePath);

            if (!File.Exists(sourcePath))
            {
                return;
            }

            PackageBuilder.Files.Add(new PhysicalPackageFile
            {
                SourcePath = sourcePath,
                TargetPath = targetPath
            });
        }

        private void PopulateDependencies(ProjectContext context)
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
                    PackageBuilder.FrameworkAssemblies.Add(new FrameworkAssemblyReference(dependency.Name, new[] { context.TargetFramework }));

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

            PackageBuilder.DependencySets.Add(new PackageDependencySet(context.TargetFramework, dependencies));
        }

        protected virtual string GetPackageName()
        {
            return $"{Project.Name}.{Project.Version}";
        }

        private string GetProjectOutputName(NuGetFramework framework)
        {
            var compilationOptions = Project.GetCompilerOptions(framework, Configuration);
            var outputExtension = ".dll";

            if (framework.IsDesktop() && compilationOptions.EmitEntryPoint.GetValueOrDefault())
            {
                outputExtension = ".exe";
            }

            return Project.Name + outputExtension;
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

    }
}
