// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.DotNet.Cli;
using System.Linq;
using System.IO;
using Microsoft.DotNet.ProjectJsonMigration.Rules;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    public class ProjectMigrator
    {
        private readonly IMigrationRule _ruleSet;
        private readonly ProjectDependencyFinder _projectDependencyFinder = new ProjectDependencyFinder();

        public ProjectMigrator() : this(new DefaultMigrationRuleSet()) { }

        public ProjectMigrator(IMigrationRule ruleSet)
        {
            _ruleSet = ruleSet;
        }

        public void Migrate(MigrationSettings rootSettings, bool skipProjectReferences = false)
        {
            if (rootSettings == null)
            {
                throw new ArgumentNullException();
            }
            Exception exc = null;
            IEnumerable<ProjectDependency> projectDependencies = null;

            var tempMSBuildProjectTemplate = rootSettings.MSBuildProjectTemplate.DeepClone();

            try
            {
                projectDependencies = ResolveTransitiveClosureProjectDependencies(
                    rootSettings.ProjectDirectory, 
                    rootSettings.ProjectXProjFilePath);
            }
            catch (Exception e)
            {
                exc = e;
            }

            // Verify up front so we can prefer these errors over an unresolved project dependency
            VerifyInputs(ComputeMigrationRuleInputs(rootSettings), rootSettings);
            if (exc != null)
            {
                throw exc;
            }

            MigrateProject(rootSettings);

            if (skipProjectReferences)
            {
                return;
            }

            foreach(var project in projectDependencies)
            {
                var projectDir = Path.GetDirectoryName(project.ProjectFilePath);
                var settings = new MigrationSettings(projectDir,
                                                     projectDir,
                                                     rootSettings.SdkPackageVersion,
                                                     tempMSBuildProjectTemplate.DeepClone());
                MigrateProject(settings);
            }
        }

        private void DeleteProjectJsons(MigrationSettings rootsettings, IEnumerable<ProjectDependency> projectDependencies)
        {
            try
            {
                File.Delete(Path.Combine(rootsettings.ProjectDirectory, "project.json"));
            } catch {} 

            foreach (var projectDependency in projectDependencies)
            {
                try 
                {
                    File.Delete(projectDependency.ProjectFilePath);
                } catch { }
            }
        }

        private IEnumerable<ProjectDependency> ResolveTransitiveClosureProjectDependencies(string rootProject, string xprojFile)
        {
            HashSet<ProjectDependency> projectsMap = new HashSet<ProjectDependency>(new ProjectDependencyComparer());
            var projectDependencies = _projectDependencyFinder.ResolveProjectDependencies(rootProject, xprojFile);
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

        private void MigrateProject(MigrationSettings migrationSettings)
        {
            var migrationRuleInputs = ComputeMigrationRuleInputs(migrationSettings);

            if (IsMigrated(migrationSettings, migrationRuleInputs))
            {
                // TODO : Adding user-visible logging
                MigrationTrace.Instance.WriteLine($"{nameof(ProjectMigrator)}: Skip migrating {migrationSettings.ProjectDirectory}, it is already migrated.");
                return;
            }

            VerifyInputs(migrationRuleInputs, migrationSettings);

            SetupOutputDirectory(migrationSettings.ProjectDirectory, migrationSettings.OutputDirectory);

            _ruleSet.Apply(migrationSettings, migrationRuleInputs);
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
                throw new Exception("Expected non-null MSBuildProjectTemplate in MigrationSettings");
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
                MigrationErrorCodes.MIGRATE1013($"No projects found in {projectDirectory}").Throw();
            }

            var defaultProjectContext = projectContexts.First();

            var diagnostics = defaultProjectContext.ProjectFile.Diagnostics;
            if (diagnostics.Any())
            {
                MigrationErrorCodes.MIGRATE1011(
                        $"{projectDirectory}{Environment.NewLine}{string.Join(Environment.NewLine, diagnostics.Select(d => FormatDiagnosticMessage(d)))}")
                    .Throw();
            }

            var compilerName =
                defaultProjectContext.ProjectFile.GetCompilerOptions(defaultProjectContext.TargetFramework, "_")
                    .CompilerName;
            if (!compilerName.Equals("csc", StringComparison.OrdinalIgnoreCase))
            {
                MigrationErrorCodes.MIGRATE20013(
                    $"Cannot migrate project {defaultProjectContext.ProjectFile.ProjectFilePath} using compiler {compilerName}").Throw();
            }
        }

        private string FormatDiagnosticMessage(DiagnosticMessage d)
        {
            return $"{d.Message} (line: {d.StartLine}, file: {d.SourceFilePath})";
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

        public bool IsMigrated(MigrationSettings migrationSettings, MigrationRuleInputs migrationRuleInputs)
        {
            var outputName = Path.GetFileNameWithoutExtension(
                migrationRuleInputs.DefaultProjectContext.GetOutputPaths("_").CompilationFiles.Assembly);

            var outputProject = Path.Combine(migrationSettings.OutputDirectory, outputName + ".csproj");
            return File.Exists(outputProject);
        }

    }
}
