// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ProjectModel.Server.Helpers;
using Microsoft.DotNet.ProjectModel.Server.InternalModels;
using Microsoft.DotNet.ProjectModel.Server.Models;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectModel.Server
{
    internal class Snapshot
    {
        public static Snapshot CreateFromProject(Project project)
        {
            GlobalSettings globalSettings;
            var projectSearchPaths = project.ResolveSearchPaths(out globalSettings);

            return new Snapshot(project, globalSettings?.FilePath, projectSearchPaths);
        }

        public Snapshot()
        {
            Projects = new Dictionary<NuGetFramework, ProjectSnapshot>();
        }

        public Snapshot(Project project, string globalJsonPath, IEnumerable<string> projectSearchPaths)
            : this()
        {
            Project = project;
            GlobalJsonPath = globalJsonPath;
            ProjectSearchPaths = projectSearchPaths.ToList();
        }

        public Project Project { get; set; }
        public string GlobalJsonPath { get; set; }
        public IReadOnlyList<string> ProjectSearchPaths { get; set; }
        public IReadOnlyList<DiagnosticMessage> ProjectDiagnostics { get; set; }
        public ErrorMessage GlobalErrorMessage { get; set; }
        public Dictionary<NuGetFramework, ProjectSnapshot> Projects { get; }
    }
}
