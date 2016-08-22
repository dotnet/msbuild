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
using Microsoft.DotNet.ProjectModel.Files;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    public class MigratePublishOptionsRule : IMigrationRule
    {
        private readonly ITransformApplicator _transformApplicator;

        public MigratePublishOptionsRule(TransformApplicator transformApplicator = null)
        {
            _transformApplicator = transformApplicator ?? new TransformApplicator();
        }

        public void Apply(MigrationSettings migrationSettings, MigrationRuleInputs migrationRuleInputs)
        {
            var csproj = migrationRuleInputs.OutputMSBuildProject;
            var projectContext = migrationRuleInputs.DefaultProjectContext;

            var transformResults = new[]
            {
                CopyToOutputFilesTransform.Transform(projectContext.ProjectFile.PublishOptions)
            };

            if (transformResults.Any(t => t != null && t.Any()))
            {
                var itemGroup = migrationRuleInputs.CommonItemGroup;
                _transformApplicator.Execute(
                    CopyToOutputFilesTransform.Transform(projectContext.ProjectFile.PublishOptions),
                    itemGroup,
                    mergeExisting: true);
            }
        }

        private IncludeContextTransform CopyToOutputFilesTransform =>
            new IncludeContextTransform("Content", transformMappings: true)
            .WithMetadata("CopyToOutputDirectory", "None")
            .WithMetadata("CopyToPublishDirectory", "PreserveNewest");
    }
}
