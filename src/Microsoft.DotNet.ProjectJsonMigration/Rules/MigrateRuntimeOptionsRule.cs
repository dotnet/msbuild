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
    public class MigrateRuntimeOptionsRule : IMigrationRule
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
                    throw new Exception("Runtime options file already exists. Has migration already been run?");
                }

                File.WriteAllText(outputRuntimeOptionsFile, raw);
            }
        }   
    }
}
