// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.BackEnd;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.OM.Instance
{
    /// <summary>
    /// Tests for the import edge feature: structured import graph data on ProjectInstance
    /// exposed to tasks via EngineServices.GetImportEdges().
    /// </summary>
    public class ProjectInstance_ImportEdges_Tests
    {
        private readonly ITestOutputHelper _output;

        public ProjectInstance_ImportEdges_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ImportEdgesArePopulatedFromEvaluation()
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
                    <Import Project="{import1File.Path}" />
                </Project>
                """;
            var projectFile = env.CreateFile("test.proj", projectContent);

            using var collection = new ProjectCollection();
            var project = new Project(projectFile.Path, globalProperties: null, toolsVersion: null, collection);
            ProjectInstance instance = project.CreateProjectInstance();

            // Should have the flat import paths
            instance.ImportPaths.ShouldContain(import1File.Path);
            instance.ImportPaths.ShouldContain(import2File.Path);

            // Should have structured import edges
            var edges = instance.ImportEdges;
            edges.ShouldNotBeNull();
            edges.Count.ShouldBe(2);

            // Edge: project -> import1
            var edge1 = edges.First(e => e.ImportedProjectPath == import1File.Path);
            edge1.ImportingProjectPath.ShouldBe(projectFile.Path);
            edge1.SdkName.ShouldBeNull();

            // Edge: import1 -> import2
            var edge2 = edges.First(e => e.ImportedProjectPath == import2File.Path);
            edge2.ImportingProjectPath.ShouldBe(import1File.Path);
            edge2.SdkName.ShouldBeNull();
        }

        [Fact]
        public void ImportEdgesExcludeRootProject()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            string projectContent = "<Project />";
            var projectFile = env.CreateFile("test.proj", projectContent);

            using var collection = new ProjectCollection();
            var project = new Project(projectFile.Path, globalProperties: null, toolsVersion: null, collection);
            ProjectInstance instance = project.CreateProjectInstance();

            // A project with no imports should have zero edges
            instance.ImportEdges.ShouldNotBeNull();
            instance.ImportEdges.Count.ShouldBe(0);
        }

        [Fact]
        public void ImportEdgesSurviveDeepCopy()
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
            ProjectInstance original = project.CreateProjectInstance();
            ProjectInstance copy = original.DeepCopy();

            copy.ImportEdges.ShouldNotBeNull();
            copy.ImportEdges.Count.ShouldBe(1);
            copy.ImportEdges[0].ImportedProjectPath.ShouldBe(importFile.Path);
            copy.ImportEdges[0].ImportingProjectPath.ShouldBe(projectFile.Path);
        }

        [Fact]
        public void ImportEdgesSerializedWhenPropertyIsSet()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            string importContent = "<Project />";
            var importFile = env.CreateFile("import.targets", importContent);

            string projectContent = $"""
                <Project>
                    <PropertyGroup>
                        <MSBuildProvideImportGraph>true</MSBuildProvideImportGraph>
                    </PropertyGroup>
                    <Import Project="{importFile.Path}" />
                </Project>
                """;
            var projectFile = env.CreateFile("test.proj", projectContent);

            using var collection = new ProjectCollection();
            var project = new Project(projectFile.Path, globalProperties: null, toolsVersion: null, collection);
            ProjectInstance original = project.CreateProjectInstance();
            original.TranslateEntireState = true;

            // Verify edges exist before serialization
            original.ImportEdges.ShouldNotBeNull();
            original.ImportEdges.Count.ShouldBe(1);

            // Round-trip through serialization
            ((ITranslatable)original).Translate(TranslationHelpers.GetWriteTranslator());
            ProjectInstance deserialized = ProjectInstance.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            // Edges should survive serialization when property is set
            deserialized.ImportEdges.ShouldNotBeNull();
            deserialized.ImportEdges.Count.ShouldBe(1);
            deserialized.ImportEdges[0].ImportedProjectPath.ShouldBe(importFile.Path);
            deserialized.ImportEdges[0].ImportingProjectPath.ShouldBe(projectFile.Path);
            deserialized.ImportEdges[0].SdkName.ShouldBeNull();
        }

        [Fact]
        public void ImportEdgesNotSerializedWithoutProperty()
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
            ProjectInstance original = project.CreateProjectInstance();
            original.TranslateEntireState = true;

            // Edges exist on the original
            original.ImportEdges.ShouldNotBeNull();
            original.ImportEdges.Count.ShouldBe(1);

            // Round-trip through serialization
            ((ITranslatable)original).Translate(TranslationHelpers.GetWriteTranslator());
            ProjectInstance deserialized = ProjectInstance.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            // Without the opt-in property, edges should NOT be serialized
            deserialized.ImportEdges.ShouldBeNull();
        }

        [Fact]
        public void ProjectImportEdgeToString()
        {
            var edge = new ProjectImportEdge(@"C:\imported.targets", @"C:\project.csproj");
            edge.ToString().ShouldContain(@"C:\project.csproj");
            edge.ToString().ShouldContain(@"C:\imported.targets");
            edge.ToString().ShouldContain("->");

            var sdkEdge = new ProjectImportEdge(@"C:\sdk.targets", @"C:\project.csproj", "Microsoft.NET.Sdk");
            sdkEdge.ToString().ShouldContain("SDK: Microsoft.NET.Sdk");
        }
    }
}
