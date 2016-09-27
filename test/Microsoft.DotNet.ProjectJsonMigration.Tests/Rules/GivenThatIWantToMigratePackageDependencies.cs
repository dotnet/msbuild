// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Construction;
using Microsoft.DotNet.Tools.Test.Utilities;
using System.Linq;
using Xunit;
using FluentAssertions;
using Microsoft.DotNet.ProjectJsonMigration.Rules;
using System;

namespace Microsoft.DotNet.ProjectJsonMigration.Tests
{
    public class GivenThatIWantToMigratePackageDependencies : TestBase
    {
        [Fact]
        public void It_migrates_basic_PackageReference()
        {
            var mockProj = RunPackageDependenciesRuleOnPj(@"                
                {
                    ""dependencies"": {
                        ""APackage"" : ""1.0.0-preview"",
                        ""BPackage"" : ""1.0.0""
                    }
                }");

            Console.WriteLine(mockProj.RawXml);
            
            EmitsPackageReferences(mockProj, Tuple.Create("APackage", "1.0.0-preview", ""), Tuple.Create("BPackage", "1.0.0", ""));            
        }

        private void EmitsPackageReferences(ProjectRootElement mockProj, params Tuple<string, string, string>[] packageSpecs)
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
                    .Where(i => i.GetMetadataWithName("Version").Value == packageVersion);

                items.Should().HaveCount(1);
            }
        }

        private ProjectRootElement RunPackageDependenciesRuleOnPj(string s, string testDirectory = null)
        {
            testDirectory = testDirectory ?? Temp.CreateDirectory().Path;
            return TemporaryProjectFileRuleRunner.RunRules(new IMigrationRule[]
            {
                new MigratePackageDependenciesRule()
            }, s, testDirectory);
        }
    }
}