// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;

using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Compilation;

namespace Microsoft.DotNet.Tools.Build
{
    // facade over the dependencies of a project context
    internal class ProjectDependenciesFacade
    {
        // projectName -> ProjectDescription
        public Dictionary<string, ProjectDescription> ProjectDependenciesWithSources { get; }
        public List<LibraryExport> Dependencies { get; }

        public ProjectDependenciesFacade(ProjectContext rootProject, string configValue)
        {
            Dependencies = GetProjectDependencies(rootProject, configValue);

            ProjectDependenciesWithSources = new Dictionary<string, ProjectDescription>();

            // Build project references
            foreach (var dependency in Dependencies)
            {
                var projectDependency = dependency.Library as ProjectDescription;

                if (projectDependency != null && projectDependency.Resolved && projectDependency.Project.Files.SourceFiles.Any())
                {
                    ProjectDependenciesWithSources[projectDependency.Identity.Name] = projectDependency;
                }
            }
        }

        // todo make extension of ProjectContext?
        private static List<LibraryExport> GetProjectDependencies(ProjectContext projectContext, string configuration)
        {
            // Create the library exporter
            var exporter = projectContext.CreateExporter(configuration);

            // Gather exports for the project
            var dependencies = exporter.GetDependencies().ToList();

            return dependencies;
        }
    }

}
