using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectJsonMigration;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Test.Utilities;
using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.DotNet.ProjectJsonMigration.Rules;

namespace Microsoft.DotNet.ProjectJsonMigration.Tests
{
    public class GivenThatIWantToMigrateRuntimes : TestBase
    {
        [Fact]
        public void It_migrates_runtimes()
        {
            var projectJson = @"
                {
                    ""runtimes"": {
                        ""win7-x64"": { },
                        ""win7-x86"": { },
                        ""osx.10.10-x64"": { }
                    }
                }
            ";

            var testDirectory = Temp.CreateDirectory().Path;
            var migratedProj = TemporaryProjectFileRuleRunner.RunRules(new IMigrationRule[]
                {
                    new MigrateRuntimesRule()
                }, projectJson, testDirectory);

            migratedProj.Properties.Count(p => p.Name == "RuntimeIdentifiers").Should().Be(1);
            migratedProj.Properties.First(p => p.Name == "RuntimeIdentifiers").Value
                .Should().Be("win7-x64;win7-x86;osx.10.10-x64");
        }

        [Fact]
        public void It_has_an_empty_runtime_node_to_migrate()
        {
            var projectJson = @"
                {
                    ""runtimes"": {
                    }
                }
            ";

            var testDirectory = Temp.CreateDirectory().Path;
            var migratedProj = TemporaryProjectFileRuleRunner.RunRules(new IMigrationRule[]
                {
                    new MigrateRuntimesRule()
                }, projectJson, testDirectory);

            migratedProj.Properties.Count(p => p.Name == "RuntimeIdentifiers").Should().Be(0);
        }

        [Fact]
        public void It_has_no_runtimes_to_migrate()
        {
            var projectJson = @"
                {
                }
            ";

            var testDirectory = Temp.CreateDirectory().Path;
            var migratedProj = TemporaryProjectFileRuleRunner.RunRules(new IMigrationRule[]
                {
                    new MigrateRuntimesRule()
                }, projectJson, testDirectory);

            migratedProj.Properties.Count(p => p.Name == "RuntimeIdentifiers").Should().Be(0);
        }
    }
}
