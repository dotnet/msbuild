// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Microsoft.DotNet.ProjectJsonMigration.Rules
{
    /// <summary>
    /// This rule is temporary while project.json still exists in the new project system.
    /// It renames your existing project.json (if output directory is the current project directory),
    /// creates a copy, then mutates that copy.
    /// 
    /// Mutations:
    ///  - inject a dependency on the Microsoft.SDK targets
    ///  - removing the "runtimes" node.
    /// </summary>
    public class TemporaryMutateProjectJsonRule : IMigrationRule
    {
        public void Apply(MigrationSettings migrationSettings, MigrationRuleInputs migrationRuleInputs)
        {
            var sourceProjectFile = Path.Combine(migrationSettings.ProjectDirectory, "project.json");

            var renamedProjectFile = Path.Combine(migrationSettings.ProjectDirectory, "project.migrated.json");
            File.Move(sourceProjectFile, renamedProjectFile);
            sourceProjectFile = renamedProjectFile;
        }
    }
}