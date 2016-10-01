// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.ProjectJsonMigration.Transforms;

namespace Microsoft.DotNet.ProjectJsonMigration.Rules
{
    public class WorkaroundOptionsRule : IMigrationRule
    {
        private readonly ITransformApplicator _transformApplicator;

        private AddPropertyTransform<object> ProjectLockFileTransform =>
            new AddPropertyTransform<object>("ProjectLockFile",
                frameworks => "$(MSBuildProjectDirectory)/project.lock.json",
                frameworks => true);

        public WorkaroundOptionsRule(ITransformApplicator transformApplicator = null)
        {
            _transformApplicator = transformApplicator ?? new TransformApplicator();
        }

        public void Apply(MigrationSettings migrationSettings, MigrationRuleInputs migrationRuleInputs)
        {
            var propertyGroup = migrationRuleInputs.CommonPropertyGroup;

            _transformApplicator.Execute(ProjectLockFileTransform.Transform(string.Empty), propertyGroup);
        }
    }
}