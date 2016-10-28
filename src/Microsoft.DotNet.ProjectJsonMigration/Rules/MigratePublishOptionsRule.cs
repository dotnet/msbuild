// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.DotNet.ProjectJsonMigration.Transforms;

namespace Microsoft.DotNet.ProjectJsonMigration.Rules
{
    internal class MigratePublishOptionsRule : IMigrationRule
    {
        private readonly ITransformApplicator _transformApplicator;

        public MigratePublishOptionsRule(ITransformApplicator transformApplicator = null)
        {
            _transformApplicator = transformApplicator ?? new TransformApplicator();
        }

        public void Apply(MigrationSettings migrationSettings, MigrationRuleInputs migrationRuleInputs)
        {
            var csproj = migrationRuleInputs.OutputMSBuildProject;
            var projectContext = migrationRuleInputs.DefaultProjectContext;

            var transformResult = CopyToOutputFilesTransform.Transform(projectContext.ProjectFile.PublishOptions);

            if (transformResult != null && transformResult.Any())
            {
                var itemGroup = migrationRuleInputs.CommonItemGroup;
                _transformApplicator.Execute(
                    transformResult,
                    itemGroup,
                    mergeExisting: true);
            }
        }

        private IncludeContextTransform CopyToOutputFilesTransform =>
            new IncludeContextTransform("Content", transformMappings: true)
                .WithMetadata("CopyToPublishDirectory", "PreserveNewest");
    }
}
