// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectJsonMigration;
using Microsoft.DotNet.Internal.ProjectModel;
using Project = Microsoft.DotNet.Internal.ProjectModel.Project;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.Tools.Migrate
{
    public partial class MigrateCommand
    {
        private const string ProductDescription = "Visual Studio 15";
        private const string VisualStudioVersion = "15.0.26114.2";
        private const string MinimumVisualStudioVersion = "10.0.40219.1";

        private SlnFile _slnFile;
        private readonly DirectoryInfo _workspaceDirectory;
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

            MigrationReport migrationReport = null;

            foreach (var project in projectsToMigrate)
            {
                var projectDirectory = Path.GetDirectoryName(project);
                var outputDirectory = projectDirectory;
                var migrationSettings = new MigrationSettings(
                    projectDirectory,
                    outputDirectory,
                    msBuildTemplatePath,
                    _xprojFilePath,
                    null,
                    _slnFile);
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

            UpdateSolutionFile(migrationReport);

            MoveProjectJsonArtifactsToBackup(migrationReport);

            return migrationReport.FailedProjectsCount;
        }

        private void UpdateSolutionFile(MigrationReport migrationReport)
        {
            if (_slnFile == null)
            {
                return;
            }

            if (migrationReport.FailedProjectsCount > 0)
            {
                return;
            }

            List<string> csprojFilesToAdd = new List<string>();

            var slnPathWithTrailingSlash = PathUtility.EnsureTrailingSlash(_slnFile.BaseDirectory);
            foreach (var report in migrationReport.ProjectMigrationReports)
            {
                var reportPathWithTrailingSlash = PathUtility.EnsureTrailingSlash(report.ProjectDirectory);
                var reportRelPath = Path.Combine(
                    PathUtility.GetRelativePath(slnPathWithTrailingSlash, reportPathWithTrailingSlash),
                    report.ProjectName + ".xproj");

                var projects = _slnFile.Projects.Where(p => p.FilePath == reportRelPath);

                var migratedProjectName = report.ProjectName + ".csproj";
                if (projects.Count() == 1)
                {
                    var slnProject = projects.Single();
                    slnProject.FilePath = Path.Combine(
                        Path.GetDirectoryName(slnProject.FilePath),
                        migratedProjectName);
                    slnProject.TypeGuid = ProjectTypeGuids.CSharpProjectTypeGuid;
                }
                else
                {
                    csprojFilesToAdd.Add(Path.Combine(report.ProjectDirectory, migratedProjectName));
                }

                foreach (var preExisting in report.PreExistingCsprojDependencies)
                {
                    csprojFilesToAdd.Add(Path.Combine(report.ProjectDirectory, preExisting));
                }
            }

            Version version;
            if (!Version.TryParse(_slnFile.VisualStudioVersion, out version) || version.Major < 15)
            {
                _slnFile.ProductDescription = ProductDescription;
                _slnFile.VisualStudioVersion = VisualStudioVersion;
                _slnFile.MinimumVisualStudioVersion = MinimumVisualStudioVersion;
            }

            _slnFile.Write();

            foreach (var csprojFile in csprojFilesToAdd)
            {
                AddProject(_slnFile.FullPath, csprojFile);
            }
        }

        private void AddProject(string slnPath, string csprojPath)
        {
            List<string> args = new List<string>()
                {
                    "sln",
                    slnPath,
                    "add",
                    csprojPath,
                };

            var dotnetPath = Path.Combine(AppContext.BaseDirectory, "dotnet.dll");
            var addCommand = new ForwardingApp(dotnetPath, args);
            addCommand.Execute();
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

            BackupProjects(migrationReport);
        }

        private void BackupProjects(MigrationReport migrationReport)
        {
            foreach (var report in migrationReport.ProjectMigrationReports)
            {
                var backupPlan = new MigrationBackupPlan(
                    new DirectoryInfo(report.ProjectDirectory),
                    _workspaceDirectory);

                backupPlan.PerformBackup();
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

            Reporter.Output.WriteLine(LocalizableStrings.MigrationAdditionalHelp);
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
                projects = GetProjectsFromGlobalJson(projectArg);

                if (!projects.Any())
                {
                    throw new GracefulException("Unable to find any projects in global.json");
                }
            }
            else if (File.Exists(projectArg) && 
                string.Equals(Path.GetExtension(projectArg), ".sln", StringComparison.OrdinalIgnoreCase))
            {
                projects = GetProjectsFromSolution(projectArg);

                if (!projects.Any())
                {
                    throw new GracefulException($"Unable to find any projects in {projectArg}");
                }
            }
            else if (Directory.Exists(projectArg))
            {
                projects = Directory.EnumerateFiles(projectArg, Project.FileName, SearchOption.AllDirectories);

                if (!projects.Any())
                {
                    throw new GracefulException($"No project.json file found in '{projectArg}'");
                }
            }
            else
            {
                throw new GracefulException($"Invalid project argument - '{projectArg}' is not a project.json, global.json, or solution.sln file and a directory named '{projectArg}' doesn't exist.");
            }

            foreach (var project in projects)
            {
                yield return GetProjectJsonPath(project);
            }
        }

        private void EnsureNotNull(string variable, string message)
        {
            if (variable == null)
            {
                throw new GracefulException(message);
            }
        }

        private string GetProjectJsonPath(string projectJson)
        {
            projectJson = ProjectPathHelper.NormalizeProjectFilePath(projectJson);

            if (File.Exists(projectJson))
            {
                return projectJson;
            }

            throw new GracefulException($"Unable to find project file at {projectJson}");
        }

        private IEnumerable<string> GetProjectsFromGlobalJson(string globalJson)
        {
            if (!File.Exists(globalJson))
            {
                throw new GracefulException($"Unable to find global settings file at {globalJson}");
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
                    var projectFilePath = Path.Combine(projectDirectory.FullName, Project.FileName);

                    if (File.Exists(projectFilePath))
                    {
                        yield return projectFilePath;
                    }
                }
            }
        }

        private IEnumerable<string> GetProjectsFromSolution(string slnPath)
        {
            if (!File.Exists(slnPath))
            {
                throw new GracefulException($"Unable to find the solution file at {slnPath}");
            }

            _slnFile = SlnFile.Read(slnPath);

            foreach (var project in _slnFile.Projects)
            {
                var projectFilePath = Path.Combine(
                    _slnFile.BaseDirectory, 
                    Path.GetDirectoryName(project.FilePath), 
                    Project.FileName);

                if (File.Exists(projectFilePath))
                {
                    yield return projectFilePath;
                }
            }
        }

    }
}
