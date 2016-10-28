// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Construction;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectJsonMigration;
using Microsoft.DotNet.Internal.ProjectModel;

namespace Microsoft.DotNet.Tools.Migrate
{
    public partial class MigrateCommand
    {
        private readonly string _templateFile;
        private readonly string _projectArg;
        private readonly string _sdkVersion;
        private readonly string _xprojFilePath;
        private readonly bool _skipProjectReferences;
        private readonly string _reportFile;
        private readonly bool _reportFormatJson;

        private readonly TemporaryDotnetNewTemplateProject _temporaryDotnetNewProject;

        public MigrateCommand(
            string templateFile, 
            string projectArg, 
            string sdkVersion, 
            string xprojFilePath, 
            string reportFile, 
            bool skipProjectReferences, 
            bool reportFormatJson)
        {
            _templateFile = templateFile;
            _projectArg = projectArg ?? Directory.GetCurrentDirectory();
            _sdkVersion = sdkVersion;
            _xprojFilePath = xprojFilePath;
            _skipProjectReferences = skipProjectReferences;
            _temporaryDotnetNewProject = new TemporaryDotnetNewTemplateProject();
            _reportFile = reportFile;
            _reportFormatJson = reportFormatJson;
        }

        public int Execute()
        {
            var projectsToMigrate = GetProjectsToMigrate(_projectArg);

            var msBuildTemplate = _templateFile != null ?
                ProjectRootElement.TryOpen(_templateFile) : _temporaryDotnetNewProject.MSBuildProject;
            
            var sdkVersion = _sdkVersion ?? _temporaryDotnetNewProject.MSBuildProject.GetSdkVersion();

            EnsureNotNull(sdkVersion, "Null Sdk Version");

            MigrationReport migrationReport = null;

            foreach (var project in projectsToMigrate)
            {
                var projectDirectory = Path.GetDirectoryName(project);
                var outputDirectory = projectDirectory;
                var migrationSettings = new MigrationSettings(projectDirectory, outputDirectory, sdkVersion, msBuildTemplate, _xprojFilePath);
                var projectMigrationReport = new ProjectMigrator().Migrate(migrationSettings, _skipProjectReferences);

                if (migrationReport == null)
                {
                    migrationReport = projectMigrationReport;
                }
                else
                {
                    migrationReport = migrationReport.Merge(projectMigrationReport);
                }
            }

            WriteReport(migrationReport);

            return migrationReport.FailedProjectsCount;
        }

        private void WriteReport(MigrationReport migrationReport)
        {

            if (!string.IsNullOrEmpty(_reportFile))
            {
                using (var outputTextWriter = GetReportFileOutputTextWriter())
                {
                    outputTextWriter.Write(GetReportContent(migrationReport));
                }
            }

            WriteReportToStdOut(migrationReport);
        }

        private void WriteReportToStdOut(MigrationReport migrationReport)
        {
            StringBuilder sb = new StringBuilder();

            foreach (var projectMigrationReport in migrationReport.ProjectMigrationReports)
            {
                var errorContent = GetProjectReportErrorContent(projectMigrationReport, colored: true);
                var successContent = GetProjectReportSuccessContent(projectMigrationReport, colored: true);
                if (!string.IsNullOrEmpty(errorContent))
                {
                    Reporter.Error.WriteLine(errorContent);
                }
                else
                {
                    Reporter.Output.WriteLine(successContent);
                }
            }

            Reporter.Output.WriteLine(GetReportSummary(migrationReport));
        }

        private string GetReportContent(MigrationReport migrationReport, bool colored = false)
        {
            if (_reportFormatJson)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(migrationReport);
            }

            StringBuilder sb = new StringBuilder();

            foreach (var projectMigrationReport in migrationReport.ProjectMigrationReports)
            {
                var errorContent = GetProjectReportErrorContent(projectMigrationReport, colored: colored);
                var successContent = GetProjectReportSuccessContent(projectMigrationReport, colored: colored);
                if (!string.IsNullOrEmpty(errorContent))
                {
                    sb.AppendLine(errorContent);
                }
                else
                {
                    sb.AppendLine(successContent);
                }
            }

            sb.AppendLine(GetReportSummary(migrationReport));

            return sb.ToString();
        }

        private string GetReportSummary(MigrationReport migrationReport)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("Summary");
            sb.AppendLine($"Total Projects: {migrationReport.MigratedProjectsCount}");
            sb.AppendLine($"Succeeded Projects: {migrationReport.SucceededProjectsCount}");
            sb.AppendLine($"Failed Projects: {migrationReport.FailedProjectsCount}");

            return sb.ToString();
        }

        private string GetProjectReportSuccessContent(ProjectMigrationReport projectMigrationReport, bool colored)
        {
            Func<string, string> GreenIfColored = (str) => colored ? str.Green() : str;
            return GreenIfColored($"Project {projectMigrationReport.ProjectName} migration succeeded ({projectMigrationReport.ProjectDirectory})");
        }

        private string GetProjectReportErrorContent(ProjectMigrationReport projectMigrationReport, bool colored)
        {
            StringBuilder sb = new StringBuilder();
            Func<string, string> RedIfColored = (str) => colored ? str.Red() : str;

            if (projectMigrationReport.Errors.Any())
            {

                sb.AppendLine(RedIfColored($"Project {projectMigrationReport.ProjectName} migration failed ({projectMigrationReport.ProjectDirectory})"));

                foreach (var error in projectMigrationReport.Errors.Select(e => e.GetFormattedErrorMessage()))
                {
                    sb.AppendLine(RedIfColored(error));
                }
            }

            return sb.ToString();
        }

        private TextWriter GetReportFileOutputTextWriter()
        {
            return File.CreateText(_reportFile);
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
                if (!projects.Any())
                {
                    throw new Exception("Unable to find any projects in global.json");
                }
            }
            else if (Directory.Exists(projectArg))
            {
                projects = Directory.EnumerateFiles(projectArg, Project.FileName, SearchOption.AllDirectories);
                if (!projects.Any())
                {
                    throw new Exception($"No project.json file found in '{projectArg}'");
                }
            }
            else
            {
                throw new Exception($"Invalid project argument - '{projectArg}' is not a project.json or a global.json file and a directory named '{projectArg}' doesn't exist.");
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
