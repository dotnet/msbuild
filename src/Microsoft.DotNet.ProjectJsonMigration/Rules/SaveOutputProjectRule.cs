// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.Build.Construction;

namespace Microsoft.DotNet.ProjectJsonMigration.Rules
{
    internal class SaveOutputProjectRule : IMigrationRule
    {
        private static string GetContainingFolderName(string projectDirectory)
        {
            projectDirectory = projectDirectory.TrimEnd(new char[] { '/', '\\' });
            return Path.GetFileName(projectDirectory);
        }

        public void Apply(MigrationSettings migrationSettings, MigrationRuleInputs migrationRuleInputs)
        {
            var outputName = migrationRuleInputs.DefaultProjectContext.GetProjectName();

            string csprojName = $"{GetContainingFolderName(migrationSettings.ProjectDirectory)}.csproj";
            var outputProject = Path.Combine(migrationSettings.OutputDirectory, csprojName);

            migrationRuleInputs.OutputMSBuildProject.Save(outputProject);
        }
    }
}
