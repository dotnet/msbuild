// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
namespace Microsoft.DotNet.ProjectJsonMigration
{
    internal class ProjectMigrationReport
    {
        public string ProjectDirectory { get; }

        public string ProjectName { get; }

        public string OutputMSBuildProject { get; }

        public List<MigrationError> Errors { get; }

        public List<string> Warnings { get; }

        public bool Skipped { get; }

        public bool Failed => Errors.Any();
        
        public bool Succeeded => !Errors.Any();

        public ProjectMigrationReport(string projectDirectory, string projectName, bool skipped)
            : this(projectDirectory, projectName, null, null, null, skipped: skipped) { }

        public ProjectMigrationReport(string projectDirectory, string projectName, List<MigrationError> errors, List<string> warnings)
            : this(projectDirectory, projectName, null, errors, warnings) { }

        public ProjectMigrationReport(string projectDirectory, string projectName, string outputMSBuildProject, List<string> warnings)
            : this(projectDirectory, projectName, outputMSBuildProject, null, warnings) { }

        private ProjectMigrationReport(string projectDirectory, string projectName, string outputMSBuildProject, List<MigrationError> errors, List<string> warnings, bool skipped=false)
        {
            ProjectDirectory = projectDirectory;
            ProjectName = projectName;
            OutputMSBuildProject = outputMSBuildProject;
            Errors = errors ?? new List<MigrationError>();
            Warnings = warnings ?? new List<string>();
            Skipped=skipped;
        }
    }
}
