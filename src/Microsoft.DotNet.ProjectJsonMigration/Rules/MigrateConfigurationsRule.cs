// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Construction;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectJsonMigration.Rules
{
    public class MigrateConfigurationsRule : IMigrationRule
    {
        public void Apply(MigrationSettings migrationSettings, MigrationRuleInputs migrationRuleInputs)
        {
            var projectContext = migrationRuleInputs.DefaultProjectContext;
            var configurations = projectContext.ProjectFile.GetConfigurations().ToList();

            var frameworks = new List<NuGetFramework>();
            frameworks.Add(null);
            frameworks.AddRange(projectContext.ProjectFile.GetTargetFrameworks().Select(t => t.FrameworkName));

            if (!configurations.Any())
            {
                return;
            }

            var frameworkConfigurationCombinations = frameworks.SelectMany(f => configurations, Tuple.Create);

            foreach (var entry in frameworkConfigurationCombinations)
            {
                var framework = entry.Item1;
                var configuration = entry.Item2;

                MigrateConfiguration(configuration, framework, migrationSettings, migrationRuleInputs);
            }
        }

        private void MigrateConfiguration(
            string configuration,
            NuGetFramework framework,
            MigrationSettings migrationSettings, 
            MigrationRuleInputs migrationRuleInputs)
        {
            var csproj = migrationRuleInputs.OutputMSBuildProject;

            var propertyGroup = CreatePropertyGroupAtEndOfProject(csproj);
            var itemGroup = CreateItemGroupAtEndOfProject(csproj);

            var configurationCondition = $" '$(Configuration)' == '{configuration}' ";
            if (framework != null)
            {
                configurationCondition +=
                    $" and '$(TargetFrameworkIdentifier),Version=$(TargetFrameworkVersion)' == '{framework.DotNetFrameworkName}' ";
            }
            propertyGroup.Condition = configurationCondition;
            itemGroup.Condition = configurationCondition;

            new MigrateBuildOptionsRule(configuration, framework, propertyGroup, itemGroup)
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
