// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectJsonMigration;
using Microsoft.DotNet.ProjectJsonMigration.Transforms;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Files;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.ProjectJsonMigration.Rules
{
    public class MigratePackOptionsRule : IMigrationRule
    {
        private AddPropertyTransform<PackOptions> TagsTransform => new AddPropertyTransform<PackOptions>(
                    "PackageTags", 
                    packOptions => string.Join(";", packOptions.Tags),
                    packOptions => packOptions.Tags != null && packOptions.Tags.Any());

        private AddPropertyTransform<PackOptions> ReleaseNotesTransform => new AddPropertyTransform<PackOptions>(
                    "PackageReleaseNotes", 
                    packOptions => packOptions.ReleaseNotes,
                    packOptions => !string.IsNullOrEmpty(packOptions.ReleaseNotes));

        private AddPropertyTransform<PackOptions> IconUrlTransform => new AddPropertyTransform<PackOptions>(
                    "PackageIconUrl", 
                    packOptions => packOptions.IconUrl,
                    packOptions => !string.IsNullOrEmpty(packOptions.IconUrl));

        private AddPropertyTransform<PackOptions> ProjectUrlTransform => new AddPropertyTransform<PackOptions>(
                    "PackageProjectUrl", 
                    packOptions => packOptions.ProjectUrl,
                    packOptions => !string.IsNullOrEmpty(packOptions.ProjectUrl));

        private AddPropertyTransform<PackOptions> LicenseUrlTransform => new AddPropertyTransform<PackOptions>(
                    "PackageLicenseUrl", 
                    packOptions => packOptions.LicenseUrl,
                    packOptions => !string.IsNullOrEmpty(packOptions.LicenseUrl));

        private AddPropertyTransform<PackOptions> RequireLicenseAcceptanceTransform => new AddPropertyTransform<PackOptions>(
                    "PackageRequireLicenseAcceptance", 
                    packOptions => packOptions.RequireLicenseAcceptance.ToString().ToLower(),
                    packOptions => true);

        private AddPropertyTransform<PackOptions> RepositoryTypeTransform => new AddPropertyTransform<PackOptions>(
                    "RepositoryType", 
                    packOptions => packOptions.RepositoryType,
                    packOptions => !string.IsNullOrEmpty(packOptions.RepositoryType));

        private AddPropertyTransform<PackOptions> RepositoryUrlTransform => new AddPropertyTransform<PackOptions>(
                    "RepositoryUrl", 
                    packOptions => packOptions.RepositoryUrl,
                    packOptions => !string.IsNullOrEmpty(packOptions.RepositoryUrl));

        private IncludeContextTransform PackFilesTransform =>
            new IncludeContextTransform("Content", transformMappings: true)
                .WithMetadata("Pack", "True")
                .WithMappingsToTransform(_mappingsToTransfrom);

        private Func<AddItemTransform<IncludeContext>, string, AddItemTransform<IncludeContext>> _mappingsToTransfrom =>
            (addItemTransform, targetPath) =>
            {
                var msbuildLinkMetadataValue = ConvertTargetPathToMsbuildMetadata(targetPath);

                return addItemTransform.WithMetadata("PackagePath", msbuildLinkMetadataValue);
            };

        private readonly ITransformApplicator _transformApplicator;

        private List<AddPropertyTransform<PackOptions>> _propertyTransforms;

        public MigratePackOptionsRule(ITransformApplicator transformApplicator = null)
        {
            _transformApplicator = transformApplicator ?? new TransformApplicator();

            ConstructTransformLists();
        }

        private void ConstructTransformLists()
        {
            _propertyTransforms = new List<AddPropertyTransform<PackOptions>>()
            {
                TagsTransform,
                ReleaseNotesTransform,
                IconUrlTransform,
                ProjectUrlTransform,
                LicenseUrlTransform,
                RequireLicenseAcceptanceTransform,
                RepositoryTypeTransform,
                RepositoryUrlTransform
            };
        }

        public void Apply(MigrationSettings migrationSettings, MigrationRuleInputs migrationRuleInputs)
        {
            var projectContext = migrationRuleInputs.DefaultProjectContext;
            var packOptions = projectContext.ProjectFile.PackOptions;

            TransformProperties(packOptions, migrationRuleInputs.CommonPropertyGroup);

            TransformPackFiles(packOptions, migrationRuleInputs.CommonItemGroup);
        }

        private void TransformProperties(PackOptions packOptions, ProjectPropertyGroupElement propertyGroup)
        {
            foreach (var propertyTransfrom in _propertyTransforms)
            {
                _transformApplicator.Execute(propertyTransfrom.Transform(packOptions), propertyGroup, true);
            }
        }

        private void TransformPackFiles(PackOptions packOptions, ProjectItemGroupElement itemGroup)
        {
            var transformResult = PackFilesTransform.Transform(packOptions.PackInclude);

            if (transformResult != null && transformResult.Any())
            {
                _transformApplicator.Execute(
                    transformResult,
                    itemGroup,
                    mergeExisting: true);
            }
        }

        private string ConvertTargetPathToMsbuildMetadata(string targetPath)
        {
            var targetIsDirectory = PathIsDirectory(targetPath);

            if (targetIsDirectory)
            {
                return targetPath;
            }

            return Path.GetDirectoryName(targetPath);
        }

        private bool PathIsDirectory(string targetPath)
        {
            var normalizedTargetPath = PathUtility.GetPathWithDirectorySeparator(targetPath);

            return normalizedTargetPath[normalizedTargetPath.Length - 1] == Path.DirectorySeparatorChar;
        }
    }
}