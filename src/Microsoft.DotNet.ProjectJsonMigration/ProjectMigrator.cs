// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Cli;
using System.Linq;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    public class ProjectMigrator
    {
        // TODO: Migrate PackOptions
        // TODO: Support Mappings in IncludeContext Transformations
        // TODO: Migrate Multi-TFM projects
        // TODO: Tests
        // TODO: Out of Scope
        //     - Globs that resolve to directories: /some/path/**/somedir
        //     - Migrating Deprecated project.jsons
        //     - Configuration dependent source exclusion

        public void Migrate(MigrationSettings migrationSettings)
        {
            var projectDirectory = migrationSettings.ProjectDirectory;
            EnsureDirectoryExists(migrationSettings.OutputDirectory);

            var migrationRuleInputs = ComputeMigrationRuleInputs(migrationSettings);
            VerifyInputs(migrationRuleInputs);
            
            new DefaultMigrationRuleSet().Apply(migrationSettings, migrationRuleInputs);
        }

        private void EnsureDirectoryExists(string outputDirectory)
        {
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }
        }

        private MigrationRuleInputs ComputeMigrationRuleInputs(MigrationSettings migrationSettings)
        {
            var projectContexts = ProjectContext.CreateContextForEachFramework(migrationSettings.ProjectDirectory);

            var templateMSBuildProject = migrationSettings.MSBuildProjectTemplate ?? ProjectRootElement.Create();

            var propertyGroup = templateMSBuildProject.AddPropertyGroup();
            var itemGroup = templateMSBuildProject.AddItemGroup();

            return new MigrationRuleInputs(projectContexts, templateMSBuildProject, itemGroup, propertyGroup);
        }

        private void VerifyInputs(MigrationRuleInputs migrationRuleInputs)
        {
            VerifyProject(migrationRuleInputs.ProjectContexts);
        }

        private void VerifyProject(IEnumerable<ProjectContext> projectContexts)
        {
            if (projectContexts.Count() > 1)
            {
                throw new Exception("MultiTFM projects currently not supported.");
            }

            if (projectContexts.Count() == 0)
            {
                throw new Exception("No projects found");
            }

            if (projectContexts.First().LockFile == null)
            {
                throw new Exception("Restore must be run prior to project migration.");
            }
        }


    }
}
