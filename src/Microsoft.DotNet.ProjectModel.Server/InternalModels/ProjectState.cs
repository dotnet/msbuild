// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Collections.Generic;
using System;

namespace Microsoft.DotNet.ProjectModel.Server.InternalModels
{
    internal class ProjectState
    {
        public static ProjectState Create(string appPath, 
                                          string configuration,
                                          WorkspaceContext workspaceContext,
                                          IEnumerable<string> currentSearchPaths)
        {
            var projectContextsCollection = workspaceContext.GetProjectContextCollection(appPath);
            if (!projectContextsCollection.ProjectContexts.Any())
            {
                throw new InvalidOperationException($"Unable to find project.json in '{appPath}'");
            }

            var project = projectContextsCollection.ProjectContexts.First().ProjectFile;
            var projectDiagnostics = new List<DiagnosticMessage>(projectContextsCollection.ProjectDiagnostics);
            var projectInfos = new List<ProjectInfo>();

            foreach (var projectContext in projectContextsCollection.ProjectContexts)
            {
                projectInfos.Add(new ProjectInfo(
                    projectContext,
                    configuration,
                    currentSearchPaths));
            }

            return new ProjectState(project, projectDiagnostics, projectInfos);
        }

        private ProjectState(Project project, List<DiagnosticMessage> projectDiagnostics, List<ProjectInfo> projectInfos)
        {
            Project = project;
            Projects = projectInfos;
            Diagnostics = projectDiagnostics;
        }

        public Project Project { get; }

        public IReadOnlyList<ProjectInfo> Projects { get; }

        public IReadOnlyList<DiagnosticMessage> Diagnostics { get; }
    }
}
