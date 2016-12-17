// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.ProjectJsonMigration.Rules;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    internal class DefaultMigrationRuleSet : IMigrationRule
    {
        private IMigrationRule[] Rules => new IMigrationRule[]
        {
            new AddDefaultsToProjectRule(),
            new MigrateRootOptionsRule(),
            new MigrateTFMRule(),
            new MigrateBuildOptionsRule(),
            new MigrateJsonPropertiesRule(),
            new MigratePackOptionsRule(),
            new MigrateRuntimeOptionsRule(),
            new MigrateRuntimesRule(),
            new MigratePublishOptionsRule(),
            new MigrateProjectDependenciesRule(),
            new MigratePackageDependenciesAndToolsRule(),
            new MigrateConfigurationsRule(),
            new MigrateScriptsRule(),
            new MigrateAssemblyInfoRule(),
            new RemoveDefaultsFromProjectRule(),
            new CleanOutputProjectRule(),
            new SaveOutputProjectRule(),
            new MigrateWebSdkRule()
        };

        public void Apply(MigrationSettings migrationSettings, MigrationRuleInputs migrationRuleInputs)
        {
            foreach (var rule in Rules)
            {
                MigrationTrace.Instance.WriteLine(string.Format(LocalizableStrings.ExecutingMigrationRule, nameof(DefaultMigrationRuleSet), rule.GetType().Name));
                rule.Apply(migrationSettings, migrationRuleInputs);
            }
        }
    }
}
