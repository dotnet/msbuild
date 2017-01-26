// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.DotNet.Internal.ProjectModel;
using Microsoft.DotNet.Internal.ProjectModel.Graph;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.ExceptionExtensions;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.ProjectJsonMigration.Rules;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    internal class ProjectMigrator
    {
        private readonly IMigrationRule _ruleSet;
        private readonly ProjectDependencyFinder _projectDependencyFinder = new ProjectDependencyFinder();
        private HashSet<string> _migratedProjects = new HashSet<string>();

        public ProjectMigrator() : this(new DefaultMigrationRuleSet()) { }

        public ProjectMigrator(IMigrationRule ruleSet)
        {
            _ruleSet = ruleSet;
        }

        public MigrationReport Migrate(MigrationSettings rootSettings, bool skipProjectReferences = false)
        {
            if (rootSettings == null)
            {
                throw new ArgumentNullException();
            }
            
            // Try to read the project dependencies, ignore an unresolved exception for now
            MigrationRuleInputs rootInputs = ComputeMigrationRuleInputs(rootSettings);
            IEnumerable<ProjectDependency> projectDependencies = null;
            var projectMigrationReports = new List<ProjectMigrationReport>();

            try
            {
                // Verify up front so we can prefer these errors over an unresolved project dependency
                VerifyInputs(rootInputs, rootSettings);

                projectMigrationReports.Add(MigrateProject(rootSettings));
                
                if (skipProjectReferences)
                {
                    return new MigrationReport(projectMigrationReports);
                }

                projectDependencies = ResolveTransitiveClosureProjectDependencies(
                    rootSettings.ProjectDirectory, 
                    rootSettings.ProjectXProjFilePath,
                    rootSettings.SolutionFile);
            }
            catch (MigrationException e)
            {
                return new MigrationReport(
                    new List<ProjectMigrationReport>
                    {
                        new ProjectMigrationReport(
                            rootSettings.ProjectDirectory,
                            rootInputs?.DefaultProjectContext?.GetProjectName(),
                            new List<MigrationError> {e.Error},
                            null)
                    });
            }

            foreach(var project in projectDependencies)
            {
                var projectDir = Path.GetDirectoryName(project.ProjectFilePath);
                var settings = new MigrationSettings(projectDir,
                                                     projectDir,
                                                     rootSettings.MSBuildProjectTemplatePath);
                projectMigrationReports.Add(MigrateProject(settings));
            }

            return new MigrationReport(projectMigrationReports);
        }

        private void DeleteProjectJsons(MigrationSettings rootsettings, IEnumerable<ProjectDependency> projectDependencies)
        {
            try
            {
                File.Delete(Path.Combine(rootsettings.ProjectDirectory, "project.json"));
            }
            catch (Exception e)
            {
                e.ReportAsWarning();
            }

            foreach (var projectDependency in projectDependencies)
            {
                try 
                {
                    File.Delete(projectDependency.ProjectFilePath);
                }
                catch (Exception e)
                {
                    e.ReportAsWarning();
                }
            }
        }

        private IEnumerable<ProjectDependency> ResolveTransitiveClosureProjectDependencies(
            string rootProject, string xprojFile, SlnFile solutionFile)
        {
            HashSet<ProjectDependency> projectsMap = new HashSet<ProjectDependency>(new ProjectDependencyComparer());
            var projectDependencies = _projectDependencyFinder.ResolveProjectDependencies(rootProject, xprojFile, solutionFile);
            Queue<ProjectDependency> projectsQueue = new Queue<ProjectDependency>(projectDependencies);

            while (projectsQueue.Count() != 0)
            {
                var projectDependency = projectsQueue.Dequeue();

                if (projectsMap.Contains(projectDependency))
                {
                    continue;
                }

                projectsMap.Add(projectDependency);

                var projectDir = Path.GetDirectoryName(projectDependency.ProjectFilePath);
                projectDependencies = _projectDependencyFinder.ResolveProjectDependencies(projectDir);

                foreach (var project in projectDependencies)
                {
                    projectsQueue.Enqueue(project);
                }
            }

            return projectsMap;
        }

        private ProjectMigrationReport MigrateProject(MigrationSettings migrationSettings)
        {
            var migrationRuleInputs = ComputeMigrationRuleInputs(migrationSettings);
            var projectName = migrationRuleInputs.DefaultProjectContext.GetProjectName();
            var outputProject = Path.Combine(migrationSettings.OutputDirectory, projectName + ".csproj");

            try
            {
                if (File.Exists(outputProject))
                {
                    if (_migratedProjects.Contains(outputProject))
                    {
                        MigrationTrace.Instance.WriteLine(String.Format(
                            LocalizableStrings.SkipMigrationAlreadyMigrated,
                            nameof(ProjectMigrator),
                            migrationSettings.ProjectDirectory));

                        return new ProjectMigrationReport(
                            migrationSettings.ProjectDirectory,
                            projectName,
                            skipped: true);
                    }
                    else
                    {
                        MigrationBackupPlan.RenameCsprojFromMigrationOutputNameToTempName(outputProject);
                    }
                }

                VerifyInputs(migrationRuleInputs, migrationSettings);

                SetupOutputDirectory(migrationSettings.ProjectDirectory, migrationSettings.OutputDirectory);

                _ruleSet.Apply(migrationSettings, migrationRuleInputs);
            }
            catch (MigrationException exc)
            {
                var error = new List<MigrationError>
                {
                    exc.Error
                };

                return new ProjectMigrationReport(migrationSettings.ProjectDirectory, projectName, error, null);
            }

            List<string> csprojDependencies = null;
            if (migrationRuleInputs.ProjectXproj != null)
            {
                var projectDependencyFinder = new ProjectDependencyFinder();
                var dependencies = projectDependencyFinder.ResolveXProjProjectDependencies(
                    migrationRuleInputs.ProjectXproj);

                if (dependencies.Any())
                {
                    csprojDependencies = dependencies
                        .SelectMany(r => r.Includes().Select(p => PathUtility.GetPathWithDirectorySeparator(p)))
                        .ToList();
                }
                else
                {
                    csprojDependencies = new List<string>();
                }
            }

            _migratedProjects.Add(outputProject);

            return new ProjectMigrationReport(
                migrationSettings.ProjectDirectory,
                projectName,
                outputProject,
                null,
                null,
                csprojDependencies);
        }

        private MigrationRuleInputs ComputeMigrationRuleInputs(MigrationSettings migrationSettings)
        {
            var projectContexts = ProjectContext.CreateContextForEachFramework(migrationSettings.ProjectDirectory);
            var xprojFile = migrationSettings.ProjectXProjFilePath ?? _projectDependencyFinder.FindXprojFile(migrationSettings.ProjectDirectory);

            ProjectRootElement xproj = null;
            if (xprojFile != null)
            {
                xproj = ProjectRootElement.Open(xprojFile);
            }

            var templateMSBuildProject = migrationSettings.MSBuildProjectTemplate;
            if (templateMSBuildProject == null)
            {
                throw new Exception(LocalizableStrings.NullMSBuildProjectTemplateError);
            }

            var propertyGroup = templateMSBuildProject.AddPropertyGroup();
            var itemGroup = templateMSBuildProject.AddItemGroup();

            return new MigrationRuleInputs(projectContexts, templateMSBuildProject, itemGroup, propertyGroup, xproj);
        }

        private void VerifyInputs(MigrationRuleInputs migrationRuleInputs, MigrationSettings migrationSettings)
        {
            VerifyProject(migrationRuleInputs.ProjectContexts, migrationSettings.ProjectDirectory);
        }

        private void VerifyProject(IEnumerable<ProjectContext> projectContexts, string projectDirectory)
        {
            if (!projectContexts.Any())
            {
                MigrationErrorCodes.MIGRATE1013(String.Format(LocalizableStrings.MIGRATE1013Arg, projectDirectory)).Throw();
            }

            var defaultProjectContext = projectContexts.First();

            var diagnostics = defaultProjectContext.ProjectFile.Diagnostics;
            if (diagnostics.Any())
            {
                var warnings = diagnostics.Where(d => d.Severity == DiagnosticMessageSeverity.Warning);
                if (warnings.Any())
                {
                    var deprecatedProjectJsonWarnings = string.Join(
                        Environment.NewLine,
                        diagnostics.Select(d => FormatDiagnosticMessage(d)));
                    var warningMessage = $"{projectDirectory}{Environment.NewLine}{deprecatedProjectJsonWarnings}";
                    Reporter.Output.WriteLine(warningMessage.Yellow());
                }

                var errors = diagnostics.Where(d => d.Severity == DiagnosticMessageSeverity.Error);
                if (errors.Any())
                {
                    MigrationErrorCodes.MIGRATE1011(String.Format(
                        "{0}{1}{2}",
                        projectDirectory,
                        Environment.NewLine,
                        string.Join(Environment.NewLine, diagnostics.Select(d => FormatDiagnosticMessage(d)))))
                        .Throw();
                }
            }

            var compilerName =
                defaultProjectContext.ProjectFile.GetCompilerOptions(defaultProjectContext.TargetFramework, "_")
                    .CompilerName;
            if (!compilerName.Equals("csc", StringComparison.OrdinalIgnoreCase))
            {
                MigrationErrorCodes.MIGRATE20013(
                    String.Format(LocalizableStrings.CannotMigrateProjectWithCompilerError, defaultProjectContext.ProjectFile.ProjectFilePath, compilerName)).Throw();
            }
        }

        private string FormatDiagnosticMessage(DiagnosticMessage d)
        {
            return String.Format(LocalizableStrings.DiagnosticMessageTemplate, d.Message, d.StartLine, d.SourceFilePath);
        }

        private void SetupOutputDirectory(string projectDirectory, string outputDirectory)
        {
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            if (projectDirectory != outputDirectory)
            {
                CopyProjectToOutputDirectory(projectDirectory, outputDirectory);
            }
        }

        private void CopyProjectToOutputDirectory(string projectDirectory, string outputDirectory)
        {
            var sourceFilePaths = Directory.EnumerateFiles(projectDirectory, "*", SearchOption.AllDirectories);

            foreach (var sourceFilePath in sourceFilePaths)
            {
                var relativeFilePath = PathUtility.GetRelativePath(projectDirectory, sourceFilePath);
                var destinationFilePath = Path.Combine(outputDirectory, relativeFilePath);
                var destinationDirectory = Path.GetDirectoryName(destinationFilePath);

                if (!Directory.Exists(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                File.Copy(sourceFilePath, destinationFilePath);
            }
        }
    }
}
