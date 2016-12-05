// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.Build.Construction;

namespace Microsoft.DotNet.ProjectJsonMigration.Rules
{
    //HACK to workaround https://github.com/Microsoft/msbuild/issues/1429
    internal class MigrateWebSdkRule : IMigrationRule
    {
        private static string GetContainingFolderName(string projectDirectory)
        {
            projectDirectory = projectDirectory.TrimEnd(new char[] { '/', '\\' });
            return Path.GetFileName(projectDirectory);
        }

        public void Apply(MigrationSettings migrationSettings, MigrationRuleInputs migrationRuleInputs)
        {
            var project = migrationRuleInputs.DefaultProjectContext.ProjectFile;
            var type = project.GetProjectType();

            if(type == ProjectType.Web)
            {
                ReplaceSdkWithWebSdk(migrationSettings);
            }
        }

        private void ReplaceSdkWithWebSdk(MigrationSettings migrationSettings)
        {
            string csprojName = $"{GetContainingFolderName(migrationSettings.ProjectDirectory)}.csproj";
            var outputProject = Path.Combine(migrationSettings.OutputDirectory, csprojName);

            var csprojContent = File.ReadAllText(outputProject);
            csprojContent = csprojContent.Replace("Sdk=\"Microsoft.NET.Sdk\"", "Sdk=\"Microsoft.NET.Sdk.Web\"");

            File.WriteAllText(outputProject, csprojContent);
        }
    }
}
