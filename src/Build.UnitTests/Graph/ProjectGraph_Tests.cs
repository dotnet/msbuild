// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Graph.UnitTests
{
    public class ProjectGraphTests
    {
        [Fact]
        public void TestGraphWithSingleNode()
        {
            string projectContents = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                  <PropertyGroup>
                    <TestProperty>value</TestProperty>
                  </PropertyGroup>

                  <Target Name='Build'>
                    <Message Text='Building test project'/>
                  </Target>
                </Project>
                ";
            using (var env = TestEnvironment.Create())
            {
                TransientTestProjectWithFiles testProject =
                    env.CreateTestProjectWithFiles(projectContents, Array.Empty<string>());
                var projectGraph = new ProjectGraph(testProject.ProjectFile);
                projectGraph.ProjectNodes.Count.ShouldBe(1);
                Project projectNode = projectGraph.ProjectNodes.First().Project;
                projectNode.FullPath.ShouldBe(testProject.ProjectFile);
            }
        }

        [Fact]
        public void TestGraphWithSingleEntryPointMultipleNodes()
        {
            string projectAContents = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                  <PropertyGroup>
                    <TestProperty>value</TestProperty>
                  </PropertyGroup>
                  <ItemGroup>
                    <ProjectReference Include=""..\B\build.proj"" />
                  </ItemGroup>
                </Project>
                ";

            string projectBContents = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                  <PropertyGroup>
                    <TestProperty>value</TestProperty>
                  </PropertyGroup>
                </Project>
                ";

            using (var env = TestEnvironment.Create())
            {
                TransientTestProjectWithFiles projectA =
                    env.CreateTestProjectWithFiles(projectAContents, Array.Empty<string>(), "..\\A");
                TransientTestProjectWithFiles projectB =
                    env.CreateTestProjectWithFiles(projectBContents, Array.Empty<string>(), "..\\B");

                List<string> projectsToParse = new List<string>();
                projectsToParse.Add(projectA.ProjectFile);
                ProjectGraph graph = new ProjectGraph(projectsToParse);
                graph.ProjectNodes.Count.ShouldBe(2);
            }
        }

        /// <summary>
        /// A  B
        /// \ /
        ///  C
        /// </summary>
        [Fact]
        public void TestGraphWithMultipleEntryPointsMultipleNodes()
        {
            string projectAContents = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                  <PropertyGroup>
                    <TestProperty>value</TestProperty>
                  </PropertyGroup>
                  <ItemGroup>
                    <ProjectReference Include=""..\C\build.proj"" />
                  </ItemGroup>
                </Project>
                ";

            string projectBContents = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                  <PropertyGroup>
                    <TestProperty>value</TestProperty>
                  </PropertyGroup>
                  <ItemGroup>
                    <ProjectReference Include=""..\C\build.proj"" />
                  </ItemGroup>
                </Project>
                ";

            string projectCContents = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                  <PropertyGroup>
                    <TestProperty>value</TestProperty>
                  </PropertyGroup>
                </Project>
                ";

            using (var env = TestEnvironment.Create())
            {
                TransientTestProjectWithFiles projectA =
                    env.CreateTestProjectWithFiles(projectAContents, Array.Empty<string>(), "..\\A");
                TransientTestProjectWithFiles projectB =
                    env.CreateTestProjectWithFiles(projectBContents, Array.Empty<string>(), "..\\B");
                TransientTestProjectWithFiles projectC =
                    env.CreateTestProjectWithFiles(projectCContents, Array.Empty<string>(), "..\\C");
                List<string> projectsToParse = new List<string>();
                projectsToParse.Add(projectA.ProjectFile);
                projectsToParse.Add(projectB.ProjectFile);
                ProjectGraph graph = new ProjectGraph(projectsToParse);

                graph.ProjectNodes.Count.ShouldBe(3);
                ProjectGraphNode projectNodeA = graph.ProjectNodes.First(node => node.Project.FullPath.Equals(projectA.ProjectFile));
                ProjectGraphNode projectNodeB = graph.ProjectNodes.First(node => node.Project.FullPath.Equals(projectB.ProjectFile));
                ProjectGraphNode projectNodeC = graph.ProjectNodes.First(node => node.Project.FullPath.Equals(projectC.ProjectFile));
                projectNodeA.ProjectReferences.Count.ShouldBe(1);
                projectNodeB.ProjectReferences.Count.ShouldBe(1);
                projectNodeC.ProjectReferences.Count.ShouldBe(0);
            }
        }

        /// <summary>
        /// Test the following graph with entry projects (A,B,C)
        ///  F   B---C
        ///  |   |
        ///  A   |
        /// / \  |
        /// D   E 
        /// </summary>
        [Fact]
        public void TestGraphWithMultipleEntryPointsMultipleNodes2()
        {
            string projectAContents = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                  <PropertyGroup>
                    <TestProperty>value</TestProperty>
                  </PropertyGroup>
                  <ItemGroup>
                    <ProjectReference Include=""..\D\build.proj"" />
                    <ProjectReference Include=""..\E\build.proj"" />
                  </ItemGroup>
                </Project>
                ";

            string projectBContents = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                  <PropertyGroup>
                    <TestProperty>value</TestProperty>
                  </PropertyGroup>
                  <ItemGroup>
                    <ProjectReference Include=""..\F\build.proj"" />
                    <ProjectReference Include=""..\E\build.proj"" />
                    <ProjectReference Include=""..\C\build.proj"" />
                  </ItemGroup>
                </Project>
                ";

            string projectCContents = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                  <PropertyGroup>
                    <TestProperty>value</TestProperty>
                  </PropertyGroup>
                </Project>
                ";

            string projectDContents = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                  <PropertyGroup>
                    <TestProperty>value</TestProperty>
                  </PropertyGroup>
                </Project>
                ";

            string projectEContents = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                  <PropertyGroup>
                    <TestProperty>value</TestProperty>
                  </PropertyGroup>
                </Project>
                ";

            string projectFContents = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                  <PropertyGroup>
                    <TestProperty>value</TestProperty>
                  </PropertyGroup>
                  <ItemGroup>
                    <ProjectReference Include=""..\A\build.proj"" />
                  </ItemGroup>
                </Project>
                ";

            using (var env = TestEnvironment.Create())
            {
                TransientTestProjectWithFiles projectA =
                    env.CreateTestProjectWithFiles(projectAContents, Array.Empty<string>(), "..\\A");
                TransientTestProjectWithFiles projectB =
                    env.CreateTestProjectWithFiles(projectBContents, Array.Empty<string>(), "..\\B");
                TransientTestProjectWithFiles projectC =
                    env.CreateTestProjectWithFiles(projectCContents, Array.Empty<string>(), "..\\C");
                TransientTestProjectWithFiles projectD =
                    env.CreateTestProjectWithFiles(projectDContents, Array.Empty<string>(), "..\\D");
                TransientTestProjectWithFiles projectE =
                    env.CreateTestProjectWithFiles(projectEContents, Array.Empty<string>(), "..\\E");
                TransientTestProjectWithFiles projectF =
                    env.CreateTestProjectWithFiles(projectFContents, Array.Empty<string>(), "..\\F");

                // pass A,B,C in initial list of targets
                List<string> projectsToParse = new List<string>();
                projectsToParse.Add(projectA.ProjectFile);
                projectsToParse.Add(projectB.ProjectFile);
                projectsToParse.Add(projectC.ProjectFile);
                ProjectGraph graph = new ProjectGraph(projectsToParse);

                graph.ProjectNodes.Count.ShouldBe(6);
                ProjectGraphNode projectNodeA = graph.ProjectNodes.First(node => node.Project.FullPath.Equals(projectA.ProjectFile));
                ProjectGraphNode projectNodeB = graph.ProjectNodes.First(node => node.Project.FullPath.Equals(projectB.ProjectFile));
                ProjectGraphNode projectNodeC = graph.ProjectNodes.First(node => node.Project.FullPath.Equals(projectC.ProjectFile));
                projectNodeA.ProjectReferences.Count.ShouldBe(2);
                projectNodeB.ProjectReferences.Count.ShouldBe(3);
                projectNodeC.ProjectReferences.Count.ShouldBe(0);
            }
        }

        [Fact]
        public void TestSingleEntryWithCycle()
        {
            string projectAContents = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                  <PropertyGroup>
                    <TestProperty>value</TestProperty>
                  </PropertyGroup>
                  <ItemGroup>
                    <ProjectReference Include=""..\B\build.proj"" />
                  </ItemGroup>
                </Project>
                ";

            string projectBContents = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                  <PropertyGroup>
                    <TestProperty>value</TestProperty>
                  </PropertyGroup>
                  <ItemGroup>
                    <ProjectReference Include=""..\C\build.proj"" />
                  </ItemGroup>
                </Project>
                ";

            string projectCContents = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                  <PropertyGroup>
                    <TestProperty>value</TestProperty>
                  </PropertyGroup>
                  <ItemGroup>
                    <ProjectReference Include=""..\A\build.proj"" />
                  </ItemGroup>
                </Project>
                ";
            using (var env = TestEnvironment.Create())
            {
                TransientTestProjectWithFiles projectA =
                    env.CreateTestProjectWithFiles(projectAContents, Array.Empty<string>(), "..\\A");
                TransientTestProjectWithFiles projectB =
                    env.CreateTestProjectWithFiles(projectBContents, Array.Empty<string>(), "..\\B");
                TransientTestProjectWithFiles projectC =
                    env.CreateTestProjectWithFiles(projectCContents, Array.Empty<string>(), "..\\C");
                List<string> projectsToParse = new List<string>();
                projectsToParse.Add(projectA.ProjectFile);
                ProjectGraph graph = new ProjectGraph(projectsToParse);
                graph.ProjectNodes.Count.ShouldBe(3);
            }
        }
    }

}
