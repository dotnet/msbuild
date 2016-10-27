using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectJsonMigration;
using Microsoft.DotNet.Internal.ProjectModel;
using Microsoft.DotNet.TestFramework;
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
    public class GivenThatIWantToMigrateProjectDependencies : TestBase
    {
        [Fact]
        public void Project_dependencies_are_migrated_to_ProjectReference()
        {
            var solutionDirectory =
                TestAssetsManager.CreateTestInstance("TestAppWithLibrary", callingMethod: "p").Path;

            var appDirectory = Path.Combine(solutionDirectory, "TestApp");

            var projectContext = ProjectContext.Create(appDirectory, FrameworkConstants.CommonFrameworks.NetCoreApp10);
            var mockProj = ProjectRootElement.Create();
            var testSettings = new MigrationSettings(appDirectory, appDirectory, "1.0.0", mockProj, null);
            var testInputs = new MigrationRuleInputs(new[] {projectContext}, mockProj, mockProj.AddItemGroup(),
                mockProj.AddPropertyGroup());
            new MigrateProjectDependenciesRule().Apply(testSettings, testInputs);

            var projectReferences = mockProj.Items.Where(item => item.ItemType.Equals("ProjectReference", StringComparison.Ordinal));
            projectReferences.Count().Should().Be(1);

            var projectReference = projectReferences.First();
            projectReference.Include.Should().Be(Path.Combine("..", "TestLibrary", "TestLibrary.csproj"));
            projectReference.Parent.Condition.Should().BeEmpty();
        }

        [Fact]
        public void It_does_not_migrate_a_dependency_with_target_package_that_has_a_matching_project_as_a_ProjectReference()
        {
            var testAssetsManager = GetTestGroupTestAssetsManager("NonRestoredTestProjects");
            var solutionDirectory =
                testAssetsManager.CreateTestInstance("AppWithProjectDependencyAsTarget", callingMethod: "p").Path;

            var appDirectory = Path.Combine(solutionDirectory, "TestApp");

            var projectContext = ProjectContext.Create(appDirectory, FrameworkConstants.CommonFrameworks.NetCoreApp10);
            var mockProj = ProjectRootElement.Create();
            var testSettings = new MigrationSettings(appDirectory, appDirectory, "1.0.0", mockProj, null);
            var testInputs = new MigrationRuleInputs(new[] {projectContext}, mockProj, mockProj.AddItemGroup(),
                mockProj.AddPropertyGroup());
            new MigrateProjectDependenciesRule().Apply(testSettings, testInputs);

            var projectReferences = mockProj.Items.Where(
                item => item.ItemType.Equals("ProjectReference", StringComparison.Ordinal));
            projectReferences.Should().BeEmpty();
        }

        [Fact]
        public void TFM_specific_Project_dependencies_are_migrated_to_ProjectReference_under_condition_ItemGroup()
        {
            var solutionDirectory =
                TestAssetsManager.CreateTestInstance("TestAppWithLibraryUnderTFM", callingMethod: "p").Path;

            var appDirectory = Path.Combine(solutionDirectory, "TestApp");

            var projectContext = ProjectContext.Create(appDirectory, FrameworkConstants.CommonFrameworks.NetCoreApp10);
            var mockProj = ProjectRootElement.Create();
            var testSettings = new MigrationSettings(appDirectory, appDirectory, "1.0.0", mockProj, null);
            var testInputs = new MigrationRuleInputs(new[] {projectContext}, mockProj, mockProj.AddItemGroup(),
                mockProj.AddPropertyGroup());
            new MigrateProjectDependenciesRule().Apply(testSettings, testInputs);

            var projectReferences = mockProj.Items.Where(item => item.ItemType.Equals("ProjectReference", StringComparison.Ordinal));
            projectReferences.Count().Should().Be(1);

            var projectReference = projectReferences.First();
            projectReference.Include.Should().Be(Path.Combine("..", "TestLibrary", "TestLibrary.csproj"));
            projectReference.Parent.Condition.Should().Be(" '$(TargetFramework)' == 'netcoreapp1.0' ");
        }

        [Fact]
        public void It_throws_when_project_dependency_is_unresolved()
        {
            // No Lock file => unresolved
            var solutionDirectory =
                TestAssetsManager.CreateTestInstance("TestAppWithLibrary").Path;

            var appDirectory = Path.Combine(solutionDirectory, "TestApp");
            var libraryDirectory = Path.Combine(solutionDirectory, "TestLibrary");
            Directory.Delete(libraryDirectory, true);

            var projectContext = ProjectContext.Create(appDirectory, FrameworkConstants.CommonFrameworks.NetCoreApp10);
            var mockProj = ProjectRootElement.Create();
            var testSettings = new MigrationSettings(appDirectory, appDirectory, "1.0.0", mockProj);
            var testInputs = new MigrationRuleInputs(new[] {projectContext}, mockProj, mockProj.AddItemGroup(), mockProj.AddPropertyGroup());

            Action action = () => new MigrateProjectDependenciesRule().Apply(testSettings, testInputs);
            action.ShouldThrow<Exception>()
                .Where(e => e.Message.Contains("MIGRATE1014::Unresolved Dependency: Unresolved project dependency (TestLibrary)"));
        }

        [Theory]
        [InlineData(@"some/path/to.cSproj", new [] { @"some/path/to.cSproj" })]
        [InlineData(@"to.CSPROJ",new [] { @"to.CSPROJ" })]
        public void It_migrates_csproj_ProjectReference_in_xproj(string projectReference, string[] expectedMigratedReferences)
        {
            var xproj = ProjectRootElement.Create();
            xproj.AddItem("ProjectReference", projectReference);

            var projectReferenceName = Path.GetFileNameWithoutExtension(projectReference);

            var projectJson = @"
                {
                    ""dependencies"": {" +
                        $"\"{projectReferenceName}\"" + @": {
                            ""target"" : ""project""
                        }
                    }
                }
            ";

            var testDirectory = Temp.CreateDirectory().Path;
            var migratedProj = TemporaryProjectFileRuleRunner.RunRules(new IMigrationRule[]
                {
                    new MigrateProjectDependenciesRule()
                }, projectJson, testDirectory, xproj);

            var migratedProjectReferenceItems = migratedProj.Items.Where(i => i.ItemType == "ProjectReference");
            migratedProjectReferenceItems.Should().HaveCount(expectedMigratedReferences.Length);
            migratedProjectReferenceItems.Select(m => m.Include).Should().BeEquivalentTo(expectedMigratedReferences);
        }

        [Fact]
        public void It_migrates_csproj_ProjectReference_in_xproj_including_condition_on_ProjectReference()
        {
            var projectReference = "some/to.csproj";
            var xproj = ProjectRootElement.Create();
            var csprojReferenceItem = xproj.AddItem("ProjectReference", projectReference);
            csprojReferenceItem.Condition = " '$(Foo)' == 'bar' ";

            var projectReferenceName = Path.GetFileNameWithoutExtension(projectReference);

            var projectJson = @"
                {
                    ""dependencies"": {" +
                        $"\"{projectReferenceName}\"" + @": {
                            ""target"" : ""project""
                        }
                    }
                }
            ";

            var testDirectory = Temp.CreateDirectory().Path;
            var migratedProj = TemporaryProjectFileRuleRunner.RunRules(new IMigrationRule[]
                {
                    new MigrateProjectDependenciesRule()
                }, projectJson, testDirectory, xproj);

            var migratedProjectReferenceItems = migratedProj.Items.Where(i => i.ItemType == "ProjectReference");
            migratedProjectReferenceItems.Should().HaveCount(1);

            var migratedProjectReferenceItem = migratedProjectReferenceItems.First();
            migratedProjectReferenceItem.Include.Should().Be(projectReference);
            migratedProjectReferenceItem.Condition.Should().Be(" '$(Foo)' == 'bar' ");
        }

        [Fact]
        public void It_migrates_csproj_ProjectReference_in_xproj_including_condition_on_ProjectReference_parent()
        {
            var projectReference = "some/to.csproj";
            var xproj = ProjectRootElement.Create();
            var csprojReferenceItem = xproj.AddItem("ProjectReference", projectReference);
            csprojReferenceItem.Parent.Condition = " '$(Foo)' == 'bar' ";

            var projectReferenceName = Path.GetFileNameWithoutExtension(projectReference);

            var projectJson = @"
                {
                    ""dependencies"": {" +
                        $"\"{projectReferenceName}\"" + @": {
                            ""target"" : ""project""
                        }
                    }
                }
            ";

            var testDirectory = Temp.CreateDirectory().Path;
            var migratedProj = TemporaryProjectFileRuleRunner.RunRules(new IMigrationRule[]
                {
                    new MigrateProjectDependenciesRule()
                }, projectJson, testDirectory, xproj);

            var migratedProjectReferenceItems = migratedProj.Items.Where(i => i.ItemType == "ProjectReference");
            migratedProjectReferenceItems.Should().HaveCount(1);

            var migratedProjectReferenceItem = migratedProjectReferenceItems.First();
            migratedProjectReferenceItem.Include.Should().Be(projectReference);
            migratedProjectReferenceItem.Condition.Should().Be(" '$(Foo)' == 'bar' ");
        }

        [Fact]
        public void It_migrates_csproj_ProjectReference_in_xproj_including_condition_on_ProjectReference_parent_and_item()
        {
            var projectReference = "some/to.csproj";
            var xproj = ProjectRootElement.Create();
            var csprojReferenceItem = xproj.AddItem("ProjectReference", projectReference);
            csprojReferenceItem.Parent.Condition = " '$(Foo)' == 'bar' ";
            csprojReferenceItem.Condition = " '$(Bar)' == 'foo' ";

            var projectReferenceName = Path.GetFileNameWithoutExtension(projectReference);

            var projectJson = @"
                {
                    ""dependencies"": {" +
                        $"\"{projectReferenceName}\"" + @": {
                            ""target"" : ""project""
                        }
                    }
                }
            ";

            var testDirectory = Temp.CreateDirectory().Path;
            var migratedProj = TemporaryProjectFileRuleRunner.RunRules(new IMigrationRule[]
                {
                    new MigrateProjectDependenciesRule()
                }, projectJson, testDirectory, xproj);

            var migratedProjectReferenceItems = migratedProj.Items.Where(i => i.ItemType == "ProjectReference");
            migratedProjectReferenceItems.Should().HaveCount(1);

            var migratedProjectReferenceItem = migratedProjectReferenceItems.First();
            migratedProjectReferenceItem.Include.Should().Be(projectReference);
            migratedProjectReferenceItem.Condition.Should().Be(" '$(Bar)' == 'foo'  and  '$(Foo)' == 'bar' ");
        }

        [Fact]
        public void It_promotes_P2P_references_up_in_the_dependency_chain()
        {
            var solutionDirectory =
                TestAssetsManager.CreateTestInstance("TestAppDependencyGraph", callingMethod: "p").Path;

            var appDirectory = Path.Combine(solutionDirectory, "ProjectA");

            var projectContext = ProjectContext.Create(appDirectory, FrameworkConstants.CommonFrameworks.NetCoreApp10);
            var mockProj = ProjectRootElement.Create();
            var testSettings = new MigrationSettings(appDirectory, appDirectory, "1.0.0", mockProj, null);
            var testInputs = new MigrationRuleInputs(new[] {projectContext}, mockProj, mockProj.AddItemGroup(),
                mockProj.AddPropertyGroup());
            new MigrateProjectDependenciesRule().Apply(testSettings, testInputs);

            var projectReferences = mockProj.Items.Where(
                item => item.ItemType.Equals("ProjectReference", StringComparison.Ordinal));
            projectReferences.Count().Should().Be(7);
        }
    }
}
