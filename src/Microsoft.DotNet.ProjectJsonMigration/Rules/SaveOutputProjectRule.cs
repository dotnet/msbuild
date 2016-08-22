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
using Newtonsoft.Json;
using Microsoft.DotNet.ProjectModel.Files;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    public class SaveOutputProjectRule : IMigrationRule
    {
        public void Apply(MigrationSettings migrationSettings, MigrationRuleInputs migrationRuleInputs)
        {
            var outputName = Path.GetFileNameWithoutExtension(
                migrationRuleInputs.DefaultProjectContext.GetOutputPaths("_").CompilationFiles.Assembly);

            var outputProject = Path.Combine(migrationSettings.OutputDirectory, outputName + ".csproj");

            migrationRuleInputs.OutputMSBuildProject.Save(outputProject);
        }
    }
}
