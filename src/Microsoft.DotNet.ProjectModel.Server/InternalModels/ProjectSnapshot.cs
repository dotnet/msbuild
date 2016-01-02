// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ProjectModel.Server.Helpers;
using Microsoft.DotNet.ProjectModel.Server.Models;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectModel.Server
{
    internal class ProjectSnapshot
    {
        public Project Project { get; set; }
        public string GlobalJsonPath { get; set; }
        public IReadOnlyList<string> ProjectSearchPaths { get; set; }
        public IReadOnlyList<DiagnosticMessage> ProjectDiagnostics { get; set; }
        public ErrorMessage GlobalErrorMessage { get; set; }
        public Dictionary<NuGetFramework, ProjectContextSnapshot> ProjectContexts { get; } = new Dictionary<NuGetFramework, ProjectContextSnapshot>();

        public static ProjectSnapshot Create(string projectDirectory, string configuration, WorkspaceContext workspaceContext, IReadOnlyList<string> projectSearchPaths)
        {
            var projectContextsCollection = workspaceContext.GetProjectContextCollection(projectDirectory);
            if (!projectContextsCollection.ProjectContexts.Any())
            {
                throw new InvalidOperationException($"Unable to find project.json in '{projectDirectory}'");
            }
            GlobalSettings globalSettings;
            var currentSearchPaths = projectContextsCollection.Project.ResolveSearchPaths(out globalSettings);

            var snapshot = new ProjectSnapshot();
            snapshot.Project = projectContextsCollection.Project;
            snapshot.ProjectDiagnostics = new List<DiagnosticMessage>(projectContextsCollection.ProjectDiagnostics);
            snapshot.ProjectSearchPaths = currentSearchPaths.ToList();
            snapshot.GlobalJsonPath = globalSettings?.FilePath;

            foreach (var projectContext in projectContextsCollection.ProjectContexts)
            {
                snapshot.ProjectContexts[projectContext.TargetFramework] = 
                    ProjectContextSnapshot.Create(projectContext, configuration, currentSearchPaths);
            }
            
            return snapshot;
        }
    }
}
