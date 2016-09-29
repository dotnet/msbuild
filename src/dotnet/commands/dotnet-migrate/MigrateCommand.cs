// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Construction;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.ProjectJsonMigration;
using Microsoft.DotNet.ProjectModel;

namespace Microsoft.DotNet.Tools.Migrate
{
    public partial class MigrateCommand
    {
        private readonly string _templateFile;
        private readonly string _projectArg;
        private readonly string _sdkVersion;
        private readonly string _xprojFilePath;
        private readonly bool _skipProjectReferences;

        private readonly TemporaryDotnetNewTemplateProject _temporaryDotnetNewProject;

        public MigrateCommand(string templateFile, string projectArg, string sdkVersion, string xprojFilePath, bool skipProjectReferences)
        {
            _templateFile = templateFile;
            _projectArg = projectArg ?? Directory.GetCurrentDirectory();
            _sdkVersion = sdkVersion;
            _xprojFilePath = xprojFilePath;
            _skipProjectReferences = skipProjectReferences;
            _temporaryDotnetNewProject = new TemporaryDotnetNewTemplateProject();
        }

        public int Execute()
        {
            var projectsToMigrate = GetProjectsToMigrate(_projectArg);

            var msBuildTemplate = _templateFile != null ?
                ProjectRootElement.TryOpen(_templateFile) : _temporaryDotnetNewProject.MSBuildProject;

            var sdkVersion = _sdkVersion ?? new ProjectJsonParser(_temporaryDotnetNewProject.ProjectJson).SdkPackageVersion;
            EnsureNotNull(sdkVersion, "Null Sdk Version");

            foreach (var project in projectsToMigrate)
            {
                Console.WriteLine($"Migrating project {project}..");
                var projectDirectory = Path.GetDirectoryName(project);
                var outputDirectory = projectDirectory;
                var migrationSettings = new MigrationSettings(projectDirectory, outputDirectory, sdkVersion, msBuildTemplate, _xprojFilePath);
                new ProjectMigrator().Migrate(migrationSettings, _skipProjectReferences);
            }

            return 0;
        }

        private IEnumerable<string> GetProjectsToMigrate(string projectArg)
        {
            if (projectArg.EndsWith(Project.FileName))
            {
                yield return GetProjectJsonPath(projectArg);
            }
            else if (Directory.Exists(projectArg))
            {
                var projects = Directory.EnumerateFiles(projectArg, Project.FileName, SearchOption.AllDirectories);

                foreach(var project in projects)
                {
                    yield return GetProjectJsonPath(project);
                }
            }
            else
            {
                throw new Exception($"Invalid project argument - '{projectArg}' is not a project.json file and a directory named '{projectArg}' doesn't exist.");
            }
        }

        private void EnsureNotNull(string variable, string message)
        {
            if (variable == null)
            {
                throw new Exception(message);
            }
        }

        private string GetProjectJsonPath(string projectJson)
        {
            projectJson = ProjectPathHelper.NormalizeProjectFilePath(projectJson);

            if (File.Exists(projectJson))
            {
                return projectJson;
            }

            throw new Exception($"Unable to find project file at {projectJson}");
        }
    }
}
