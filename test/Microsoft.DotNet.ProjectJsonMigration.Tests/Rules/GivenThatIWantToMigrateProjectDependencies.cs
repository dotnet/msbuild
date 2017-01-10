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
        public void ProjectDependenciesAreMigratedToProjectReference()
        {
            var solutionDirectory =
                TestAssetsManager.CreateTestInstance("TestAppWithLibrary", callingMethod: "p").Path;

            var appDirectory = Path.Combine(solutionDirectory, "TestApp");

            var projectContext = ProjectContext.Create(appDirectory, FrameworkConstants.CommonFrameworks.NetCoreApp10);
            var mockProj = ProjectRootElement.Create();
            var testSettings = MigrationSettings.CreateMigrationSettingsTestHook(appDirectory, appDirectory, mockProj, null);
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
        public void ItDoesNotMigrateADependencyWithTargetPackageThatHasAMatchingProjectAsAProjectReference()
        {
            var testAssetsManager = GetTestGroupTestAssetsManager("NonRestoredTestProjects");
            var solutionDirectory =
                testAssetsManager.CreateTestInstance("AppWithProjectDependencyAsTarget", callingMethod: "p").Path;

            var appDirectory = Path.Combine(solutionDirectory, "TestApp");

            var projectContext = ProjectContext.Create(appDirectory, FrameworkConstants.CommonFrameworks.NetCoreApp10);
            var mockProj = ProjectRootElement.Create();
            var testSettings = MigrationSettings.CreateMigrationSettingsTestHook(appDirectory, appDirectory, mockProj, null);
            var testInputs = new MigrationRuleInputs(new[] {projectContext}, mockProj, mockProj.AddItemGroup(),
                mockProj.AddPropertyGroup());
            new MigrateProjectDependenciesRule().Apply(testSettings, testInputs);

            var projectReferences = mockProj.Items.Where(
                item => item.ItemType.Equals("ProjectReference", StringComparison.Ordinal));
            projectReferences.Should().BeEmpty();
        }

        [Fact]
        public void TFMSpecificProjectDependenciesAreMigratedToProjectReferenceUnderConditionItemGroup()
        {
            var solutionDirectory =
                TestAssetsManager.CreateTestInstance("TestAppWithLibraryUnderTFM", callingMethod: "p").Path;

            var appDirectory = Path.Combine(solutionDirectory, "TestApp");

            var projectContext = ProjectContext.Create(appDirectory, FrameworkConstants.CommonFrameworks.NetCoreApp10);
            var mockProj = ProjectRootElement.Create();
            var testSettings = MigrationSettings.CreateMigrationSettingsTestHook(appDirectory, appDirectory, mockProj, null);
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
        public void ItThrowsWhenProjectDependencyIsUnresolved()
        {
            // No Lock file => unresolved
            var solutionDirectory =
                TestAssetsManager.CreateTestInstance("TestAppWithLibrary").Path;

            var appDirectory = Path.Combine(solutionDirectory, "TestApp");
            var libraryDirectory = Path.Combine(solutionDirectory, "TestLibrary");
            Directory.Delete(libraryDirectory, true);

            var projectContext = ProjectContext.Create(appDirectory, FrameworkConstants.CommonFrameworks.NetCoreApp10);
            var mockProj = ProjectRootElement.Create();
            var testSettings = MigrationSettings.CreateMigrationSettingsTestHook(appDirectory, appDirectory, mockProj);
            var testInputs = new MigrationRuleInputs(new[] {projectContext}, mockProj, mockProj.AddItemGroup(), mockProj.AddPropertyGroup());

            Action action = () => new MigrateProjectDependenciesRule().Apply(testSettings, testInputs);
            action.ShouldThrow<Exception>()
                .Where(e => e.Message.Contains("MIGRATE1014::Unresolved Dependency: Unresolved project dependency (TestLibrary)"));
        }

        [Theory]
        [InlineData(@"some/path/to.cSproj", new [] { @"some/path/to.cSproj" })]
        [InlineData(@"to.CSPROJ",new [] { @"to.CSPROJ" })]
        public void ItMigratesCsprojProjectReferenceInXproj(string projectReference, string[] expectedMigratedReferences)
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
        public void ItMigratesCsprojProjectReferenceInXprojIncludingConditionOnProjectReference()
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
        public void ItMigratesCsprojProjectReferenceInXprojIncludingConditionOnProjectReferenceParent()
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
        public void ItMigratesCsprojProjectReferenceInXprojIncludingConditionOnProjectReferenceParentAndItem()
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
        public void ItDoesNotPromoteP2PReferencesUpInTheDependencyChain()
        {
            var mockProj = MigrateProject("TestAppDependencyGraph", "ProjectA");

            var projectReferences = mockProj.Items.Where(
                item => item.ItemType.Equals("ProjectReference", StringComparison.Ordinal));
            projectReferences.Count().Should().Be(2);
        }

        [Fact]
        public void ItDoesNotPromoteFrameworkAssembliesFromP2PReferencesUpInTheDependencyChain()
        {
            var solutionDirectory = TestAssets.Get(TestAssetKinds.DesktopTestProjects, "TestAppWithFrameworkAssemblies")
                .CreateInstance()
                .WithSourceFiles().Root;

            var appDirectory = Path.Combine(solutionDirectory.FullName, "ProjectA");

            var projectContext = ProjectContext.Create(appDirectory, FrameworkConstants.CommonFrameworks.Net451);
            var mockProj = ProjectRootElement.Create();
            var testSettings = MigrationSettings.CreateMigrationSettingsTestHook(appDirectory, appDirectory, mockProj, null);
            var testInputs = new MigrationRuleInputs(new[] {projectContext}, mockProj, mockProj.AddItemGroup(),
                mockProj.AddPropertyGroup());
            new MigrateProjectDependenciesRule().Apply(testSettings, testInputs);

            var frameworkAssemblyReferences = mockProj.Items.Where(
                item => item.ItemType == "Reference" &&
                item.Include == "System.ComponentModel.DataAnnotations" &&
                item.Parent.Condition == " '$(TargetFramework)' == 'net451' ");
            frameworkAssemblyReferences.Count().Should().Be(0);
        }

        [Fact]
        public void NoP2PReferenceIsMarkedWithAFromP2PAttribute()
        {
            var expectedNonHoistedProjectReferences = new [] {
                Path.Combine("..", "ProjectB", "ProjectB.csproj"),
                Path.Combine("..", "ProjectC", "ProjectC.csproj")
            };

            var mockProj = MigrateProject("TestAppDependencyGraph", "ProjectA");

            var projectReferences = mockProj.Items
                .Where(item =>
                    item.ItemType == "ProjectReference");

            projectReferences.Should().HaveCount(c => c ==  2)
                .And.OnlyContain(item => item.GetMetadataWithName("FromP2P") == null);

            projectReferences.Select(i => i.Include).Should().BeEquivalentTo(expectedNonHoistedProjectReferences);
        }

        [Fact]
        public void ItMigratesUnqualifiedDependenciesAsProjectReferenceWhenAMatchingProjectIsFound()
        {
            var mockProj = MigrateProject("TestAppWithUnqualifiedDependencies", "ProjectA");
            var projectReferenceInclude = Path.Combine("..", "ProjectB", "ProjectB.csproj");            

            var projectReferences = mockProj.Items.Should().ContainSingle(
                item => item.ItemType == "ProjectReference" && item.Include == projectReferenceInclude);
        }

        private ProjectRootElement MigrateProject(string solution, string project)
        {
            return MigrateProject(solution, project, FrameworkConstants.CommonFrameworks.NetCoreApp10);
        }

        private ProjectRootElement MigrateProject(
            string solution,
            string project,
            NuGetFramework targetFramework)
        {
            var solutionDirectory =
                TestAssetsManager.CreateTestInstance(solution, callingMethod: "p").Path;

            var appDirectory = Path.Combine(solutionDirectory, project);

            var projectContext = ProjectContext.Create(appDirectory, targetFramework);
            var mockProj = ProjectRootElement.Create();
            var testSettings = MigrationSettings.CreateMigrationSettingsTestHook(appDirectory, appDirectory, mockProj, null);
            var testInputs = new MigrationRuleInputs(new[] {projectContext}, mockProj, mockProj.AddItemGroup(),
                mockProj.AddPropertyGroup());
            new MigrateProjectDependenciesRule().Apply(testSettings, testInputs);

            var s = mockProj.Items.Select(p => $"ItemType = {p.ItemType}, Include = {p.Include}");
            Console.WriteLine(string.Join(Environment.NewLine, s));

            return mockProj;
        }
    }
}
