// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.Build.UnitTests;
using Xunit;
using Shouldly;

namespace Microsoft.Build.Graph.UnitTests
{
    public class ProjectGraphTests
    {
        private const string ProjA = "A.proj";
        private const string ProjB = "B.proj";
        private const string ProjC = "C.proj";
        private const string ProjD = "D.proj";
        private const string ProjE = "E.proj";
        private const string ProjF = "F.proj";
        private const string ProjG = "G.proj";

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

        /// <summary>
        ///  A  
        /// / \ 
        ///B   C
        /// </summary>
        [Fact]
        public void TestGraphWithThreeNodes()
        {
            string projectAContents = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                  <PropertyGroup>
                    <TestProperty>value</TestProperty>
                  </PropertyGroup>
                  <ItemGroup>
                    <ProjectReference Include=""B.proj"" />
                    <ProjectReference Include=""C.proj"" />
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
                    env.CreateTestProjectWithFiles(ProjA, projectAContents, new []{ProjB, ProjC});
                File.WriteAllText(Path.Combine(projectA.TestRoot, ProjB), projectBContents);
                File.WriteAllText(Path.Combine(projectA.TestRoot, ProjC), projectCContents);
                ProjectGraph graph = new ProjectGraph(projectA.ProjectFile);

                graph.ProjectNodes.Count.ShouldBe(3);
                ProjectGraphNode projectNodeA = graph.ProjectNodes.First(node => node.Project.FullPath.Equals(projectA.ProjectFile));
                ProjectGraphNode projectNodeB = graph.ProjectNodes.First(node => node.Project.FullPath.Contains(ProjB));
                ProjectGraphNode projectNodeC = graph.ProjectNodes.First(node => node.Project.FullPath.Contains(ProjC));
                projectNodeA.ProjectReferences.Count.ShouldBe(2);
                projectNodeB.ProjectReferences.Count.ShouldBe(0);
                projectNodeC.ProjectReferences.Count.ShouldBe(0);
            }
        }

        /// <summary>
        /// Test the following graph with entry project B
        /// B depends on F,E,C
        /// F depends on A
        /// E depends on G
        /// A depends on D,E
        /// </summary>
        [Fact]
        public void TestGraphWithMultipleNodes()
        {
            string projectAContents = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                  <PropertyGroup>
                    <TestProperty>value</TestProperty>
                  </PropertyGroup>
                  <ItemGroup>
                    <ProjectReference Include=""D.proj"" />
                    <ProjectReference Include=""E.proj"" />
                  </ItemGroup>
                </Project>
                ";

            string projectBContents = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                  <PropertyGroup>
                    <TestProperty>value</TestProperty>
                  </PropertyGroup>
                  <ItemGroup>
                    <ProjectReference Include=""F.proj"" />
                    <ProjectReference Include=""E.proj"" />
                    <ProjectReference Include=""C.proj"" />
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
                  <ItemGroup>
                    <ProjectReference Include=""G.proj"" />
                  </ItemGroup>
                </Project>
                ";

            string projectFContents = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                  <PropertyGroup>
                    <TestProperty>value</TestProperty>
                  </PropertyGroup>
                  <ItemGroup>
                    <ProjectReference Include=""A.proj"" />
                  </ItemGroup>
                </Project>
                ";

            string projectGContents = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                  <PropertyGroup>
                    <TestProperty>value</TestProperty>
                  </PropertyGroup>
                </Project>
                ";

            using (var env = TestEnvironment.Create())
            {
                var projectFiles = new string[] {ProjB, ProjC, ProjD, ProjE, ProjF};
                TransientTestProjectWithFiles projectA =
                    env.CreateTestProjectWithFiles(ProjA, projectAContents, projectFiles);
                string projBFullPath = Path.Combine(projectA.TestRoot, ProjB);
                File.WriteAllText(Path.Combine(projectA.TestRoot, ProjB), projectBContents);
                File.WriteAllText(Path.Combine(projectA.TestRoot, ProjC), projectCContents);
                File.WriteAllText(Path.Combine(projectA.TestRoot, ProjD), projectDContents);
                File.WriteAllText(Path.Combine(projectA.TestRoot, ProjE), projectEContents);
                File.WriteAllText(Path.Combine(projectA.TestRoot, ProjF), projectFContents);
                File.WriteAllText(Path.Combine(projectA.TestRoot, ProjG), projectGContents);
                ProjectGraph graph = new ProjectGraph(projBFullPath);

                graph.ProjectNodes.Count.ShouldBe(7);
                ProjectGraphNode projectNodeA = graph.ProjectNodes.First(node => node.Project.FullPath.Equals(projectA.ProjectFile));
                ProjectGraphNode projectNodeB = graph.ProjectNodes.First(node => node.Project.FullPath.Contains(ProjB));
                ProjectGraphNode projectNodeC = graph.ProjectNodes.First(node => node.Project.FullPath.Contains(ProjC));
                ProjectGraphNode projectNodeE = graph.ProjectNodes.First(node => node.Project.FullPath.Contains(ProjE));
                ProjectGraphNode projectNodeF = graph.ProjectNodes.First(node => node.Project.FullPath.Contains(ProjF));
                ProjectGraphNode projectNodeG = graph.ProjectNodes.First(node => node.Project.FullPath.Contains(ProjG));
                projectNodeA.ProjectReferences.Count.ShouldBe(2);
                projectNodeB.ProjectReferences.Count.ShouldBe(3);
                projectNodeC.ProjectReferences.Count.ShouldBe(0);
                projectNodeF.ProjectReferences.Count.ShouldBe(1);
                // confirm that there is a path from B -> F -> A -> E
                projectNodeB.ProjectReferences.ShouldContain(projectNodeF);
                projectNodeF.ProjectReferences.ShouldContain(projectNodeA);
                projectNodeA.ProjectReferences.ShouldContain(projectNodeE);
                projectNodeE.ProjectReferences.ShouldContain(projectNodeG);
            }
        }

        [Fact]
        public void TestCycleInGraph()
        {
            string projectAContents = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                  <PropertyGroup>
                    <TestProperty>value</TestProperty>
                  </PropertyGroup>
                  <ItemGroup>
                    <ProjectReference Include=""B.proj"" />
                  </ItemGroup>
                </Project>
                ";

            string projectBContents = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                  <PropertyGroup>
                    <TestProperty>value</TestProperty>
                  </PropertyGroup>
                  <ItemGroup>
                    <ProjectReference Include=""C.proj"" />
                  </ItemGroup>
                </Project>
                ";

            string projectCContents = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                  <PropertyGroup>
                    <TestProperty>value</TestProperty>
                  </PropertyGroup>
                  <ItemGroup>
                    <ProjectReference Include=""A.proj"" />
                  </ItemGroup>
                </Project>
                ";
            using (var env = TestEnvironment.Create())
            {
                TransientTestProjectWithFiles projectA =
                    env.CreateTestProjectWithFiles(ProjA, projectAContents, new []{ProjB, ProjC});
                File.WriteAllText(Path.Combine(projectA.TestRoot, ProjB), projectBContents);
                File.WriteAllText(Path.Combine(projectA.TestRoot, ProjC), projectCContents);
                ProjectGraph graph = new ProjectGraph(projectA.ProjectFile);
                graph.ProjectNodes.Count.ShouldBe(3);
            }
        }
    }

}
