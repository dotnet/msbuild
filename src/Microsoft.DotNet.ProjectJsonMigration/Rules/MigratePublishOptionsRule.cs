// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.DotNet.Internal.ProjectModel.Files;
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

            var project = migrationRuleInputs.DefaultProjectContext.ProjectFile;
            var projectType = project.GetProjectType();

            var copyToPublishDirectoryTransform =
                    projectType == ProjectType.Web ?
                    CopyToPublishDirectoryTransformForWeb :
                    CopyToPublishDirectoryTransform;

            ExecuteTransformation(
                copyToPublishDirectoryTransform,
                projectContext.ProjectFile.PublishOptions,
                migrationRuleInputs);

            if (projectContext.ProjectFile.PublishOptions != null)
            {
                ExecuteTransformation(
                    DoNotCopyToPublishDirectoryTransform,
                    new ExcludeContext(
                        projectContext.ProjectFile.PublishOptions.SourceBasePath,
                        projectContext.ProjectFile.PublishOptions.Option,
                        projectContext.ProjectFile.PublishOptions.RawObject,
                        projectContext.ProjectFile.PublishOptions.BuiltInsInclude?.ToArray(),
                        projectContext.ProjectFile.PublishOptions.BuiltInsExclude?.ToArray()),
                    migrationRuleInputs);
            }
        }

        private void ExecuteTransformation(
            IncludeContextTransform transform,
            IncludeContext includeContext,
            MigrationRuleInputs migrationRuleInputs)
        {
            var transformResult = transform.Transform(includeContext);

            if (transformResult != null && transformResult.Any())
            {
                var itemGroup = migrationRuleInputs.CommonItemGroup;
                _transformApplicator.Execute(
                    transformResult,
                    itemGroup,
                    mergeExisting: true);
            }
        }

        private IncludeContextTransform CopyToPublishDirectoryTransform =>
            new UpdateContextTransform("None", transformMappings: true)
                .WithMetadata("CopyToPublishDirectory", "PreserveNewest");

        private IncludeContextTransform DoNotCopyToPublishDirectoryTransform =>
            new UpdateContextTransform("None", transformMappings: true)
                .WithMetadata("CopyToPublishDirectory", "Never");

        private IncludeContextTransform CopyToPublishDirectoryTransformForWeb =>
            new UpdateContextTransform(
                    "None",
                    transformMappings: true,
                    excludePatternsRule: pattern => ItemsIncludedInTheWebSDK.HasContent(pattern))
                .WithMetadata("CopyToPublishDirectory", "PreserveNewest");
    }
}
