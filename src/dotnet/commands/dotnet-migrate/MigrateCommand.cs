// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.Build.Construction;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.ProjectJsonMigration;

namespace Microsoft.DotNet.Tools.Migrate
{
    public partial class MigrateCommand
    {
        private string _templateFile; 
        private string _outputDirectory;
        private string _projectJson;
        private string _sdkVersion;

        private TemporaryDotnetNewTemplateProject _temporaryDotnetNewProject;

        public MigrateCommand(string templateFile, string outputDirectory, string projectJson, string sdkVersion)
        {
            _templateFile = templateFile;
            _outputDirectory = outputDirectory;
            _projectJson = projectJson;
            _sdkVersion = sdkVersion;

            _temporaryDotnetNewProject = new TemporaryDotnetNewTemplateProject();
        }

        public int Start()
        {
            var project = GetProjectJsonPath(_projectJson) ?? _temporaryDotnetNewProject.ProjectJsonPath;
            EnsureNotNull(project, "Unable to find project.json");
            var projectDirectory = Path.GetDirectoryName(project);

            var templateFile = _templateFile ?? _temporaryDotnetNewProject.MSBuildProjectPath;
            EnsureNotNull(templateFile, "Unable to find default msbuild template");

            var outputDirectory = _outputDirectory ?? Path.GetDirectoryName(project);
            EnsureNotNull(outputDirectory, "Null output directory");

            var sdkVersion = _sdkVersion ?? new ProjectJsonParser(_temporaryDotnetNewProject.ProjectJson).SdkPackageVersion;
            EnsureNotNull(sdkVersion, "Null Sdk Version");

            var migrationSettings = new MigrationSettings(projectDirectory, outputDirectory, sdkVersion, templateFile);
            new ProjectMigrator().Migrate(migrationSettings);
            return 0;
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
            if (projectJson == null)
            {
                return null;
            }

            if (File.Exists(projectJson))
            {
                return projectJson;
            }

            if (Directory.Exists(projectJson))
            {
                var projectCandidate = Path.Combine(projectJson, "project.json");

                if (File.Exists(projectCandidate))
                {
                    return projectCandidate;
                }
            }

            throw new Exception($"Unable to find project file at {projectJson}");
        }
    }
}
