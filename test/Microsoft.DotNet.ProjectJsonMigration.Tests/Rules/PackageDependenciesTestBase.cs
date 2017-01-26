// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Construction;
using Microsoft.DotNet.Tools.Test.Utilities;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.ProjectJsonMigration.Rules;
using System;

namespace Microsoft.DotNet.ProjectJsonMigration.Tests
{
    public class PackageDependenciesTestBase : TestBase
    {
        protected void EmitsPackageReferences(ProjectRootElement mockProj, params Tuple<string, string, string>[] packageSpecs)
        {
            foreach (var packageSpec in packageSpecs)
            {
                var packageName = packageSpec.Item1;
                var packageVersion = packageSpec.Item2;
                var packageTFM = packageSpec.Item3;

                var items = mockProj.Items
                    .Where(i => i.ItemType == "PackageReference")
                    .Where(i => string.IsNullOrEmpty(packageTFM) || i.ConditionChain().Any(c => c.Contains(packageTFM)))
                    .Where(i => i.Include == packageName)
                    .Where(i => i.GetMetadataWithName("Version").Value == packageVersion &&
                                i.GetMetadataWithName("Version").ExpressedAsAttribute);

                items.Should().HaveCount(1);
            }
        }

        protected void EmitsToolReferences(ProjectRootElement mockProj, params Tuple<string, string>[] toolSpecs)
        {
            foreach (var toolSpec in toolSpecs)
            {
                var packageName = toolSpec.Item1;
                var packageVersion = toolSpec.Item2;

                var items = mockProj.Items
                    .Where(i => i.ItemType == "DotNetCliToolReference")
                    .Where(i => i.Include == packageName)
                    .Where(i => i.GetMetadataWithName("Version").Value == packageVersion &&
                                i.GetMetadataWithName("Version").ExpressedAsAttribute);

                items.Should().HaveCount(1);
            }
        }

        protected ProjectRootElement RunPackageDependenciesRuleOnPj(string s, string testDirectory = null)
        {
            testDirectory =
                testDirectory ??
                Temp.CreateDirectory().DirectoryInfo.CreateSubdirectory("project").FullName;

            return TemporaryProjectFileRuleRunner.RunRules(new IMigrationRule[]
            {
                new MigratePackageDependenciesAndToolsRule()
            }, s, testDirectory);
        }
    }
}