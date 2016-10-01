// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.ProjectJsonMigration.Rules;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    public class DefaultMigrationRuleSet : IMigrationRule
    {
        private IMigrationRule[] Rules => new IMigrationRule[]
        {
            new MigrateRootOptionsRule(),
            new MigrateTFMRule(),
            new MigrateBuildOptionsRule(),
            new MigratePackOptionsRule(),
            new MigrateRuntimeOptionsRule(),
            new MigratePublishOptionsRule(),
            new MigrateProjectDependenciesRule(),
            new MigrateConfigurationsRule(),
            new MigrateScriptsRule(),
            new TemporaryMutateProjectJsonRule(),
            new WorkaroundOptionsRule(),
            new SaveOutputProjectRule()
        };

        public void Apply(MigrationSettings migrationSettings, MigrationRuleInputs migrationRuleInputs)
        {
            foreach (var rule in Rules)
            {
                MigrationTrace.Instance.WriteLine($"{nameof(DefaultMigrationRuleSet)}: Executing migration rule {rule.GetType().Name}");
                rule.Apply(migrationSettings, migrationRuleInputs);
            }
        }
    }
}
