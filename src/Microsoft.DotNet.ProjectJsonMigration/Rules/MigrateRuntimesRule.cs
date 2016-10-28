// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.ProjectJsonMigration.Transforms;

namespace Microsoft.DotNet.ProjectJsonMigration.Rules
{
    internal class MigrateRuntimesRule : IMigrationRule
    {
        AddPropertyTransform<IList<string>> RuntimeIdentifiersTransform =>
            new AddPropertyTransform<IList<string>>(
                "RuntimeIdentifiers",
                l => String.Join(";", l),
                l => l.Count > 0);

        private readonly ITransformApplicator _transformApplicator;

        public MigrateRuntimesRule(ITransformApplicator transformApplicator = null)
        {
            _transformApplicator = transformApplicator ?? new TransformApplicator();
        }

        public void Apply(MigrationSettings migrationSettings, MigrationRuleInputs migrationRuleInputs)
        {
            var propertyGroup = migrationRuleInputs.CommonPropertyGroup;

            _transformApplicator.Execute(
                RuntimeIdentifiersTransform.Transform(migrationRuleInputs.DefaultProjectContext.ProjectFile.Runtimes),
                propertyGroup,
                mergeExisting: true);
        }
    }
}
