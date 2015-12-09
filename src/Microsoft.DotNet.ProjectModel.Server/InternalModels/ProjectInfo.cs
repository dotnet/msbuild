// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ProjectModel.Server.Helpers;
using Microsoft.DotNet.ProjectModel.Server.Models;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectModel.Server.InternalModels
{
    internal class ProjectInfo
    {
        public ProjectInfo(ProjectContext context,
                           string configuration,
                           IEnumerable<string> currentSearchPaths)
        {
            var allExports = context.CreateExporter(configuration).GetAllExports().ToList();
            var allDiagnostics = context.LibraryManager.GetAllDiagnostics();

            Context = context;
            Configuration = configuration;

            var allSourceFiles = new List<string>(context.ProjectFile.Files.SourceFiles);
            var allFileReferences = new List<string>();

            foreach (var export in allExports)
            {
                allSourceFiles.AddRange(export.SourceReferences);
                allFileReferences.AddRange(export.CompilationAssemblies.Select(asset => asset.ResolvedPath));
            }

            SourceFiles = allSourceFiles.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path).ToList();
            CompilationAssembiles = allFileReferences.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path).ToList();

            var allProjectReferences = new List<ProjectReferenceDescription>();

            var allDependencyDiagnostics = new List<DiagnosticMessage>();
            allDependencyDiagnostics.AddRange(context.LibraryManager.GetAllDiagnostics());
            allDependencyDiagnostics.AddRange(DependencyTypeChangeFinder.Diagnose(Context, currentSearchPaths));

            var diagnosticsLookup = allDependencyDiagnostics.ToLookup(d => d.Source);

            Dependencies = new Dictionary<string, DependencyDescription>();

            foreach (var library in context.LibraryManager.GetLibraries())
            {
                var diagnostics = diagnosticsLookup[library].ToList();
                var description = DependencyDescription.Create(library, diagnostics);
                Dependencies[description.Name] = description;

                if (library is ProjectDescription && library.Identity.Name != context.ProjectFile.Name)
                {
                    allProjectReferences.Add(ProjectReferenceDescription.Create((ProjectDescription)library));
                }
            }

            DependencyDiagnostics = allDependencyDiagnostics;
            ProjectReferences = allProjectReferences.OrderBy(reference => reference.Name).ToList();
        }

        public string Configuration { get; }

        public ProjectContext Context { get; }

        public string RootDependency => Context.ProjectFile.Name;

        public NuGetFramework Framework => Context.TargetFramework;

        public CommonCompilerOptions CompilerOptions => Context.ProjectFile.GetCompilerOptions(Framework, Configuration);

        public IReadOnlyList<string> SourceFiles { get; }

        public IReadOnlyList<string> CompilationAssembiles { get; }

        public IReadOnlyList<ProjectReferenceDescription> ProjectReferences { get; }

        public IReadOnlyList<DiagnosticMessage> DependencyDiagnostics { get; }

        public Dictionary<string, DependencyDescription> Dependencies { get; }
    }
}
