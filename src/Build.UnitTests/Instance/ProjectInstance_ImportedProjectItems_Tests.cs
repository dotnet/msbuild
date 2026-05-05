// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using Microsoft.Build.Unittest;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.OM.Instance
{
    /// <summary>
    /// Tests for the MSBuildImportedProject items synthesized from the import closure.
    /// </summary>
    public class ProjectInstance_ImportedProjectItems_Tests
    {
        private readonly ITestOutputHelper _output;

        public ProjectInstance_ImportedProjectItems_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ImportedProjectItemsNotCreatedWithoutOptIn()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            string importContent = "<Project />";
            var importFile = env.CreateFile("import.targets", importContent);

            string projectContent = $"""
                <Project>
                    <Import Project="{importFile.Path}" />
                </Project>
                """;
            var projectFile = env.CreateFile("test.proj", projectContent);

            using var collection = new ProjectCollection();
            var project = new Project(projectFile.Path, globalProperties: null, toolsVersion: null, collection);
            ProjectInstance instance = project.CreateProjectInstance();

            instance.GetItems("MSBuildImportedProject").Count.ShouldBe(0);
        }

        [Fact]
        public void ImportedProjectItemsCreatedWhenPropertyIsSet()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            string importContent = "<Project />";
            var importFile = env.CreateFile("import.targets", importContent);

            string projectContent = $"""
                <Project>
                    <PropertyGroup>
                        <MSBuildProvideImportedProjects>true</MSBuildProvideImportedProjects>
                    </PropertyGroup>
                    <Import Project="{importFile.Path}" />
                </Project>
                """;
            var projectFile = env.CreateFile("test.proj", projectContent);

            using var collection = new ProjectCollection();
            var project = new Project(projectFile.Path, globalProperties: null, toolsVersion: null, collection);
            ProjectInstance instance = project.CreateProjectInstance();

            var items = instance.GetItems("MSBuildImportedProject").ToList();
            items.Count.ShouldBe(1);
            items[0].EvaluatedInclude.ShouldBe(importFile.Path);
            items[0].GetMetadataValue("ImportingProjectPath").ShouldBe(projectFile.Path);
            items[0].GetMetadataValue("Sdk").ShouldBeEmpty();
        }

        [Fact]
        public void ImportedProjectItemsHaveCorrectImportingPath()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            string import2Content = "<Project />";
            var import2File = env.CreateFile("import2.targets", import2Content);

            string import1Content = $"""
                <Project>
                    <Import Project="{import2File.Path}" />
                </Project>
                """;
            var import1File = env.CreateFile("import1.targets", import1Content);

            string projectContent = $"""
                <Project>
                    <PropertyGroup>
                        <MSBuildProvideImportedProjects>true</MSBuildProvideImportedProjects>
                    </PropertyGroup>
                    <Import Project="{import1File.Path}" />
                </Project>
                """;
            var projectFile = env.CreateFile("test.proj", projectContent);

            using var collection = new ProjectCollection();
            var project = new Project(projectFile.Path, globalProperties: null, toolsVersion: null, collection);
            ProjectInstance instance = project.CreateProjectInstance();

            var items = instance.GetItems("MSBuildImportedProject").ToList();
            items.Count.ShouldBe(2);

            // project -> import1
            var item1 = items.First(i => i.EvaluatedInclude == import1File.Path);
            item1.GetMetadataValue("ImportingProjectPath").ShouldBe(projectFile.Path);

            // import1 -> import2
            var item2 = items.First(i => i.EvaluatedInclude == import2File.Path);
            item2.GetMetadataValue("ImportingProjectPath").ShouldBe(import1File.Path);
        }

        [Fact]
        public void ImportedProjectItemsExcludeRootProject()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            string projectContent = """
                <Project>
                    <PropertyGroup>
                        <MSBuildProvideImportedProjects>true</MSBuildProvideImportedProjects>
                    </PropertyGroup>
                </Project>
                """;
            var projectFile = env.CreateFile("test.proj", projectContent);

            using var collection = new ProjectCollection();
            var project = new Project(projectFile.Path, globalProperties: null, toolsVersion: null, collection);
            ProjectInstance instance = project.CreateProjectInstance();

            instance.GetItems("MSBuildImportedProject").Count.ShouldBe(0);
        }

        [Fact]
        public void ImportedProjectItemsAvailableToTargets()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            string importContent = "<Project />";
            var importFile = env.CreateFile("import.targets", importContent);

            string projectContent = $"""
                <Project>
                    <PropertyGroup>
                        <MSBuildProvideImportedProjects>true</MSBuildProvideImportedProjects>
                    </PropertyGroup>
                    <Import Project="{importFile.Path}" />
                    <Target Name="ShowImports">
                        <Message Text="Import: %(MSBuildImportedProject.Identity) from %(MSBuildImportedProject.ImportingProjectPath)" Importance="High" />
                    </Target>
                </Project>
                """;
            var projectFile = env.CreateFile("test.proj", projectContent);

            using var collection = new ProjectCollection();
            var project = new Project(projectFile.Path, globalProperties: null, toolsVersion: null, collection);
            ProjectInstance instance = project.CreateProjectInstance();

            var mockLogger = new MockLogger(_output);
            instance.Build(["ShowImports"], [mockLogger]).ShouldBeTrue();
            mockLogger.AssertLogContains($"Import: {importFile.Path} from {projectFile.Path}");
        }

        [Fact]
        public void ImportedProjectItemsHaveSdkMetadataForSdkImports()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            string testSdkDirectory = env.CreateFolder().Path;
            File.WriteAllText(Path.Combine(testSdkDirectory, "Sdk.props"), "<Project />");
            File.WriteAllText(Path.Combine(testSdkDirectory, "Sdk.targets"), "<Project />");

            var projectOptions = SdkUtilities.CreateProjectOptionsWithResolver(
                new SdkUtilities.FileBasedMockSdkResolver(new Dictionary<string, string>
                {
                    { "MyTestSdk", testSdkDirectory },
                }));

            string projectContent = """
                <Project Sdk='MyTestSdk'>
                    <PropertyGroup>
                        <MSBuildProvideImportedProjects>true</MSBuildProvideImportedProjects>
                    </PropertyGroup>
                </Project>
                """;

            using ProjectRootElementFromString projectRootElementFromString = new(projectContent);
            Project project = Project.FromProjectRootElement(
                projectRootElementFromString.Project,
                projectOptions);
            ProjectInstance instance = project.CreateProjectInstance();

            var items = instance.GetItems("MSBuildImportedProject").ToList();
            items.Count.ShouldBe(2); // Sdk.props and Sdk.targets

            // Both should have Sdk metadata set to "MyTestSdk"
            foreach (var item in items)
            {
                item.GetMetadataValue("Sdk").ShouldBe("MyTestSdk");
            }
        }
    }
}
