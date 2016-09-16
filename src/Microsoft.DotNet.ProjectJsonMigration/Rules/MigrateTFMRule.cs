// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectJsonMigration.Transforms;
using NuGet.Frameworks;
using System.Collections.Generic;

namespace Microsoft.DotNet.ProjectJsonMigration.Rules
{
    // TODO: Support Multi-TFM
    public class MigrateTFMRule : IMigrationRule
    {
        private readonly ITransformApplicator _transformApplicator;
        private readonly AddPropertyTransform<NuGetFramework>[] _transforms;

        public MigrateTFMRule(ITransformApplicator transformApplicator = null)
        {
            _transformApplicator = transformApplicator ?? new TransformApplicator();

            _transforms = new AddPropertyTransform<NuGetFramework>[]
            {
                OutputPathTransform,
                FrameworkIdentifierTransform,
                FrameworkVersionTransform
            };
        }

        public void Apply(MigrationSettings migrationSettings, MigrationRuleInputs migrationRuleInputs)
        {
            var csproj = migrationRuleInputs.OutputMSBuildProject;
            var propertyGroup = migrationRuleInputs.CommonPropertyGroup;

            CleanExistingProperties(csproj);

            _transformApplicator.Execute(
                FrameworksTransform.Transform(migrationRuleInputs.ProjectContexts.Select(p => p.TargetFramework)),
                propertyGroup);

            foreach (var transform in _transforms)
            {
                _transformApplicator.Execute(
                    transform.Transform(migrationRuleInputs.DefaultProjectContext.TargetFramework),
                    propertyGroup);
            }
        }

        private void CleanExistingProperties(ProjectRootElement csproj)
        {
            var existingPropertiesToRemove = new string[] { "TargetFrameworkIdentifier", "TargetFrameworkVersion" };
            var properties = csproj.Properties.Where(p => existingPropertiesToRemove.Contains(p.Name));

            foreach (var property in properties)
            {
                property.Parent.RemoveChild(property);
            }
        }

        // Taken from private NuGet.Frameworks method
        // https://github.com/NuGet/NuGet.Client/blob/33b8f85a94b01f805f1e955f9b68992b297fad6e/src/NuGet.Core/NuGet.Frameworks/NuGetFramework.cs#L234
        private static string GetDisplayVersion(Version version)
        {
            var sb = new StringBuilder(String.Format(CultureInfo.InvariantCulture, "{0}.{1}", version.Major, version.Minor));

            if (version.Build > 0
                || version.Revision > 0)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, ".{0}", version.Build);

                if (version.Revision > 0)
                {
                    sb.AppendFormat(CultureInfo.InvariantCulture, ".{0}", version.Revision);
                }
            }

            return sb.ToString();
        }

        // TODO: When we have this inferred in the sdk targets, we won't need this
        private AddPropertyTransform<NuGetFramework> OutputPathTransform =>
            new AddPropertyTransform<NuGetFramework>("OutputPath",
                f => $"bin/$(Configuration)/{f.GetShortFolderName()}",
                f => true);

        private AddPropertyTransform<NuGetFramework> FrameworkIdentifierTransform =>
            new AddPropertyTransform<NuGetFramework>("TargetFrameworkIdentifier",
                f => f.Framework,
                f => true);

        private AddPropertyTransform<NuGetFramework> FrameworkVersionTransform =>
            new AddPropertyTransform<NuGetFramework>("TargetFrameworkVersion",
                f => "v" + GetDisplayVersion(f.Version),
                f => true);
        private AddPropertyTransform<IEnumerable<NuGetFramework>> FrameworksTransform =>
            new AddPropertyTransform<IEnumerable<NuGetFramework>>("TargetFrameworks",
                frameworks => string.Join(";", frameworks.Select(f => f.GetShortFolderName())),
                frameworks => true);
    }
}
