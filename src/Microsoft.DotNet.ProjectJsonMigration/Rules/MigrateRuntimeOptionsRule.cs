// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;

namespace Microsoft.DotNet.ProjectJsonMigration.Rules
{
    internal class MigrateRuntimeOptionsRule : IMigrationRule
    {
        private static readonly string s_runtimeOptionsFileName = "runtimeconfig.template.json";

        public void Apply(MigrationSettings migrationSettings, MigrationRuleInputs migrationRuleInputs)
        {
            var projectContext = migrationRuleInputs.DefaultProjectContext;
            var raw = projectContext.ProjectFile.RawRuntimeOptions;
            var outputRuntimeOptionsFile = Path.Combine(migrationSettings.OutputDirectory, s_runtimeOptionsFileName);

            if (!string.IsNullOrEmpty(raw))
            {
                if (File.Exists(outputRuntimeOptionsFile))
                {
                    MigrationErrorCodes.MIGRATE1015(
                        String.Format(LocalizableStrings.ProjAlreadyExistsError, outputRuntimeOptionsFile)).Throw();
                }

                File.WriteAllText(outputRuntimeOptionsFile, raw);
            }
        }   
    }
}
