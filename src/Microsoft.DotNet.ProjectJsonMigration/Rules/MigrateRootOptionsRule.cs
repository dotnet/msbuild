// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.DotNet.ProjectJsonMigration.Transforms;
using Project = Microsoft.DotNet.Internal.ProjectModel.Project;

namespace Microsoft.DotNet.ProjectJsonMigration.Rules
{
    internal class MigrateRootOptionsRule : IMigrationRule
    {
        private readonly ITransformApplicator _transformApplicator;
        private readonly AddPropertyTransform<Project>[] _transforms;

        public MigrateRootOptionsRule(ITransformApplicator transformApplicator = null)
        {
            _transformApplicator = transformApplicator ?? new TransformApplicator();

            _transforms = new[]
            {
                DescriptionTransform,
                CopyrightTransform,
                TitleTransform,
                LanguageTransform,
                VersionTransform
            };
        }

        public void Apply(MigrationSettings migrationSettings, MigrationRuleInputs migrationRuleInputs)
        {
            var projectContext = migrationRuleInputs.DefaultProjectContext;

            var transformResults = _transforms.Select(t => t.Transform(projectContext.ProjectFile)).ToArray();
            if (transformResults.Any())
            {
                var propertyGroup = migrationRuleInputs.CommonPropertyGroup;

                foreach (var transformResult in transformResults)
                {
                    _transformApplicator.Execute(transformResult, propertyGroup, true);
                }
            }
        }
        
        private AddPropertyTransform<Project> DescriptionTransform => new AddPropertyTransform<Project>("Description",
            project => project.Description,
            project => !string.IsNullOrEmpty(project.Description));

        private AddPropertyTransform<Project> CopyrightTransform => new AddPropertyTransform<Project>("Copyright",
            project => project.Copyright,
            project => !string.IsNullOrEmpty(project.Copyright));

        private AddPropertyTransform<Project> TitleTransform => new AddPropertyTransform<Project>("AssemblyTitle",
            project => project.Title,
            project => !string.IsNullOrEmpty(project.Title));

        private AddPropertyTransform<Project> LanguageTransform => new AddPropertyTransform<Project>("NeutralLanguage",
            project => project.Language,
            project => !string.IsNullOrEmpty(project.Language));

        private AddPropertyTransform<Project> VersionTransform => new AddPropertyTransform<Project>("VersionPrefix",
            project => project.Version.ToString(), p => true);
    }
}
