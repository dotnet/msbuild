// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli.Compiler.Common;
using Microsoft.DotNet.ProjectModel.Files;
using Microsoft.DotNet.ProjectModel.Server.Helpers;
using Microsoft.DotNet.ProjectModel.Server.Models;
using NuGet.Frameworks;
using NuGet.LibraryModel;

namespace Microsoft.DotNet.ProjectModel.Server
{
    internal class ProjectContextSnapshot
    {
        public string RootDependency { get; set; }
        public NuGetFramework TargetFramework { get; set; }
        public IReadOnlyList<string> SourceFiles { get; set; }
        public CommonCompilerOptions CompilerOptions { get; set; }
        public IReadOnlyList<ProjectReferenceDescription> ProjectReferences { get; set; }
        public IReadOnlyList<string> FileReferences { get; set; }
        public IReadOnlyList<DiagnosticMessage> DependencyDiagnostics { get; set; }
        public IDictionary<string, DependencyDescription> Dependencies { get; set; }

        public static ProjectContextSnapshot Create(ProjectContext context, string configuration, IEnumerable<string> previousSearchPaths)
        {
            var snapshot = new ProjectContextSnapshot();

            var allDependencyDiagnostics = new List<DiagnosticMessage>();
            allDependencyDiagnostics.AddRange(context.LibraryManager.GetAllDiagnostics());
            allDependencyDiagnostics.AddRange(DependencyTypeChangeFinder.Diagnose(context, previousSearchPaths));

            var diagnosticsLookup = allDependencyDiagnostics.ToLookup(d => d.Source);

            var allExports = context.CreateExporter(configuration)
                                    .GetAllExports()
                                    .ToDictionary(export => export.Library.Identity.Name);

            var allSourceFiles = new List<string>(GetSourceFiles(context, configuration));
            var allFileReferences = new List<string>();
            var allProjectReferences = new List<ProjectReferenceDescription>();
            var allDependencies = new Dictionary<string, DependencyDescription>();

            // All exports are returned. When the same library name have a ReferenceAssembly type export and a Package type export
            // both will be listed as dependencies. Prefix "fx/" will be added to ReferenceAssembly type dependency.
            foreach (var export in allExports.Values)
            {
                allSourceFiles.AddRange(export.SourceReferences.Select(f => f.ResolvedPath));
                var diagnostics = diagnosticsLookup[export.Library].ToList();
                var description = DependencyDescription.Create(export.Library, diagnostics, allExports);
                allDependencies[description.Name] = description;

                var projectReferene = ProjectReferenceDescription.Create(export.Library);
                if (projectReferene != null && export.Library.Identity.Name != context.ProjectFile.Name)
                {
                    allProjectReferences.Add(projectReferene);
                }
                
                if (export.Library.Identity.Type != LibraryType.Project)
                {
                    allFileReferences.AddRange(export.CompilationAssemblies.Select(asset => asset.ResolvedPath));
                }
            }

            snapshot.RootDependency = context.ProjectFile.Name;
            snapshot.TargetFramework = context.TargetFramework;
            snapshot.SourceFiles = allSourceFiles.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path).ToList();
            snapshot.CompilerOptions = context.GetLanguageSpecificCompilerOptions(context.TargetFramework, configuration);
            snapshot.ProjectReferences = allProjectReferences.OrderBy(reference => reference.Name).ToList();
            snapshot.FileReferences = allFileReferences.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path).ToList();
            snapshot.DependencyDiagnostics = allDependencyDiagnostics;
            snapshot.Dependencies = allDependencies;

            return snapshot;
        }

        private static IEnumerable<string> GetSourceFiles(ProjectContext context, string configuration)
        {
            var compilerOptions = context.ProjectFile.GetCompilerOptions(context.TargetFramework, configuration);

            if (compilerOptions.CompileInclude == null)
            {
                return context.ProjectFile.Files.SourceFiles;
            }

            var includeFiles = IncludeFilesResolver.GetIncludeFiles(compilerOptions.CompileInclude, "/", diagnostics: null);

            return includeFiles.Select(f => f.SourcePath);
        }
    }
}
