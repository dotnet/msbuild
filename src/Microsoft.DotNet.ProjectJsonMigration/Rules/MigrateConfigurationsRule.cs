// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Construction;
using NuGet.Frameworks;
using Microsoft.DotNet.ProjectModel;

namespace Microsoft.DotNet.ProjectJsonMigration.Rules
{
    public class MigrateConfigurationsRule : IMigrationRule
    {
        public void Apply(MigrationSettings migrationSettings, MigrationRuleInputs migrationRuleInputs)
        {
            MigrationTrace.Instance.WriteLine($"Executing rule: {nameof(MigrateConfigurationsRule)}");
            var projectContext = migrationRuleInputs.DefaultProjectContext;
            var configurations = projectContext.ProjectFile.GetConfigurations().ToList();

            var frameworks = new List<NuGetFramework>();
            frameworks.AddRange(projectContext.ProjectFile.GetTargetFrameworks().Select(t => t.FrameworkName));

            if (!configurations.Any() && !frameworks.Any())
            {
                return;
            }

            foreach (var framework in frameworks)
            {
                MigrateConfiguration(projectContext.ProjectFile, framework, migrationSettings, migrationRuleInputs);
            }

            foreach (var configuration in configurations)
            {
                MigrateConfiguration(projectContext.ProjectFile, configuration, migrationSettings, migrationRuleInputs);
            }
        }

        private void MigrateConfiguration(
            Project project,
            string configuration,
            MigrationSettings migrationSettings, 
            MigrationRuleInputs migrationRuleInputs)
        {
            var buildOptions = project.GetRawCompilerOptions(configuration);
            var configurationCondition = $" '$(Configuration)' == '{configuration}' ";

            MigrateConfiguration(buildOptions, configurationCondition, migrationSettings, migrationRuleInputs);
        }

        private void MigrateConfiguration(
            Project project,
            NuGetFramework framework,
            MigrationSettings migrationSettings,
            MigrationRuleInputs migrationRuleInputs)
        {
            var buildOptions = project.GetRawCompilerOptions(framework);
            var configurationCondition = $" '$(TargetFrameworkIdentifier),Version=$(TargetFrameworkVersion)' == '{framework.DotNetFrameworkName}' ";

            MigrateConfiguration(buildOptions, configurationCondition, migrationSettings, migrationRuleInputs);
        }

        private void MigrateConfiguration(
            CommonCompilerOptions buildOptions,
            string configurationCondition,
            MigrationSettings migrationSettings,
            MigrationRuleInputs migrationRuleInputs)
        {
            var csproj = migrationRuleInputs.OutputMSBuildProject;

            var propertyGroup = CreatePropertyGroupAtEndOfProject(csproj);
            var itemGroup = CreateItemGroupAtEndOfProject(csproj);

            propertyGroup.Condition = configurationCondition;
            itemGroup.Condition = configurationCondition;

            new MigrateBuildOptionsRule(buildOptions, propertyGroup, itemGroup)
                .Apply(migrationSettings, migrationRuleInputs);

            propertyGroup.RemoveIfEmpty();
            itemGroup.RemoveIfEmpty();
        }


        private ProjectPropertyGroupElement CreatePropertyGroupAtEndOfProject(ProjectRootElement csproj)
        {
            var propertyGroup = csproj.CreatePropertyGroupElement();
            csproj.InsertBeforeChild(propertyGroup, csproj.LastChild);
            return propertyGroup;
        }

        private ProjectItemGroupElement CreateItemGroupAtEndOfProject(ProjectRootElement csproj)
        {
            var itemGroup = csproj.CreateItemGroupElement();
            csproj.InsertBeforeChild(itemGroup, csproj.LastChild);
            return itemGroup;
        }
    }
}
