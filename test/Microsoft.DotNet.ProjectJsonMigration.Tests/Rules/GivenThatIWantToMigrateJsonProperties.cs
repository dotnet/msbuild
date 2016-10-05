// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.DotNet.Tools.Test.Utilities;
using FluentAssertions;
using Microsoft.DotNet.ProjectJsonMigration.Rules;
using Xunit;

namespace Microsoft.DotNet.ProjectJsonMigration.Tests
{
    public class GivenThatIWantToMigrateJsonProperties : TestBase
    {
        [Fact]
        public void It_does_not_migrate_missing_props()
        {
            var mockProj = RunPropertiesRuleOnPj(@"
                {}");

            mockProj.Properties.Count().Should().Be(0);
        }

        [Fact]
        public void It_migrates_userSecretsId()
        {
            var mockProj = RunPropertiesRuleOnPj(@"
                {
                    ""userSecretsId"": ""XYZ""             
                }");

            mockProj.Properties.Count(p => p.Name == "UserSecretsId").Should().Be(1);
            mockProj.Properties.First(p => p.Name == "UserSecretsId").Value.Should().Be("XYZ");
        }

        [Fact]
        public void It_migrates_empty_userSecretsId()
        {
            var mockProj = RunPropertiesRuleOnPj(@"
                {
                    ""userSecretsId"": """"             
                }");

            mockProj.Properties.Count(p => p.Name == "UserSecretsId").Should().Be(0);
        }

        private ProjectRootElement RunPropertiesRuleOnPj(string project, string testDirectory = null)
        {
            testDirectory = testDirectory ?? Temp.CreateDirectory().Path;
            return TemporaryProjectFileRuleRunner.RunRules(new IMigrationRule[]
            {
                new MigrateJsonPropertiesRule()
            }, project, testDirectory);
        }
    }
}