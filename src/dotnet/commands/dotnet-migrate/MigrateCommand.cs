// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectJsonMigration;
using Microsoft.DotNet.Internal.ProjectModel;
using Project = Microsoft.DotNet.Internal.ProjectModel.Project;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.Tools.Migrate
{
    public partial class MigrateCommand
    {
        private readonly DirectoryInfo _workspaceDirectory;
        private readonly DirectoryInfo _backupDirectory;
        private readonly string _templateFile;
        private readonly string _projectArg;
        private readonly string _sdkVersion;
        private readonly string _xprojFilePath;
        private readonly bool _skipProjectReferences;
        private readonly string _reportFile;
        private readonly bool _reportFormatJson;
        private readonly bool _skipBackup;

        public MigrateCommand(
            string templateFile, 
            string projectArg, 
            string sdkVersion, 
            string xprojFilePath, 
            string reportFile, 
            bool skipProjectReferences, 
            bool reportFormatJson,
            bool skipBackup)
        {            
            _templateFile = templateFile;
            _projectArg = projectArg ?? Directory.GetCurrentDirectory();
            _workspaceDirectory = File.Exists(_projectArg)
                ? new FileInfo(_projectArg).Directory
                : new DirectoryInfo(_projectArg);
            _backupDirectory = new DirectoryInfo(Path.Combine(_workspaceDirectory.FullName, "backup"));
            _sdkVersion = sdkVersion;
            _xprojFilePath = xprojFilePath;
            _skipProjectReferences = skipProjectReferences;
            _reportFile = reportFile;
            _reportFormatJson = reportFormatJson;
            _skipBackup = skipBackup;
        }

        public int Execute()
        {
            var temporaryDotnetNewProject = new TemporaryDotnetNewTemplateProject();
            var projectsToMigrate = GetProjectsToMigrate(_projectArg);

            var msBuildTemplatePath = _templateFile ?? temporaryDotnetNewProject.MSBuildProjectPath;
            
            var sdkVersion = _sdkVersion ?? temporaryDotnetNewProject.MSBuildProject.GetSdkVersion();

            EnsureNotNull(sdkVersion, "Null Sdk Version");

            MigrationReport migrationReport = null;

            foreach (var project in projectsToMigrate)
            {
                var projectDirectory = Path.GetDirectoryName(project);
                var outputDirectory = projectDirectory;
                var migrationSettings = new MigrationSettings(
                    projectDirectory,
                    outputDirectory,
                    sdkVersion,
                    msBuildTemplatePath,
                    _xprojFilePath);
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

            temporaryDotnetNewProject.Clean();

            MoveProjectJsonArtifactsToBackup(migrationReport);

            return migrationReport.FailedProjectsCount;
        }

        private void MoveProjectJsonArtifactsToBackup(MigrationReport migrationReport)
        {
            if (_skipBackup)
            {
                return;
            }
            
            if (migrationReport.FailedProjectsCount > 0)
            {
                return;
            }
            
            BackupGlobalJson();

            BackupProjects(migrationReport);
            
        }

        private void BackupGlobalJson()
        {   
            _backupDirectory.Create();

            var globalJson = Path.Combine(_workspaceDirectory.FullName, GlobalSettings.FileName);

            if (File.Exists(globalJson))
            {
                File.Move(globalJson, Path.Combine(_backupDirectory.FullName, GlobalSettings.FileName));
            }
        }
        
        private void BackupProjects(MigrationReport migrationReport)
        {
            foreach (var report in migrationReport.ProjectMigrationReports)
            {
                MigrateProject(report);
            }
        }

        private void MigrateProject(ProjectMigrationReport report)
        {
            var projectDirectory = PathUtility.EnsureTrailingSlash(report.ProjectDirectory);
            
            var relativeDirectory = PathUtility.GetRelativePath(PathUtility.EnsureTrailingSlash(_workspaceDirectory.FullName), projectDirectory);

            var targetDirectory = String.IsNullOrEmpty(relativeDirectory)
                ? _backupDirectory.FullName
                :  Path.Combine(_backupDirectory.FullName, relativeDirectory);

            PathUtility.EnsureDirectory(PathUtility.EnsureTrailingSlash(targetDirectory));

            var movableFiles = new DirectoryInfo(projectDirectory)
                .EnumerateFiles()
                .Where(f => f.Name == Project.FileName || f.Extension == ".xproj");
            
            foreach (var movableFile in movableFiles)
            {
                movableFile.MoveTo(Path.Combine(targetDirectory, movableFile.Name));
            }
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
