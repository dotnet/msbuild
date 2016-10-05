// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Construction;
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
            
            var sdkVersion = _sdkVersion ?? _temporaryDotnetNewProject.MSBuildProject.GetSdkVersion();

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
            IEnumerable<string> projects = null;

            if (projectArg.EndsWith(Project.FileName, StringComparison.OrdinalIgnoreCase))
            {
                projects = Enumerable.Repeat(projectArg, 1);
            }
            else if (projectArg.EndsWith(GlobalSettings.FileName, StringComparison.OrdinalIgnoreCase))
            {
                projects =  GetProjectsFromGlobalJson(projectArg);
            }
            else if (Directory.Exists(projectArg))
            {
                projects = Directory.EnumerateFiles(projectArg, Project.FileName, SearchOption.AllDirectories);
            }
            else
            {
                throw new Exception($"Invalid project argument - '{projectArg}' is not a project.json or a global.json file and a directory named '{projectArg}' doesn't exist.");
            }

            if (!projects.Any())
            {
                throw new Exception($"Invalid project argument - Unable to find any projects in global.json or directory '{projectArg}'");
            }

            foreach(var project in projects)
            {
                yield return GetProjectJsonPath(project);
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

        private IEnumerable<string> GetProjectsFromGlobalJson(string globalJson)
        {
            if (!File.Exists(globalJson))
            {
                throw new Exception($"Unable to find global settings file at {globalJson}");
            }

            var searchPaths = ProjectDependencyFinder.GetGlobalPaths(Path.GetDirectoryName(globalJson));

            foreach (var searchPath in searchPaths)
            {
                var directory = new DirectoryInfo(searchPath);

                if (!directory.Exists)
                {
                    continue;
                }

                foreach (var projectDirectory in directory.EnumerateDirectories())
                {
                    var projectFilePath = Path.Combine(projectDirectory.FullName, "project.json");

                    if (File.Exists(projectFilePath))
                    {
                        yield return projectFilePath;
                    }
                }
            }
        }
    }
}
