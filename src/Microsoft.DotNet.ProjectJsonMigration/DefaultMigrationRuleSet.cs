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
using Microsoft.DotNet.ProjectJsonMigration.Rules;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    public class DefaultMigrationRuleSet : IMigrationRule
    {
        private IMigrationRule[] Rules => new IMigrationRule[]
        {
            new MigrateRootOptionsRule(),
            new MigrateTFMRule(),
            new MigrateBuildOptionsRule(),
            new MigrateRuntimeOptionsRule(),
            new MigratePublishOptionsRule(),
            new MigrateProjectDependenciesRule(),
            new MigrateConfigurationsRule(),
            new MigrateScriptsRule(),
            new TemporaryMutateProjectJsonRule(),
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
