// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli;
using System.Linq;
using System.IO;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    public class MigrateConfigurationsRule : IMigrationRule
    {
        public void Apply(MigrationSettings migrationSettings, MigrationRuleInputs migrationRuleInputs)
        {
            var projectContext = migrationRuleInputs.DefaultProjectContext;
            var configurations = projectContext.ProjectFile.GetConfigurations().ToList();

            if (!configurations.Any())
            {
                return;
            }

            foreach (var configuration in configurations)
            {
                MigrateConfiguration(configuration, migrationSettings, migrationRuleInputs);
            }
        }

        private void MigrateConfiguration(
            string configuration, 
            MigrationSettings migrationSettings, 
            MigrationRuleInputs migrationRuleInputs)
        {
            var csproj = migrationRuleInputs.OutputMSBuildProject;

            var propertyGroup = CreatePropertyGroupAtEndOfProject(csproj);
            var itemGroup = CreateItemGroupAtEndOfProject(csproj);

            var configurationCondition = $" '$(Configuration)' == '{configuration}' ";
            propertyGroup.Condition = configurationCondition;
            itemGroup.Condition = configurationCondition;

            new MigrateBuildOptionsRule(configuration, propertyGroup, itemGroup)
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
