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

namespace Microsoft.DotNet.Migration.Tests
{
    public class GivenThatIWantToMigrateProjectDependencies : TestBase
    {
        // Workaround For P2P dependencies
        // ISSUE: https://github.com/dotnet/sdk/issues/73
        [Fact]
        public void If_a_project_dependency_is_present_DesignTimeAutoUnify_and_AutoUnify_are_present()
        {
            var solutionDirectory =
                TestAssetsManager.CreateTestInstance("TestAppWithLibrary", callingMethod: "p").WithLockFiles().Path;

            var appDirectory = Path.Combine(solutionDirectory, "TestApp");
            var libDirectory = Path.Combine(solutionDirectory, "TestLibrary");

            var projectContext = ProjectContext.Create(appDirectory, FrameworkConstants.CommonFrameworks.NetCoreApp10);
            var mockProj = ProjectRootElement.Create();
            var testSettings = new MigrationSettings(appDirectory, appDirectory, "1.0.0", mockProj);
            var testInputs = new MigrationRuleInputs(new[] {projectContext}, mockProj, mockProj.AddItemGroup(),
                mockProj.AddPropertyGroup());
            new MigrateProjectDependenciesRule().Apply(testSettings, testInputs);

            var autoUnify = mockProj.Properties.Where(p => p.Name == "AutoUnify");
            autoUnify.Count().Should().Be(1);
            autoUnify.First().Value.Should().Be("true");

            var designTimeAutoUnify = mockProj.Properties.Where(p => p.Name == "DesignTimeAutoUnify");
            designTimeAutoUnify.Count().Should().Be(1);
            designTimeAutoUnify.First().Value.Should().Be("true");
        }

        [Fact]
        public void Project_dependencies_are_migrated_to_ProjectReference()
        {
            var solutionDirectory =
                TestAssetsManager.CreateTestInstance("TestAppWithLibrary", callingMethod: "p").WithLockFiles().Path;

            var appDirectory = Path.Combine(solutionDirectory, "TestApp");

            var projectContext = ProjectContext.Create(appDirectory, FrameworkConstants.CommonFrameworks.NetCoreApp10);
            var mockProj = ProjectRootElement.Create();
            var testSettings = new MigrationSettings(appDirectory, appDirectory, "1.0.0", mockProj);
            var testInputs = new MigrationRuleInputs(new[] {projectContext}, mockProj, mockProj.AddItemGroup(),
                mockProj.AddPropertyGroup());
            new MigrateProjectDependenciesRule().Apply(testSettings, testInputs);

            var projectReferences = mockProj.Items.Where(item => item.ItemType.Equals("ProjectReference", StringComparison.Ordinal));
            projectReferences.Count().Should().Be(1);

            projectReferences.First().Include.Should().Be(Path.Combine("..", "TestLibrary", "TestLibrary.csproj"));
        }

        [Fact]
        public void It_throws_when_project_dependency_is_unresolved()
        {
            // No Lock file => unresolved
            var solutionDirectory =
                TestAssetsManager.CreateTestInstance("TestAppWithLibrary").Path;

            var appDirectory = Path.Combine(solutionDirectory, "TestApp");

            var projectContext = ProjectContext.Create(appDirectory, FrameworkConstants.CommonFrameworks.NetCoreApp10);
            var mockProj = ProjectRootElement.Create();
            var testSettings = new MigrationSettings(appDirectory, appDirectory, "1.0.0", mockProj);
            var testInputs = new MigrationRuleInputs(new[] {projectContext}, mockProj, mockProj.AddItemGroup(), mockProj.AddPropertyGroup());

            Action action = () => new MigrateProjectDependenciesRule().Apply(testSettings, testInputs);
            action.ShouldThrow<Exception>()
                .Where(e => e.Message.Contains("MIGRATE1014::Unresolved Dependency: Unresolved project dependency (TestLibrary)"));
        }
    }
}
