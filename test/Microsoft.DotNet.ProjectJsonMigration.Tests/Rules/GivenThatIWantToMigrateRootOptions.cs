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
    public class GivenThatIWantToMigrateRootOptions : TestBase
    {
        [Fact]
        public void It_migrates_authors()
        {
            var mockProj = RunPropertiesRuleOnPj(@"
                {
                    ""authors"": [ ""Some author"", ""Some other author"" ]
                }");

            mockProj.Properties.Count(p => p.Name == "Authors").Should().Be(1);
            mockProj.Properties.First(p => p.Name == "Authors").Value.Should().Be(
                "Some author;Some other author");
        }
        
        private ProjectRootElement RunPropertiesRuleOnPj(string project, string testDirectory = null)
        {
            testDirectory = testDirectory ?? Temp.CreateDirectory().Path;
            return TemporaryProjectFileRuleRunner.RunRules(new IMigrationRule[]
            {
                new MigrateRootOptionsRule()
            }, project, testDirectory);
        }
    }
}