// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.Build.Construction;
using Microsoft.DotNet.Internal.ProjectModel;
using Microsoft.DotNet.ProjectJsonMigration.Transforms;
using NuGet.Frameworks;
using System.Collections.Generic;

namespace Microsoft.DotNet.ProjectJsonMigration.Rules
{
    // TODO: Support Multi-TFM
    internal class MigrateTFMRule : IMigrationRule
    {
        private const string RuntimeIdentifiers =
            "win7-x64;win7-x86;osx.10.10-x64;osx.10.11-x64;ubuntu.14.04-x64;ubuntu.16.04-x64;centos.7-x64;rhel.7.2-x64;debian.8-x64;fedora.23-x64;opensuse.13.2-x64";

        private readonly ITransformApplicator _transformApplicator;

        public MigrateTFMRule(ITransformApplicator transformApplicator = null)
        {
            _transformApplicator = transformApplicator ?? new TransformApplicator();
        }

        public void Apply(MigrationSettings migrationSettings, MigrationRuleInputs migrationRuleInputs)
        {
            var csproj = migrationRuleInputs.OutputMSBuildProject;
            var propertyGroup = migrationRuleInputs.CommonPropertyGroup;

            CleanExistingProperties(csproj);
            CleanExistingPackageReferences(csproj);

            if(migrationRuleInputs.IsMultiTFM)
            {
                _transformApplicator.Execute(
                    FrameworksTransform.Transform(
                        migrationRuleInputs.ProjectContexts.Select(p => p.TargetFramework)),
                    propertyGroup,
                    mergeExisting: true);
            }
            else
            {
                _transformApplicator.Execute(
                    FrameworkTransform.Transform(
                        migrationRuleInputs.ProjectContexts.Single().TargetFramework),
                    propertyGroup,
                    mergeExisting: true);
            }

            _transformApplicator.Execute(
                    RuntimeIdentifiersTransform.Transform(migrationRuleInputs.ProjectContexts),
                    propertyGroup,
                    mergeExisting: true);
            _transformApplicator.Execute(
                    RuntimeIdentifierTransform.Transform(migrationRuleInputs.ProjectContexts),
                    propertyGroup,
                    mergeExisting: true);
        }

        private void CleanExistingProperties(ProjectRootElement csproj)
        {
            var existingPropertiesToRemove = new string[] 
            {
                "TargetFrameworkIdentifier",
                "TargetFrameworkVersion",
                "TargetFrameworks",
                "TargetFramework"
            };
            
            var properties = csproj.Properties.Where(p => existingPropertiesToRemove.Contains(p.Name));

            foreach (var property in properties)
            {
                property.Parent.RemoveChild(property);
            }
        }

        //TODO: this is a hack right now to clean packagerefs. This is not the final resting place for this piece of code
        // @brthor will move it to its final location as part of this PackageRef PR: https://github.com/dotnet/cli/pull/4261
        private void CleanExistingPackageReferences(ProjectRootElement outputMSBuildProject)
        {
            var packageRefs = outputMSBuildProject
                .Items
                .Where(i => i.ItemType == "PackageReference" && i.Include != PackageConstants.SdkPackageName)
                .ToList();

            foreach (var packageRef in packageRefs)
            {
                var parent = packageRef.Parent;
                packageRef.Parent.RemoveChild(packageRef);
                parent.RemoveIfEmpty();
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

        private AddPropertyTransform<IEnumerable<NuGetFramework>> FrameworksTransform =>
            new AddPropertyTransform<IEnumerable<NuGetFramework>>(
                "TargetFrameworks",
                frameworks => string.Join(";", frameworks.Select(f => f.GetShortFolderName())),
                frameworks => true);

        private AddPropertyTransform<NuGetFramework> FrameworkTransform =>
            new AddPropertyTransform<NuGetFramework>(
                "TargetFramework",
                framework => framework.GetShortFolderName(),
                framework => true);

        private AddPropertyTransform<IEnumerable<ProjectContext>> RuntimeIdentifiersTransform =>
            new AddPropertyTransform<IEnumerable<ProjectContext>>(
                "RuntimeIdentifiers",
                projectContexts => RuntimeIdentifiers,
                projectContexts => !projectContexts.HasRuntimes() &&
                                    projectContexts.HasBothCoreAndFullFrameworkTFMs());

        private AddPropertyTransform<IEnumerable<ProjectContext>> RuntimeIdentifierTransform =>
            new AddPropertyTransform<IEnumerable<ProjectContext>>(
                "RuntimeIdentifier",
                projectContexts => "win7-x86",
                projectContexts => !projectContexts.HasRuntimes() && projectContexts.HasFullFrameworkTFM())
            .WithMSBuildCondition(projectContexts =>
                {
                    string msBuildCondition = null;
                    if (projectContexts.HasBothCoreAndFullFrameworkTFMs())
                    {
                        msBuildCondition = string.Join(
                            " OR ",
                            projectContexts.Where(p => p.IsFullFramework()).Select(
                                p => $"'$(TargetFramework)' == '{p.TargetFramework.GetShortFolderName()}'"));

                        msBuildCondition = $" {msBuildCondition} ";
                    }

                    return msBuildCondition;
                });
    }
}
