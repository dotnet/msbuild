// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;
using Microsoft.DotNet.ProjectModel;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectModel.Workspaces
{
    public class ProjectJsonWorkspace : Workspace
    {
        private Dictionary<string, AssemblyMetadata> _cache = new Dictionary<string, AssemblyMetadata>();

        private readonly string[] _projectPaths;

        public ProjectJsonWorkspace(string projectPath) : this(new[] { projectPath })
        {
        }

        public ProjectJsonWorkspace(string[] projectPaths) : base(MefHostServices.DefaultHost, "Custom")
        {
            _projectPaths = projectPaths;

            Initialize();
        }

        private void Initialize()
        {
            foreach (var projectPath in _projectPaths)
            {
                AddProject(projectPath);
            }
        }

        private void AddProject(string projectPath)
        {
            // Get all of the specific projects (there is a project per framework)
            foreach (var p in ProjectContext.CreateContextForEachFramework(projectPath))
            {
                AddProject(p);
            }
        }

        private ProjectId AddProject(ProjectContext project)
        {
            // Create the framework specific project and add it to the workspace
            var projectInfo = ProjectInfo.Create(
                                ProjectId.CreateNewId(),
                                VersionStamp.Create(),
                                project.ProjectFile.Name + "+" + project.TargetFramework,
                                project.ProjectFile.Name,
                                LanguageNames.CSharp,
                                project.ProjectFile.ProjectFilePath);

            OnProjectAdded(projectInfo);

            // TODO: ctor argument?
            var configuration = "Debug";

            var compilationOptions = project.ProjectFile.GetCompilerOptions(project.TargetFramework, configuration);

            var compilationSettings = ToCompilationSettings(compilationOptions, project.TargetFramework, project.ProjectFile.ProjectDirectory);

            OnParseOptionsChanged(projectInfo.Id, new CSharpParseOptions(compilationSettings.LanguageVersion, preprocessorSymbols: compilationSettings.Defines));

            OnCompilationOptionsChanged(projectInfo.Id, compilationSettings.CompilationOptions);

            foreach (var file in project.ProjectFile.Files.SourceFiles)
            {
                AddSourceFile(projectInfo, file);
            }

            var exporter = project.CreateExporter(configuration);

            foreach (var dependency in exporter.GetDependencies())
            {
                var projectDependency = dependency.Library as ProjectDescription;
                if (projectDependency != null)
                {
                    var projectDependencyContext = ProjectContext.Create(projectDependency.Project.ProjectFilePath, projectDependency.Framework);

                    var id = AddProject(projectDependencyContext);

                    OnProjectReferenceAdded(projectInfo.Id, new ProjectReference(id));
                }
                else
                {
                    foreach (var asset in dependency.CompilationAssemblies)
                    {
                        OnMetadataReferenceAdded(projectInfo.Id, GetMetadataReference(asset.ResolvedPath));
                    }
                }

                foreach (var file in dependency.SourceReferences)
                {
                    AddSourceFile(projectInfo, file);
                }
            }

            return projectInfo.Id;
        }

        private void AddSourceFile(ProjectInfo projectInfo, string file)
        {
            using (var stream = File.OpenRead(file))
            {
                var sourceText = SourceText.From(stream, encoding: Encoding.UTF8);
                var id = DocumentId.CreateNewId(projectInfo.Id);
                var version = VersionStamp.Create();

                var loader = TextLoader.From(TextAndVersion.Create(sourceText, version));
                OnDocumentAdded(DocumentInfo.Create(id, file, filePath: file, loader: loader));
            }
        }

        private MetadataReference GetMetadataReference(string path)
        {
            AssemblyMetadata assemblyMetadata;
            if (!_cache.TryGetValue(path, out assemblyMetadata))
            {
                using (var stream = File.OpenRead(path))
                {
                    var moduleMetadata = ModuleMetadata.CreateFromStream(stream, PEStreamOptions.PrefetchMetadata);
                    assemblyMetadata = AssemblyMetadata.Create(moduleMetadata);
                    _cache[path] = assemblyMetadata;
                }
            }
            
            return assemblyMetadata.GetReference();
        }

        private static CompilationSettings ToCompilationSettings(CommonCompilerOptions compilerOptions,
                                                                 NuGetFramework targetFramework,
                                                                 string projectDirectory)
        {
            var options = GetCompilationOptions(compilerOptions, projectDirectory);

            // Disable 1702 until roslyn turns this off by default
            options = options.WithSpecificDiagnosticOptions(new Dictionary<string, ReportDiagnostic>
            {
                { "CS1701", ReportDiagnostic.Suppress }, // Binding redirects
                { "CS1702", ReportDiagnostic.Suppress },
                { "CS1705", ReportDiagnostic.Suppress }
            });

            AssemblyIdentityComparer assemblyIdentityComparer =
                targetFramework.IsDesktop() ?
                DesktopAssemblyIdentityComparer.Default :
                null;

            options = options.WithAssemblyIdentityComparer(assemblyIdentityComparer);

            LanguageVersion languageVersion;
            if (!Enum.TryParse<LanguageVersion>(value: compilerOptions.LanguageVersion,
                                                ignoreCase: true,
                                                result: out languageVersion))
            {
                languageVersion = LanguageVersion.CSharp6;
            }

            var settings = new CompilationSettings
            {
                LanguageVersion = languageVersion,
                Defines = compilerOptions.Defines ?? Enumerable.Empty<string>(),
                CompilationOptions = options
            };

            return settings;
        }

        private static CSharpCompilationOptions GetCompilationOptions(CommonCompilerOptions compilerOptions, string projectDirectory)
        {
            var outputKind = compilerOptions.EmitEntryPoint.GetValueOrDefault() ?
                OutputKind.ConsoleApplication : OutputKind.DynamicallyLinkedLibrary;
            var options = new CSharpCompilationOptions(outputKind);

            string platformValue = compilerOptions.Platform;
            bool allowUnsafe = compilerOptions.AllowUnsafe ?? false;
            bool optimize = compilerOptions.Optimize ?? false;
            bool warningsAsErrors = compilerOptions.WarningsAsErrors ?? false;

            Platform platform;
            if (!Enum.TryParse(value: platformValue, ignoreCase: true, result: out platform))
            {
                platform = Platform.AnyCpu;
            }

            options = options
                        .WithAllowUnsafe(allowUnsafe)
                        .WithPlatform(platform)
                        .WithGeneralDiagnosticOption(warningsAsErrors ? ReportDiagnostic.Error : ReportDiagnostic.Default)
                        .WithOptimizationLevel(optimize ? OptimizationLevel.Release : OptimizationLevel.Debug);

            return AddSigningOptions(options, compilerOptions, projectDirectory);
        }

        private static CSharpCompilationOptions AddSigningOptions(CSharpCompilationOptions options, CommonCompilerOptions compilerOptions, string projectDirectory)
        {
            var useOssSigning = compilerOptions.PublicSign == true;
            var keyFile = compilerOptions.KeyFile;

            if (!string.IsNullOrEmpty(keyFile))
            {
                keyFile = Path.GetFullPath(Path.Combine(projectDirectory, compilerOptions.KeyFile));

                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || useOssSigning)
                {
                    return options.WithCryptoPublicKey(
                        SnkUtils.ExtractPublicKey(File.ReadAllBytes(keyFile)));
                }

                options = options.WithCryptoKeyFile(keyFile);

                return options.WithDelaySign(compilerOptions.DelaySign);
            }

            return options;
        }

        private class CompilationSettings
        {
            public LanguageVersion LanguageVersion { get; set; }
            public IEnumerable<string> Defines { get; set; }
            public CSharpCompilationOptions CompilationOptions { get; set; }
        }
    }
}