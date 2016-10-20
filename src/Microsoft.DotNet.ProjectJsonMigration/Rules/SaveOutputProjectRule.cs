// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.Build.Construction;

namespace Microsoft.DotNet.ProjectJsonMigration.Rules
{
    public class SaveOutputProjectRule : IMigrationRule
    {
        public void Apply(MigrationSettings migrationSettings, MigrationRuleInputs migrationRuleInputs)
        {
            var outputName = migrationRuleInputs.DefaultProjectContext.GetProjectName();

            var outputProject = Path.Combine(migrationSettings.OutputDirectory, outputName + ".csproj");

            migrationRuleInputs.OutputMSBuildProject.Save(outputProject);
        }
    }
}
