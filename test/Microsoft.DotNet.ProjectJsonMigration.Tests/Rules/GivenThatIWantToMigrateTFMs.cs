using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectJsonMigration;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Test.Utilities;
using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.DotNet.ProjectJsonMigration.Rules;

namespace Microsoft.DotNet.ProjectJsonMigration.Tests
{
    public class GivenThatIWantToMigrateProjectFramework : TestBase
    {
        [Fact]
        public void Migrating_netcoreapp_project_Populates_TargetFrameworkIdentifier_and_TargetFrameworkVersion()
        {
            var testDirectory = Temp.CreateDirectory().Path;
            var testPJ = new ProjectJsonBuilder(TestAssetsManager)
                .FromTestAssetBase("TestAppWithRuntimeOptions")
                .WithCustomProperty("buildOptions", new Dictionary<string, string>
                {
                    { "emitEntryPoint", "false" }
                })
                .SaveToDisk(testDirectory);

            var projectContext = ProjectContext.Create(testDirectory, FrameworkConstants.CommonFrameworks.NetCoreApp10);
            var mockProj = ProjectRootElement.Create();

            // Run BuildOptionsRule
            var testSettings = new MigrationSettings(testDirectory, testDirectory, "1.0.0", mockProj);
            var testInputs = new MigrationRuleInputs(new[] { projectContext }, mockProj, mockProj.AddItemGroup(), mockProj.AddPropertyGroup());
            new MigrateTFMRule().Apply(testSettings, testInputs);

            mockProj.Properties.Count(p => p.Name == "TargetFrameworkIdentifier").Should().Be(1);
            mockProj.Properties.Count(p => p.Name == "TargetFrameworkVersion").Should().Be(1);
            mockProj.Properties.First(p => p.Name == "TargetFrameworkIdentifier").Value.Should().Be(".NETCoreApp");
            mockProj.Properties.First(p => p.Name == "TargetFrameworkVersion").Value.Should().Be("v1.0");
        }
    }
}
