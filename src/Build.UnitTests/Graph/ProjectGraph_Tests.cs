// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.Experimental.Graph.UnitTests
{
    public class ProjectGraphTests : IDisposable
    {
        private TestEnvironment _env;

        private static readonly ImmutableDictionary<string, string> EmptyGlobalProperties = new Dictionary<string, string> {{PropertyNames.IsGraphBuild, "true"}}.ToImmutableDictionary();

        private static readonly string InnerBuildPropertyName = "InnerBuild";
        private static readonly string MultitargetingSpecification = $@"<PropertyGroup>
                                                                        <InnerBuildProperty>{InnerBuildPropertyName}</InnerBuildProperty>
                                                                        <InnerBuildPropertyValues>InnerBuildProperties</InnerBuildPropertyValues>
                                                                        <InnerBuildProperties>a;b</InnerBuildProperties>
                                                                     </PropertyGroup>";
        private static readonly string ProjectReferenceTargetsWithMultitargeting = @"<ItemGroup>
                                                                                        <!-- Item order is important to ensure outer build targets are put in front of inner build ones -->
                                                                                        <ProjectReferenceTargets Include='A' Targets='AHelperInner;A' />
                                                                                        <ProjectReferenceTargets Include='A' Targets='AHelperOuter' OuterBuild='true' />
                                                                                     </ItemGroup>";
        private static string[] NonOuterBuildTargets = {"AHelperOuter", "AHelperInner", "A"};
        private static string[] OuterBuildTargets = {"AHelperOuter"};

        private static readonly string OuterBuildSpecificationWithProjectReferenceTargets = MultitargetingSpecification + ProjectReferenceTargetsWithMultitargeting;

        public ProjectGraphTests(ITestOutputHelper outputHelper)
        {
            _env = TestEnvironment.Create(outputHelper);
        }

        [Fact]
        public void ConstructWithNoNodes()
        {
            var projectGraph = new ProjectGraph(Enumerable.Empty<ProjectGraphEntryPoint>());

            projectGraph.ProjectNodes.ShouldBeEmpty();
            projectGraph.EntryPointNodes.ShouldBeEmpty();
            projectGraph.GraphRoots.ShouldBeEmpty();
            projectGraph.ProjectNodesTopologicallySorted.ShouldBeEmpty();
            projectGraph.GetTargetLists(new []{"restore", "build"}).ShouldBeEmpty();
        }

        [Fact]
        public void ConstructWithSingleNode()
        {
            using (var env = TestEnvironment.Create())
            {
                TransientTestFile entryProject = CreateProjectFile(env, 1);
                var projectGraph = new ProjectGraph(entryProject.Path);
                projectGraph.ProjectNodes.Count.ShouldBe(1);
                projectGraph.ProjectNodes.First().ProjectInstance.FullPath.ShouldBe(entryProject.Path);
            }
        }

        [Fact]
        public void ConstructWithSingleNodeWithProjectInstanceFactory()
        {
            using (var env = TestEnvironment.Create())
            {
                TransientTestFile entryProject = CreateProjectFile(env, 1);

                bool factoryCalled = false;
                var projectGraph = new ProjectGraph(
                    entryProject.Path,
                    ProjectCollection.GlobalProjectCollection,
                    (projectPath, globalProperties, projectCollection) =>
                    {
                        factoryCalled = true;
                        return ProjectGraph.DefaultProjectInstanceFactory(
                            projectPath,
                            globalProperties,
                            projectCollection);
                    });
                projectGraph.ProjectNodes.Count.ShouldBe(1);
                projectGraph.ProjectNodes.First().ProjectInstance.FullPath.ShouldBe(entryProject.Path);
                factoryCalled.ShouldBeTrue();
            }
        }

        [Fact]
        public void ConstructWithProjectInstanceFactory_FactoryReturnsNull_Throws()
        {
            using (var env = TestEnvironment.Create())
            {
                TransientTestFile entryProject = CreateProjectFile(env, 1);

                Should.Throw<AggregateException>(() => new ProjectGraph(
                    entryProject.Path,
                    ProjectCollection.GlobalProjectCollection,
                    (projectPath, globalProperties, projectCollection) => null)).InnerException.ShouldBeOfType<InvalidOperationException>();
            }
        }
        
        /// <summary>
        ///   1
        ///  / \
        /// 2   3
        /// </summary>
        [Fact]
        public void ConstructWithThreeNodes()
        {
            using (var env = TestEnvironment.Create())
            {
                TransientTestFile entryProject = CreateProjectFile(env, 1, new[] { 2, 3 });
                CreateProjectFile(env, 2);
                CreateProjectFile(env, 3);
                ProjectGraph graph = new ProjectGraph(entryProject.Path);

                graph.ProjectNodes.Count.ShouldBe(3);
                GetFirstNodeWithProjectNumber(graph, 1).ProjectReferences.Count.ShouldBe(2);
                GetFirstNodeWithProjectNumber(graph, 2).ProjectReferences.Count.ShouldBe(0);
                GetFirstNodeWithProjectNumber(graph, 3).ProjectReferences.Count.ShouldBe(0);
            }
        }

        /// <summary>
        /// Test the following graph with entry project 2
        /// 2 depends on 3,5,6
        /// 6 depends on 1
        /// 5 depends on 7
        /// 1 depends on 4,5
        /// </summary>
        [Fact]
        public void ConstructWithMultipleNodes()
        {
            using (var env = TestEnvironment.Create())
            {
                CreateProjectFile(env, 1, new[] { 4, 5 });
                TransientTestFile entryProject = CreateProjectFile(env, 2, new[] { 3, 5, 6 });
                CreateProjectFile(env, 3);
                CreateProjectFile(env, 4);
                CreateProjectFile(env, 5, new[] { 7 });
                CreateProjectFile(env, 6, new[] { 1 });
                CreateProjectFile(env, 7);

                ProjectGraph graph = new ProjectGraph(entryProject.Path);

                graph.ProjectNodes.Count.ShouldBe(7);
                ProjectGraphNode node1 = GetFirstNodeWithProjectNumber(graph, 1);
                ProjectGraphNode node2 = GetFirstNodeWithProjectNumber(graph, 2);
                ProjectGraphNode node3 = GetFirstNodeWithProjectNumber(graph, 3);
                ProjectGraphNode node4 = GetFirstNodeWithProjectNumber(graph, 4);
                ProjectGraphNode node5 = GetFirstNodeWithProjectNumber(graph, 5);
                ProjectGraphNode node6 = GetFirstNodeWithProjectNumber(graph, 6);
                ProjectGraphNode node7 = GetFirstNodeWithProjectNumber(graph, 7);

                node1.ProjectReferences.Count.ShouldBe(2);
                node2.ProjectReferences.Count.ShouldBe(3);
                node3.ProjectReferences.Count.ShouldBe(0);
                node4.ProjectReferences.Count.ShouldBe(0);
                node5.ProjectReferences.Count.ShouldBe(1);
                node6.ProjectReferences.Count.ShouldBe(1);
                node7.ProjectReferences.Count.ShouldBe(0);

                node1.ReferencingProjects.Count.ShouldBe(1);
                node2.ReferencingProjects.Count.ShouldBe(0);
                node3.ReferencingProjects.Count.ShouldBe(1);
                node4.ReferencingProjects.Count.ShouldBe(1);
                node5.ReferencingProjects.Count.ShouldBe(2);
                node6.ReferencingProjects.Count.ShouldBe(1);
                node7.ReferencingProjects.Count.ShouldBe(1);

                // confirm that there is a path from 2 -> 6 -> 1 -> 5 -> 7
                node2.ProjectReferences.ShouldContain(node6);
                node6.ProjectReferences.ShouldContain(node1);
                node1.ProjectReferences.ShouldContain(node5);
                node5.ProjectReferences.ShouldContain(node7);

                // confirm that there is a path from 7 -> 5 -> 1 -> 6 -> 2 using ReferencingProjects
                node7.ReferencingProjects.ShouldContain(node5);
                node5.ReferencingProjects.ShouldContain(node1);
                node1.ReferencingProjects.ShouldContain(node6);
                node6.ReferencingProjects.ShouldContain(node2);
            }
        }

        [Fact]
        public void ConstructWithCycle()
        {
            using (var env = TestEnvironment.Create())
            {
                TransientTestFile entryProject = CreateProjectFile(env, 1, new[] { 2 });
                var proj2 = CreateProjectFile(env, 2, new[] { 3 });
                var proj3 = CreateProjectFile(env, 3, new[] { 1 });
                var projectsInCycle = new List<string>() {entryProject.Path, proj3.Path, proj2.Path, entryProject.Path};
                string expectedErrorMessage = ProjectGraph.FormatCircularDependencyError(projectsInCycle);
                Should.Throw<CircularDependencyException>(() => new ProjectGraph(entryProject.Path)).Message.ShouldContain(expectedErrorMessage.ToString());
            }
        }

        [Fact]
        public void ConstructWithSelfLoop()
        {
            using (var env = TestEnvironment.Create())
            {
                TransientTestFile entryProject = CreateProjectFile(env, 1, new[] { 2, 3 });
                CreateProjectFile(env, 2, new[] { 2 });
                CreateProjectFile(env, 3);
                Should.Throw<CircularDependencyException>(() => new ProjectGraph(entryProject.Path));
            }
        }

        [Fact]
        // graph with a cycle between 2->6->7->3->2
        public void ConstructBigGraphWithCycle()
        {
            using (var env = TestEnvironment.Create())
            {
                TransientTestFile entryProject = CreateProjectFile(env, 1, new[] {2,3,4});
                var proj2 = CreateProjectFile(env, 2, new[] {5, 6});
                var proj3 = CreateProjectFile(env, 3, new[] {2, 8});
                CreateProjectFile(env, 4);
                CreateProjectFile(env, 5, new []{9, 10});
                var proj6 = CreateProjectFile(env, 6, new[] { 7});
                var proj7 = CreateProjectFile(env, 7, new[] { 3 });
                CreateProjectFile(env, 8);
                CreateProjectFile(env, 9);
                CreateProjectFile(env, 10);
                var projectsInCycle = new List<string>(){proj2.Path, proj3.Path, proj7.Path, proj6.Path, proj2.Path };
                var errorMessage = ProjectGraph.FormatCircularDependencyError(projectsInCycle);
                Should.Throw<CircularDependencyException>(() => new ProjectGraph(entryProject.Path)).Message.ShouldContain(errorMessage.ToString());
            }
        }

        [Fact]
        public void ConstructWithDifferentGlobalProperties()
        {
            using (var env = TestEnvironment.Create())
            {
                TransientTestFile entryProject = CreateProjectFile(env, 1, new[] { 2, 3 });
                env.CreateFile("2.proj", @"
<Project>
  <ItemGroup>
    <ProjectReference Include=""4.proj"" />
  </ItemGroup>
</Project>");
                env.CreateFile("3.proj", @"
<Project>
  <ItemGroup>
    <ProjectReference Include=""4.proj"" AdditionalProperties=""A=B"" />
  </ItemGroup>
</Project>");
                CreateProjectFile(env, 4);
                ProjectGraph graph = new ProjectGraph(entryProject.Path);

                // Project 4 requires 2 nodes
                graph.ProjectNodes.Count.ShouldBe(5);

                // Projects 2 and 3 both reference project 4, but with different properties, so they should not point to the same node.
                GetFirstNodeWithProjectNumber(graph, 2).ProjectReferences.First().ShouldNotBe(GetFirstNodeWithProjectNumber(graph, 3).ProjectReferences.First());
                GetFirstNodeWithProjectNumber(graph, 2).ProjectReferences.First().ProjectInstance.FullPath.ShouldEndWith("4.proj");
                GetFirstNodeWithProjectNumber(graph, 2).ProjectReferences.First().ProjectInstance.GlobalProperties.ShouldBeEquivalentTo(EmptyGlobalProperties);
                GetFirstNodeWithProjectNumber(graph, 3).ProjectReferences.First().ProjectInstance.FullPath.ShouldEndWith("4.proj");
                GetFirstNodeWithProjectNumber(graph, 3).ProjectReferences.First().ProjectInstance.GlobalProperties.Count.ShouldBeGreaterThan(1);
            }
        }

        [Fact]
        public void TestGlobalPropertiesInProjectReferences()
        {
            using (var env = TestEnvironment.Create())
            {
                TransientTestFile entryProject = env.CreateFile("1.proj", @"
<Project>
  <ItemGroup>
    <ProjectReference Include=""2.proj"" AdditionalProperties=""A=B""/>
  </ItemGroup>
</Project>");
                CreateProjectFile(env, 2, new[] { 3 });
                CreateProjectFile(env, 3);
                ProjectGraph graph = new ProjectGraph(entryProject.Path);
                graph.ProjectNodes.Count.ShouldBe(3);
                GetFirstNodeWithProjectNumber(graph, 3).ProjectInstance.GlobalProperties["A"].ShouldBe("B");
            }
        }

        [Fact]
        public void ConstructWithConvergingProperties()
        {
            using (var env = TestEnvironment.Create())
            {
                TransientTestFile entryProject = CreateProjectFile(env, 1, new[] { 2, 3 });
                env.CreateFile("2.proj", @"
<Project>
  <ItemGroup>
    <ProjectReference Include=""4.proj"" AdditionalProperties=""Foo=A"" />
  </ItemGroup>
</Project>");
                env.CreateFile("3.proj", @"
<Project>
  <ItemGroup>
    <ProjectReference Include=""4.proj"" AdditionalProperties=""Foo=B"" />
  </ItemGroup>
</Project>");
                env.CreateFile("4.proj", @"
<Project>
  <ItemGroup>
    <ProjectReference Include=""5.proj"" GlobalPropertiesToRemove=""Foo"" />
  </ItemGroup>
</Project>");
                CreateProjectFile(env, 5);
                ProjectGraph graph = new ProjectGraph(entryProject.Path);

                // Project 4 requires 2 nodes, but project 5 does not
                graph.ProjectNodes.Count.ShouldBe(6);

                var node4A = GetFirstNodeWithProjectNumber(graph, 2).ProjectReferences.First();
                var node4B = GetFirstNodeWithProjectNumber(graph, 3).ProjectReferences.First();
                node4A.ShouldNotBe(node4B);

                node4A.ProjectReferences.Count.ShouldBe(1);
                node4B.ProjectReferences.Count.ShouldBe(1);
                node4A.ProjectReferences.First().ShouldBe(node4B.ProjectReferences.First());
            }
        }

        [Fact]
        public void ConstructWithSameEffectiveProperties()
        {
            using (var env = TestEnvironment.Create())
            {
                TransientTestFile entryProject = CreateProjectFile(env, 1, new[] { 2, 3 });
                env.CreateFile("2.proj", @"
<Project>
  <ItemGroup>
    <ProjectReference Include=""4.proj"" AdditionalProperties=""Foo=Bar"" />
  </ItemGroup>
</Project>");
                env.CreateFile("3.proj", @"
<Project>
  <ItemGroup>
    <ProjectReference Include=""4.proj"" GlobalPropertiesToRemove=""DoesNotExist"" />
  </ItemGroup>
</Project>");
                CreateProjectFile(env, 4);
                ProjectGraph graph = new ProjectGraph(
                    entryProject.Path,
                    new Dictionary<string, string> { { "Foo", "Bar" } });

                // Project 4 does not require 2 nodes
                graph.ProjectNodes.Count.ShouldBe(4);

                // The project references end up using the same effective properties
                GetFirstNodeWithProjectNumber(graph, 2).ProjectReferences.First().ShouldBe(GetFirstNodeWithProjectNumber(graph, 3).ProjectReferences.First());
            }
        }

        [Fact]
        public void ConstructWithCaseDifferences()
        {
            using (var env = TestEnvironment.Create())
            {
                TransientTestFile entryProject = CreateProjectFile(env, 1, new[] { 2, 3, 4 });
                env.CreateFile("2.proj", @"
<Project>
  <ItemGroup>
    <ProjectReference Include=""5.proj"" AdditionalProperties=""foo=bar"" />
  </ItemGroup>
</Project>");
                env.CreateFile("3.proj", @"
<Project>
  <ItemGroup>
    <ProjectReference Include=""5.proj"" AdditionalProperties=""FOO=bar"" />
  </ItemGroup>
</Project>");
                env.CreateFile("4.proj", @"
<Project>
  <ItemGroup>
    <ProjectReference Include=""5.proj"" AdditionalProperties=""foo=BAR"" />
  </ItemGroup>
</Project>");
                CreateProjectFile(env, 5);
                ProjectGraph graph = new ProjectGraph(entryProject.Path);

                // Project 5 requires 2 nodes
                graph.ProjectNodes.Count.ShouldBe(6);

                // Property names are case-insensitive, so projects 2 and 3 point to the same project 5 node.
                GetFirstNodeWithProjectNumber(graph, 2).ProjectReferences.First().ShouldBe(GetFirstNodeWithProjectNumber(graph, 3).ProjectReferences.First());
                GetFirstNodeWithProjectNumber(graph, 2).ProjectReferences.First().ProjectInstance.FullPath.ShouldEndWith("5.proj");
                GetFirstNodeWithProjectNumber(graph, 2).ProjectReferences.First().ProjectInstance.GlobalProperties["FoO"].ShouldBe("bar");

                // Property values are case-sensitive, so project 4 points to a different project 5 node than proejcts 2 and 3
                GetFirstNodeWithProjectNumber(graph, 4).ProjectReferences.First().ShouldNotBe(GetFirstNodeWithProjectNumber(graph, 2).ProjectReferences.First());
                GetFirstNodeWithProjectNumber(graph, 4).ProjectReferences.First().ProjectInstance.FullPath.ShouldEndWith("5.proj");
                GetFirstNodeWithProjectNumber(graph, 4).ProjectReferences.First().ProjectInstance.GlobalProperties["FoO"].ShouldBe("BAR");
            }
        }

        [Fact]
        public void ConstructWithInvalidProperties()
        {
            using (var env = TestEnvironment.Create())
            {
                TransientTestFile entryProject = CreateProjectFile(env, 1, new[] { 2 });
                env.CreateFile("2.proj", @"
<Project>
  <ItemGroup>
    <ProjectReference Include=""3.proj"" AdditionalProperties=""ThisIsntValid"" />
  </ItemGroup>
</Project>");
                CreateProjectFile(env, 3);

                Should.Throw<AggregateException>(() => new ProjectGraph(entryProject.Path)).InnerException.ShouldBeOfType<InvalidProjectFileException>();
            }
        }

        [Fact]
        public void ConstructWithMultipleEntryPoints()
        {
            using (var env = TestEnvironment.Create())
            {
                TransientTestFile entryProject1 = CreateProjectFile(env, 1, new[] { 3 });
                TransientTestFile entryProject2 = CreateProjectFile(env, 2, new[] { 3 });
                CreateProjectFile(env, 3);
                var projectGraph = new ProjectGraph(new [] { entryProject1.Path, entryProject2.Path });
                projectGraph.ProjectNodes.Count.ShouldBe(3);

                var node1 = GetFirstNodeWithProjectNumber(projectGraph, 1);
                var node2 = GetFirstNodeWithProjectNumber(projectGraph, 2);
                var node3 = GetFirstNodeWithProjectNumber(projectGraph, 3);
                node1.ProjectReferences.Count.ShouldBe(1);
                node1.ProjectReferences.First().ShouldBe(node3);
                node2.ProjectReferences.Count.ShouldBe(1);
                node2.ProjectReferences.First().ShouldBe(node3);
            }
        }

        [Fact]
        public void ConstructWithMultipleEntryPointsWithDifferentGlobalProperties()
        {
            using (var env = TestEnvironment.Create())
            {
                TransientTestFile entryProject = CreateProjectFile(env, 1, new[] { 2 });
                CreateProjectFile(env, 2);
                var entryPoint1 = new ProjectGraphEntryPoint(entryProject.Path, new Dictionary<string, string> { { "Platform", "x86" } });
                var entryPoint2 = new ProjectGraphEntryPoint(entryProject.Path, new Dictionary<string, string> { { "Platform", "x64" } });

                var projectGraph = new ProjectGraph(new[] { entryPoint1, entryPoint2 });
                projectGraph.ProjectNodes.Count.ShouldBe(4);

                projectGraph.EntryPointNodes.Count.ShouldBe(2);

                var entryPointNode1 = projectGraph.EntryPointNodes.First();
                var entryPointNode2 = projectGraph.EntryPointNodes.Last();

                // The entry points should not be the same node, but should point to the same project
                entryPointNode1.ShouldNotBe(entryPointNode2);
                entryPointNode1.ProjectInstance.FullPath.ShouldBe(entryPointNode2.ProjectInstance.FullPath);
                entryPointNode1.ProjectInstance.GlobalProperties["Platform"].ShouldBe("x86");
                entryPointNode2.ProjectInstance.GlobalProperties["Platform"].ShouldBe("x64");

                // The entry points should not have the same project reference, but should point to the same project reference file
                entryPointNode1.ProjectReferences.Count.ShouldBe(1);
                entryPointNode2.ProjectReferences.Count.ShouldBe(1);
                entryPointNode1.ProjectReferences.First().ShouldNotBe(entryPointNode2.ProjectReferences.First());
                entryPointNode1.ProjectReferences.First().ProjectInstance.FullPath.ShouldBe(entryPointNode2.ProjectReferences.First().ProjectInstance.FullPath);
                entryPointNode1.ProjectReferences.First().ProjectInstance.GlobalProperties["Platform"].ShouldBe("x86");
                entryPointNode2.ProjectReferences.First().ProjectInstance.GlobalProperties["Platform"].ShouldBe("x64");
            }
        }

        [Fact]
        public void ConstructWithMultipleEntryPointsWithDifferentGlobalPropertiesConverging()
        {
            using (var env = TestEnvironment.Create())
            {
                TransientTestFile entryProject = env.CreateFile("1.proj", @"
<Project>
  <ItemGroup>
    <ProjectReference Include=""2.proj"" GlobalPropertiesToRemove=""Platform"" />
  </ItemGroup>
</Project>");
                CreateProjectFile(env, 2);
                var entryPoint1 = new ProjectGraphEntryPoint(entryProject.Path, new Dictionary<string, string> { { "Platform", "x86" } });
                var entryPoint2 = new ProjectGraphEntryPoint(entryProject.Path, new Dictionary<string, string> { { "Platform", "x64" } });

                var projectGraph = new ProjectGraph(new[] { entryPoint1, entryPoint2 });
                projectGraph.ProjectNodes.Count.ShouldBe(3);

                projectGraph.EntryPointNodes.Count.ShouldBe(2);

                var entryPointNode1 = projectGraph.EntryPointNodes.First();
                var entryPointNode2 = projectGraph.EntryPointNodes.Last();

                // The entry points should not be the same node, but should point to the same project
                entryPointNode1.ShouldNotBe(entryPointNode2);
                entryPointNode1.ProjectInstance.FullPath.ShouldBe(entryPointNode2.ProjectInstance.FullPath);
                entryPointNode1.ProjectInstance.GlobalProperties["Platform"].ShouldBe("x86");
                entryPointNode2.ProjectInstance.GlobalProperties["Platform"].ShouldBe("x64");

                // The entry points should have the same project reference since they're platform-agnostic
                entryPointNode1.ProjectReferences.Count.ShouldBe(1);
                entryPointNode2.ProjectReferences.Count.ShouldBe(1);
                entryPointNode1.ProjectReferences.First().ShouldBe(entryPointNode2.ProjectReferences.First());
                entryPointNode1.ProjectReferences.First().ProjectInstance.GlobalProperties.ContainsKey("Platform").ShouldBeFalse();
            }
        }

        [Fact]
        public void ConstructGraphWithDifferentEntryPointsAndGraphRoots()
        {
            using(var env = TestEnvironment.Create())
            {
                TransientTestFile entryProject1 = CreateProjectFile(env, 1, new[] { 4 });
                TransientTestFile entryProject2 = CreateProjectFile(env, 2, new[] { 4, 5 });
                TransientTestFile entryProject3 = CreateProjectFile(env, 3, new[] { 2, 6 });
                CreateProjectFile(env, 4);
                CreateProjectFile(env, 5);
                CreateProjectFile(env, 6);
                var projectGraph = new ProjectGraph(new[] { entryProject1.Path, entryProject2.Path, entryProject3.Path });
                projectGraph.EntryPointNodes.Count.ShouldBe(3);
                projectGraph.GraphRoots.Count.ShouldBe(2);
                projectGraph.GraphRoots.ShouldNotContain(GetFirstNodeWithProjectNumber(projectGraph, 2));
            }
        }

        [Fact]
        public void GetTargetListsAggregatesFromMultipleEdges()
        {
            using (var env = TestEnvironment.Create())
            {
                TransientTestFile entryProject = CreateProjectFile(env: env, projectNumber: 1, projectReferences: new[] { 2, 3 }, projectReferenceTargets: new Dictionary<string, string[]> { { "A", new[] { "B" } } });
                CreateProjectFile(env: env, projectNumber: 2, projectReferences: new[] { 4 }, projectReferenceTargets: new Dictionary<string, string[]> { { "B", new[] { "C" } } });
                CreateProjectFile(env: env, projectNumber: 3, projectReferences: new[] { 4 }, projectReferenceTargets: new Dictionary<string, string[]> { { "B", new[] { "D" } } });
                CreateProjectFile(env: env, projectNumber: 4);

                var projectGraph = new ProjectGraph(entryProject.Path);
                projectGraph.ProjectNodes.Count.ShouldBe(4);

                IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> targetLists = projectGraph.GetTargetLists(new[] { "A" });
                targetLists.Count.ShouldBe(projectGraph.ProjectNodes.Count);
                targetLists[GetFirstNodeWithProjectNumber(projectGraph, 1)].ShouldBe(new[] { "A" });
                targetLists[GetFirstNodeWithProjectNumber(projectGraph, 2)].ShouldBe(new[] { "B" });
                targetLists[GetFirstNodeWithProjectNumber(projectGraph, 3)].ShouldBe(new[] { "B" });
                targetLists[GetFirstNodeWithProjectNumber(projectGraph, 4)].ShouldBe(new[] { "C", "D" }); // From B => C and B => D
            }
        }

        [Fact]
        public void GetTargetListsDedupesTargets()
        {
            var projectReferenceTargets = new Dictionary<string, string[]>
            {
                { "A" , new[] { "B", "X", "C" } },
                { "B" , new[] { "X", "Y" } },
                { "C" , new[] { "X", "Z" } },
            };

            using (var env = TestEnvironment.Create())
            {
                TransientTestFile entryProject = CreateProjectFile(env: env, projectNumber: 1, projectReferences: new[] { 2 }, projectReferenceTargets: projectReferenceTargets);
                CreateProjectFile(env: env, projectNumber: 2, projectReferences: new[] { 3 }, projectReferenceTargets: projectReferenceTargets);
                CreateProjectFile(env: env, projectNumber: 3, projectReferences: Array.Empty<int>(), projectReferenceTargets: projectReferenceTargets);

                var projectGraph = new ProjectGraph(entryProject.Path);
                projectGraph.ProjectNodes.Count.ShouldBe(3);

                IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> targetLists = projectGraph.GetTargetLists(new[] { "A" });
                targetLists.Count.ShouldBe(projectGraph.ProjectNodes.Count);
                targetLists[GetFirstNodeWithProjectNumber(projectGraph, 1)].ShouldBe(new[] { "A" });
                targetLists[GetFirstNodeWithProjectNumber(projectGraph, 2)].ShouldBe(new[] { "B", "X", "C" });
                targetLists[GetFirstNodeWithProjectNumber(projectGraph, 3)].ShouldBe(new[] { "X", "Y", "Z" }); // Simplified from X, Y, X, Z
            }
        }

        [Fact]
        public void GetTargetListsForComplexGraph()
        {
            var projectReferenceTargets = new Dictionary<string, string[]>
            {
                { "A" , new[] { "B" } },
                { "B" , new[] { "C" } },
                { "C" , new[] { "D" } },
                { "D" , new[] { "E" } },
            };

            using (var env = TestEnvironment.Create())
            {
                TransientTestFile entryProject = CreateProjectFile(env: env, projectNumber: 1, projectReferences: new[] { 2, 3, 5 }, projectReferenceTargets: projectReferenceTargets);
                CreateProjectFile(env: env, projectNumber: 2, projectReferences: new[] { 4, 5 }, projectReferenceTargets: projectReferenceTargets);
                CreateProjectFile(env: env, projectNumber: 3, projectReferences: new[] { 5, 6 }, projectReferenceTargets: projectReferenceTargets);
                CreateProjectFile(env: env, projectNumber: 4, projectReferences: new[] { 5 }, projectReferenceTargets: projectReferenceTargets);
                CreateProjectFile(env: env, projectNumber: 5, projectReferences: new[] { 6 }, projectReferenceTargets: projectReferenceTargets);
                CreateProjectFile(env: env, projectNumber: 6, projectReferences: Array.Empty<int>(), projectReferenceTargets: projectReferenceTargets);

                var projectGraph = new ProjectGraph(entryProject.Path);
                projectGraph.ProjectNodes.Count.ShouldBe(6);

                IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> targetLists = projectGraph.GetTargetLists(new[] { "A" });
                targetLists.Count.ShouldBe(projectGraph.ProjectNodes.Count);
                targetLists[GetFirstNodeWithProjectNumber(projectGraph, 1)].ShouldBe(new[] { "A" });
                targetLists[GetFirstNodeWithProjectNumber(projectGraph, 2)].ShouldBe(new[] { "B" });
                targetLists[GetFirstNodeWithProjectNumber(projectGraph, 3)].ShouldBe(new[] { "B" });
                targetLists[GetFirstNodeWithProjectNumber(projectGraph, 4)].ShouldBe(new[] { "C" });
                targetLists[GetFirstNodeWithProjectNumber(projectGraph, 5)].ShouldBe(new[] { "B", "C", "D" });
                targetLists[GetFirstNodeWithProjectNumber(projectGraph, 6)].ShouldBe(new[] { "C", "D", "E" });
            }
        }

        [Fact]
        public void GetTargetListsNullEntryTargets()
        {
            using (var env = TestEnvironment.Create())
            {
                TransientTestFile entryProject = CreateProjectFile(env: env, projectNumber: 1, projectReferences: new[] { 2 }, projectReferenceTargets: new Dictionary<string, string[]> { { "A", new[] { "B" } } }, defaultTargets: "A");
                CreateProjectFile(env: env, projectNumber: 2);

                var projectGraph = new ProjectGraph(entryProject.Path);
                projectGraph.ProjectNodes.Count.ShouldBe(2);

                IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> targetLists = projectGraph.GetTargetLists(null);
                targetLists.Count.ShouldBe(projectGraph.ProjectNodes.Count);
                targetLists[GetFirstNodeWithProjectNumber(projectGraph, 1)].ShouldBe(new[] { "A" });
                targetLists[GetFirstNodeWithProjectNumber(projectGraph, 2)].ShouldBe(new[] { "B" });
            }
        }

        [Fact]
        public void GetTargetsListsShouldApplyDefaultTargetsOnlyToGraphRoots()
        {
            using (var env = TestEnvironment.Create())
            {
                var root1 = CreateProjectFile(env: env, projectNumber: 1, projectReferences: new[] {2}, projectReferenceTargets: new Dictionary<string, string[]> {{"A", new[] {"B"}}}, defaultTargets: "A").Path;
                var root2 = CreateProjectFile(env: env, projectNumber: 2, projectReferences: new[] {3}, projectReferenceTargets: new Dictionary<string, string[]> {{"B", new[] {"C"}}, {"X", new[] {"Y"}}}, defaultTargets: "X").Path;
                CreateProjectFile(env: env, projectNumber: 3);
                

                var projectGraph = new ProjectGraph(new []{root1, root2});
                projectGraph.ProjectNodes.Count.ShouldBe(3);

                IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> targetLists = projectGraph.GetTargetLists(null);

                targetLists.Count.ShouldBe(projectGraph.ProjectNodes.Count);
                targetLists[GetFirstNodeWithProjectNumber(projectGraph, 1)].ShouldBe(new[] { "A" });
                targetLists[GetFirstNodeWithProjectNumber(projectGraph, 2)].ShouldBe(new[] { "B" });
                targetLists[GetFirstNodeWithProjectNumber(projectGraph, 3)].ShouldBe(new[] { "C" });
            }
        }

        [Fact]
        public void GetTargetListsUsesAllTargetsForNonMultitargetingNodes()
        {
            using (var env = TestEnvironment.Create())
            {
                var root1 = CreateProjectFile(
                    env: env,
                    projectNumber: 1,
                    projectReferences: new[] {2},
                    projectReferenceTargets: null,
                    defaultTargets: null,
                    extraContent: ProjectReferenceTargetsWithMultitargeting)
                    .Path;
                CreateProjectFile(env, 2);
                
                var projectGraph = new ProjectGraph(root1);

                var dot = projectGraph.ToDot();

                projectGraph.ProjectNodes.Count.ShouldBe(2);

                IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> targetLists = projectGraph.GetTargetLists(new List<string>{"A"});
                targetLists.Count.ShouldBe(projectGraph.ProjectNodes.Count);

                targetLists[GetFirstNodeWithProjectNumber(projectGraph, 1)].ShouldBe(new[] { "A" });
                targetLists[GetFirstNodeWithProjectNumber(projectGraph, 2)].ShouldBe(NonOuterBuildTargets);
            }
        }

        [Fact]
        public void GetTargetsListInnerBuildToInnerBuild()
        {
            using (var env = TestEnvironment.Create())
            {
                string singleTargetedSpec = OuterBuildSpecificationWithProjectReferenceTargets +
                        $@"<PropertyGroup>
                            <{InnerBuildPropertyName}>a</{InnerBuildPropertyName}>
                          </PropertyGroup>";

                var root1 =CreateProjectFile(
                            env: env,
                            projectNumber: 1,
                            projectReferences: new[] {2},
                            projectReferenceTargets: null,
                            defaultTargets: null,
                            extraContent: singleTargetedSpec)
                            .Path;
                CreateProjectFile(
                    env: env,
                    projectNumber: 2,
                    projectReferences: null,
                    projectReferenceTargets: null,
                    defaultTargets: null,
                    extraContent: singleTargetedSpec);
                
                
                var projectGraph = new ProjectGraph(root1);

                var dot = projectGraph.ToDot();

                projectGraph.ProjectNodes.Count.ShouldBe(2);

                IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> targetLists = projectGraph.GetTargetLists(new List<string>{"A"});
                targetLists.Count.ShouldBe(projectGraph.ProjectNodes.Count);

                targetLists[GetFirstNodeWithProjectNumber(projectGraph, 1)].ShouldBe(new[] { "A" });
                targetLists[GetFirstNodeWithProjectNumber(projectGraph, 2)].ShouldBe(NonOuterBuildTargets);
            }
        }

        [Fact]
        public void GetTargetListsFiltersTargetsForOuterAndInnerBuilds()
        {
            using (var env = TestEnvironment.Create())
            {
                var root1 = CreateProjectFile(
                    env: env,
                    projectNumber: 1,
                    projectReferences: new[] { 2 },
                    projectReferenceTargets: null,
                    defaultTargets: null,
                    extraContent: ProjectReferenceTargetsWithMultitargeting).Path;
                CreateProjectFile(
                    env: env,
                    projectNumber: 2,
                    projectReferences: null,
                    projectReferenceTargets: null,
                    defaultTargets: null,
                    extraContent: OuterBuildSpecificationWithProjectReferenceTargets);
                
                var projectGraph = new ProjectGraph(root1);

                var dot = projectGraph.ToDot();

                projectGraph.ProjectNodes.Count.ShouldBe(4);

                IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> targetLists = projectGraph.GetTargetLists(new List<string>{"A"});

                targetLists.Count.ShouldBe(projectGraph.ProjectNodes.Count);
                var root = GetFirstNodeWithProjectNumber(projectGraph, 1);

                var outerBuild = GetNodesWithProjectNumber(projectGraph, 2).First(IsOuterBuild);
                var innerBuilds = GetNodesWithProjectNumber(projectGraph, 2).Where(IsInnerBuild).ToArray();

                targetLists[root].ShouldBe(new[] { "A" });
                targetLists[outerBuild].ShouldBe(OuterBuildTargets);

                foreach (var innerBuild in innerBuilds)
                {
                    targetLists[innerBuild].ShouldBe(NonOuterBuildTargets);
                }
            }
        }

        [Fact]
        public void GetTargetListsForComplexMultitargetingGraph()
        {
            using (var env = TestEnvironment.Create())
            {
                var root1 = CreateProjectFile(
                    env: env,
                    projectNumber: 1,
                    projectReferences: null,
                    projectReferenceTargets: null,
                    defaultTargets: null,
                    extraContent: OuterBuildSpecificationWithProjectReferenceTargets +
                    $@"<ItemGroup>
                            <ProjectReference Include=`3.proj` Condition=`'$({InnerBuildPropertyName})'=='a'`/>

                            <ProjectReference Include=`4.proj` Condition=`'$({InnerBuildPropertyName})'=='b'`/>
                            <ProjectReference Include=`5.proj` Condition=`'$({InnerBuildPropertyName})'=='b'`/>
                            <ProjectReference Include=`6.proj` Condition=`'$({InnerBuildPropertyName})'=='b'` Properties=`{InnerBuildPropertyName}=a`/>
                       </ItemGroup>".Cleanup())
                    .Path;

                var root2 = CreateProjectFile(
                    env: env,
                    projectNumber: 2,
                    projectReferences: null,
                    projectReferenceTargets: null,
                    defaultTargets: null,
                    extraContent: ProjectReferenceTargetsWithMultitargeting +
                    $@"<ItemGroup>
                            <ProjectReference Include=`1.proj` Properties=`{InnerBuildPropertyName}=b`/>
                            <ProjectReference Include=`4.proj`/>
                            <ProjectReference Include=`5.proj`/>
                       </ItemGroup>".Cleanup())
                    .Path;

                CreateProjectFile(
                    env: env,
                    projectNumber: 3,
                    projectReferences: null,
                    projectReferenceTargets: null,
                    defaultTargets: null,
                    extraContent: OuterBuildSpecificationWithProjectReferenceTargets);

                CreateProjectFile(
                    env: env,
                    projectNumber: 4,
                    projectReferences: new []{6},
                    projectReferenceTargets: null,
                    defaultTargets: null,
                    extraContent: ProjectReferenceTargetsWithMultitargeting);

                CreateProjectFile(
                    env: env,
                    projectNumber: 5,
                    projectReferences: null,
                    projectReferenceTargets: null,
                    defaultTargets: null,
                    extraContent: OuterBuildSpecificationWithProjectReferenceTargets +
                    $@"
                       <PropertyGroup>
                            <{InnerBuildPropertyName}>a</{InnerBuildPropertyName}>
                       </PropertyGroup>

                       <ItemGroup>
                            <ProjectReference Include=`3.proj` Properties=`{InnerBuildPropertyName}=a`/>
                            <ProjectReference Include=`6.proj`/>
                       </ItemGroup>".Cleanup());

                CreateProjectFile(
                    env: env,
                    projectNumber: 6,
                    projectReferences: null,
                    projectReferenceTargets: null,
                    defaultTargets: null,
                    extraContent: OuterBuildSpecificationWithProjectReferenceTargets);
                
                var projectGraph = new ProjectGraph(new[] {root1, root2});

                var dot = projectGraph.ToDot();

                projectGraph.ProjectNodes.Count.ShouldBe(12);

                IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> targetLists = projectGraph.GetTargetLists(new List<string>{"A"});

                targetLists.Count.ShouldBe(projectGraph.ProjectNodes.Count);

                AssertMultitargetingNode(1, projectGraph, targetLists, new []{"A"}, NonOuterBuildTargets);
                AssertMultitargetingNode(3, projectGraph, targetLists, OuterBuildTargets, NonOuterBuildTargets);
                AssertMultitargetingNode(6, projectGraph, targetLists, OuterBuildTargets, NonOuterBuildTargets);

                targetLists[GetFirstNodeWithProjectNumber(projectGraph, 2)].ShouldBe(new []{"A"});
                targetLists[GetFirstNodeWithProjectNumber(projectGraph, 4)].ShouldBe(NonOuterBuildTargets);
                targetLists[GetFirstNodeWithProjectNumber(projectGraph, 5)].ShouldBe(NonOuterBuildTargets);
            }

            void AssertMultitargetingNode(int projectNumber, ProjectGraph projectGraph, IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> targetLists, string[] outerBuildTargets, string[] nonOuterBuildTargets)
            {
                targetLists[GetNodesWithProjectNumber(projectGraph, projectNumber).First(IsOuterBuild)].ShouldBe(outerBuildTargets);

                foreach (var innerBuild in GetNodesWithProjectNumber(projectGraph, projectNumber).Where(IsInnerBuild))
                {
                    targetLists[innerBuild].ShouldBe(nonOuterBuildTargets);
                }
            }
        }

        [Fact]
        public void GetTargetListsDefaultTargetsAreExpanded()
        {
            using (var env = TestEnvironment.Create())
            {
                TransientTestFile entryProject = CreateProjectFile(env, 1, new[] { 2 }, new Dictionary<string, string[]> { { "A", new[] { ".default" } } }, defaultTargets: "A");
                CreateProjectFile(env: env, projectNumber: 2, defaultTargets: "B");

                var projectGraph = new ProjectGraph(entryProject.Path);
                projectGraph.ProjectNodes.Count.ShouldBe(2);

                IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> targetLists = projectGraph.GetTargetLists(null);
                targetLists.Count.ShouldBe(projectGraph.ProjectNodes.Count);
                targetLists[GetFirstNodeWithProjectNumber(projectGraph, 1)].ShouldBe(new[] { "A" });
                targetLists[GetFirstNodeWithProjectNumber(projectGraph, 2)].ShouldBe(new[] { "B" });
            }
        }

        [Fact]
        public void GetTargetListsUnspecifiedTargetsDefaultToBuild()
        {
            using (var env = TestEnvironment.Create())
            {
                TransientTestFile entryProject = CreateProjectFile(
                    env: env,
                    projectNumber: 1,
                    projectReferences: new[] { 2 },
                    projectReferenceTargets: new Dictionary<string, string[]> { { "Build", new[] { "A", ".default" } } });

                CreateProjectFile(env: env, projectNumber: 2);

                var projectGraph = new ProjectGraph(entryProject.Path);
                projectGraph.ProjectNodes.Count.ShouldBe(2);

                IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> targetLists = projectGraph.GetTargetLists(null);
                targetLists.Count.ShouldBe(projectGraph.ProjectNodes.Count);
                targetLists[GetFirstNodeWithProjectNumber(projectGraph, 1)].ShouldBe(new[] { "Build" });
                targetLists[GetFirstNodeWithProjectNumber(projectGraph, 2)].ShouldBe(new[] { "A", "Build" });
            }
        }

        [Fact]
        public void GetTargetListsDefaultComplexPropagation()
        {
            var projectReferenceTargets = new Dictionary<string, string[]>
            {
                { "Build", new[] { "A", ".default" } },
                { "X", new[] { "B", ".default" } },
                { "Y", new[] { "C", ".default" } },
            };

            using (var env = TestEnvironment.Create())
            {
                TransientTestFile entryProject = CreateProjectFile(env, 1, new[] { 2, 3, 4 }, projectReferenceTargets);
                CreateProjectFile(env: env, projectNumber: 2, projectReferences: new[] { 5 }, projectReferenceTargets: projectReferenceTargets);
                CreateProjectFile(env: env, projectNumber: 3, projectReferences: new[] { 6 }, projectReferenceTargets: projectReferenceTargets, defaultTargets: "X");
                CreateProjectFile(env: env, projectNumber: 4, projectReferences: new[] { 7 }, projectReferenceTargets: projectReferenceTargets, defaultTargets: "Y");
                CreateProjectFile(env: env, projectNumber: 5);
                CreateProjectFile(env: env, projectNumber: 6);
                CreateProjectFile(env: env, projectNumber: 7, defaultTargets: "Z;W");

                var projectGraph = new ProjectGraph(entryProject.Path);
                projectGraph.ProjectNodes.Count.ShouldBe(7);

                IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> targetLists = projectGraph.GetTargetLists(null);
                targetLists.Count.ShouldBe(projectGraph.ProjectNodes.Count);
                targetLists[GetFirstNodeWithProjectNumber(projectGraph, 1)].ShouldBe(new[] { "Build" });
                targetLists[GetFirstNodeWithProjectNumber(projectGraph, 2)].ShouldBe(new[] { "A", "Build" });
                targetLists[GetFirstNodeWithProjectNumber(projectGraph, 3)].ShouldBe(new[] { "A", "X" });
                targetLists[GetFirstNodeWithProjectNumber(projectGraph, 4)].ShouldBe(new[] { "A", "Y" });
                targetLists[GetFirstNodeWithProjectNumber(projectGraph, 5)].ShouldBe(new[] { "A", "Build" });
                targetLists[GetFirstNodeWithProjectNumber(projectGraph, 6)].ShouldBe(new[] { "B", "Build" });
                targetLists[GetFirstNodeWithProjectNumber(projectGraph, 7)].ShouldBe(new[] { "C", "Z", "W" });
            }
        }

        public static IEnumerable<object[]> Graphs
        {
            get
            {
                yield return new object[]
                {
                    new Dictionary<int, int[]>()
                };

                yield return new object[]
                {
                    new Dictionary<int, int[]>
                    {
                        {1, null}
                    }
                };

                yield return new object[]
                {
                    new Dictionary<int, int[]>
                    {
                        {1, null},
                        {2, null}
                    }
                };

                yield return new object[]
                {
                    new Dictionary<int, int[]>
                    {
                        {1, new []{2}}
                    }
                };

                yield return new object[]
                {
                    new Dictionary<int, int[]>
                    {
                        {1, new []{2}},
                        {2, null}
                    }
                };

                yield return new object[]
                {
                    new Dictionary<int, int[]>
                    {
                        {1, new []{2}},
                        {2, new []{3}}
                    }
                };

                yield return new object[]
                {
                    new Dictionary<int, int[]>
                    {
                        {1, new []{2, 3}}
                    }
                };

                yield return new object[]
                {
                    new Dictionary<int, int[]>
                    {
                        {1, new []{3, 2}},
                        {2, new []{3}}
                    }
                };

                yield return new object[]
                {
                    new Dictionary<int, int[]>
                    {
                        {1, new []{2, 3}},
                        {2, new []{4}},
                        {3, new []{4}}
                    }
                };

                yield return new object[]
                {
                    new Dictionary<int, int[]>
                    {
                        {1, new []{4, 3, 2}},
                        {2, new []{4}},
                        {3, new []{4}}
                    }
                };

                yield return new object[]
                {
                    new Dictionary<int, int[]>
                    {
                        {1, new []{2}},
                        {3, new []{4}}
                    }
                };

                yield return new object[]
                {
                    new Dictionary<int, int[]>
                    {
                        {1, new []{2, 4}},
                        {3, new []{4}}
                    }
                };

                yield return new object[]
                {
                    new Dictionary<int, int[]>
                    {
                        {1, new []{2, 4}},
                        {2, new []{3}},
                        {3, new []{4}},
                    }
                };

                yield return new object[]
                {
                    new Dictionary<int, int[]>
                    {
                        {1, new []{3, 2}},
                        {2, new []{3}},
                        {3, new []{5, 4}},
                        {4, new []{5}},
                    }
                };

                yield return new object[]
                {
                    new Dictionary<int, int[]>
                    {
                        {1, new []{5, 4, 7}},
                        {2, new []{5}},
                        {3, new []{6, 5}},
                        {4, new []{7}},
                        {5, new []{7, 8}},
                        {6, new []{7, 9}}
                    }
                };
            }
        }

        [Theory]
        [MemberData(nameof(Graphs))]
        public void TopologicalSortShouldTopologicallySort(Dictionary<int, int[]> edges)
        {
            using (var env = TestEnvironment.Create())
            {
                var projectGraph = Helpers.CreateProjectGraph(env, edges);

                var toposort = projectGraph.ProjectNodesTopologicallySorted.ToArray();

                toposort.Length.ShouldBe(projectGraph.ProjectNodes.Count);

                for (var i = 0; i < toposort.Length; i++)
                {
                    for (var j = 0; j < i; j++)
                    {
                        // toposort is reversed
                        toposort[i].ReferencingProjects.ShouldNotContain(toposort[j], $"Dependency of node at index {j} found at index {i}");
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(Graphs))]
        public void DotNotationShouldRepresentGraph(Dictionary<int, int[]> edges)
        {
            var graph = Helpers.CreateProjectGraph(
                _env,
                edges,
                new Dictionary<string, string> {{"a", "b"}});


            Func<ProjectGraphNode, string> nodeIdProvider = GetProjectFileName;

            var dot = graph.ToDot(nodeIdProvider);

            var edgeCount = 0;

            foreach (var node in graph.ProjectNodes)
            {
                var nodeId = nodeIdProvider(node);

                foreach (var globalProperty in node.ProjectInstance.GlobalProperties)
                {
                    dot.ShouldMatch($@"{nodeId}\s*\[.*{globalProperty.Key}.*{globalProperty.Value}.*\]");
                }

                foreach (var reference in node.ProjectReferences)
                {
                    edgeCount++;
                    dot.ShouldMatch($@"{nodeId}\s*->\s*{nodeIdProvider(reference)}");
                }
            }

            // edge count
            Regex.Matches(dot,"->").Count.ShouldBe(edgeCount);

            // node count
            Regex.Matches(dot,"label").Count.ShouldBe(graph.ProjectNodes.Count);
        }

        private static void AssertOuterBuildAsRoot(ProjectGraphNode outerBuild, Dictionary<string, string> additionalGlobalProperties = null)
        {
            additionalGlobalProperties = additionalGlobalProperties ?? new Dictionary<string, string>();

            AssertOuterBuildEvaluation(outerBuild, additionalGlobalProperties);

            outerBuild.ReferencingProjects.ShouldBeEmpty();
            outerBuild.ProjectReferences.Count.ShouldBe(2);

            foreach (var innerBuild in outerBuild.ProjectReferences)
            {
                AssertInnerBuildEvaluation(innerBuild, true, additionalGlobalProperties);
            }
        }

        private static void AssertOuterBuildAsNonRoot(ProjectGraphNode outerBuild, Dictionary<string, string> additionalGlobalProperties = null)
        {
            additionalGlobalProperties = additionalGlobalProperties ?? new Dictionary<string, string>();

            AssertOuterBuildEvaluation(outerBuild, additionalGlobalProperties);

            outerBuild.ProjectReferences.ShouldBeEmpty();
            outerBuild.ReferencingProjects.ShouldNotBeEmpty();

            foreach (var outerBuildReferencer in outerBuild.ReferencingProjects)
            {
                var innerBuilds =
                    outerBuildReferencer.ProjectReferences.Where(
                        p =>
                            IsInnerBuild(p) 
                            && p.ProjectInstance.FullPath == outerBuild.ProjectInstance.FullPath).ToArray();

                innerBuilds.Length.ShouldBe(2);

                foreach (var innerBuild in innerBuilds)
                {
                    AssertInnerBuildEvaluation(innerBuild, true, additionalGlobalProperties);
                }
            }
        }

        private static bool IsOuterBuild(ProjectGraphNode project)
        {
            return ProjectInterpretation.GetProjectType(project.ProjectInstance) == ProjectInterpretation.ProjectType.OuterBuild;
        }

        private static bool IsInnerBuild(ProjectGraphNode project)
        {
            return ProjectInterpretation.GetProjectType(project.ProjectInstance) == ProjectInterpretation.ProjectType.InnerBuild;
        }

        private static bool IsNotMultitargeting(ProjectGraphNode project)
        {
            return ProjectInterpretation.GetProjectType(project.ProjectInstance) == ProjectInterpretation.ProjectType.NonMultitargeting;
        }

        private static void AssertNonMultitargetingNode(ProjectGraphNode node, Dictionary<string, string> additionalGlobalProperties = null)
        {
            additionalGlobalProperties = additionalGlobalProperties ?? new Dictionary<string, string>();

            IsNotMultitargeting(node).ShouldBeTrue();
            node.ProjectInstance.GlobalProperties.ShouldBeEquivalentTo(EmptyGlobalProperties.AddRange(additionalGlobalProperties));
            node.ProjectInstance.GetProperty(InnerBuildPropertyName).ShouldBeNull();
        }

        private static void AssertOuterBuildEvaluation(ProjectGraphNode outerBuild, Dictionary<string, string> additionalGlobalProperties)
        {
            additionalGlobalProperties.ShouldNotBeNull();

            IsOuterBuild(outerBuild).ShouldBeTrue();
            IsInnerBuild(outerBuild).ShouldBeFalse();

            outerBuild.ProjectInstance.GetProperty(InnerBuildPropertyName).ShouldBeNull();
            outerBuild.ProjectInstance.GlobalProperties.ShouldBeEquivalentTo(EmptyGlobalProperties.AddRange(additionalGlobalProperties));
        }

        private static void AssertInnerBuildEvaluation(
            ProjectGraphNode innerBuild,
            bool InnerBuildPropertyIsSetViaGlobalProperty,
            Dictionary<string, string> additionalGlobalProperties)
        {
            additionalGlobalProperties.ShouldNotBeNull();

            IsOuterBuild(innerBuild).ShouldBeFalse();
            IsInnerBuild(innerBuild).ShouldBeTrue();

            var innerBuildPropertyValue = innerBuild.ProjectInstance.GetPropertyValue(InnerBuildPropertyName);

            innerBuildPropertyValue.ShouldNotBeNullOrEmpty();

            if (InnerBuildPropertyIsSetViaGlobalProperty)
            {
                innerBuild.ProjectInstance.GlobalProperties.ShouldBeEquivalentTo(
                    EmptyGlobalProperties
                        .Add(InnerBuildPropertyName, innerBuildPropertyValue)
                        .AddRange(additionalGlobalProperties));
            }
        }

        [Fact]
        public void OuterBuildAsRootShouldDirectlyReferenceInnerBuilds()
        {
            var projectFile = _env.CreateTestProjectWithFiles($@"<Project>{MultitargetingSpecification}</Project>").ProjectFile;

            var graph = new ProjectGraph(projectFile);

            var dot = graph.ToDot();

            graph.ProjectNodes.Count.ShouldBe(3);
            
            var outerBuild = graph.GraphRoots.First();

            AssertOuterBuildAsRoot(outerBuild);
        }

        [Fact]
        public void ReferenceOfMultitargetingProjectShouldNotInheritInnerBuildSpecificGlobalProperties()
        {
            var root = CreateProjectFile(env: _env, projectNumber: 1, projectReferences: new[] {2}, projectReferenceTargets: null, defaultTargets: null, extraContent: MultitargetingSpecification).Path;
            CreateProjectFile(env: _env, projectNumber: 2);

            var graph = new ProjectGraph(root);

            var dot = graph.ToDot();

            graph.ProjectNodes.Count.ShouldBe(4);

            AssertOuterBuildAsRoot(graph.GraphRoots.First());

            var nonMultitargetingNode = GetFirstNodeWithProjectNumber(graph, 2);

            AssertNonMultitargetingNode(nonMultitargetingNode);
        }

        [Fact]
        public void InnerBuildAsRootViaLocalPropertyShouldNotPropagateInnerBuildPropertyToReference()
        {
            var innerBuildViaLocalProperty = MultitargetingSpecification + $"<PropertyGroup><{InnerBuildPropertyName}>foo</{InnerBuildPropertyName}></PropertyGroup>";

            var root = CreateProjectFile(
                env: _env,
                projectNumber: 1,
                projectReferences: new[] {2},
                projectReferenceTargets: null,
                defaultTargets: null,
                extraContent: innerBuildViaLocalProperty).Path;

            CreateProjectFile(env: _env, projectNumber: 2);

            var graph = new ProjectGraph(root);

            var dot = graph.ToDot();

            graph.ProjectNodes.Count.ShouldBe(2);

            AssertInnerBuildEvaluation(graph.GraphRoots.First(), false, new Dictionary<string, string>());

            var nonMultitargetingNode = GetFirstNodeWithProjectNumber(graph, 2);

            AssertNonMultitargetingNode(nonMultitargetingNode);
        }

        [Fact]
        public void InnerBuildAsRootViaGlobalPropertyShouldNotPropagateInnerBuildPropertyToReference()
        {
            var root = CreateProjectFile(env: _env, projectNumber: 1, projectReferences: new[] {2}, projectReferenceTargets: null, defaultTargets: null, extraContent: MultitargetingSpecification).Path;
            CreateProjectFile(env: _env, projectNumber: 2);

            var graph = new ProjectGraph(root, new Dictionary<string, string>{{InnerBuildPropertyName, "foo"}});

            var dot = graph.ToDot();

            graph.ProjectNodes.Count.ShouldBe(2);

            AssertInnerBuildEvaluation(graph.GraphRoots.First(), true, new Dictionary<string, string>());

            var nonMultitargetingNode = GetFirstNodeWithProjectNumber(graph, 2);

            AssertNonMultitargetingNode(nonMultitargetingNode);
        }

        [Fact]
        public void NonMultitargetingProjectsAreCompatibleWithMultitargetingProjects()
        {
            var root = CreateProjectFile(env: _env, projectNumber: 1, projectReferences: new[] {2, 3}, projectReferenceTargets: null, defaultTargets: null, extraContent: MultitargetingSpecification).Path;
            CreateProjectFile(env: _env, projectNumber: 2, projectReferences: new[] {4});
            CreateProjectFile(env: _env, projectNumber: 3, projectReferences: new[] {4});
            CreateProjectFile(env: _env, projectNumber: 4, projectReferences: null, projectReferenceTargets: null, defaultTargets: null, extraContent: MultitargetingSpecification);

            var graph = new ProjectGraph(root);

            var dot = graph.ToDot();

            graph.ProjectNodes.Count.ShouldBe(8);

            AssertOuterBuildAsRoot(graph.GraphRoots.First());
            AssertOuterBuildAsNonRoot(GetNodesWithProjectNumber(graph, 4).First(IsOuterBuild));

            AssertNonMultitargetingNode(GetFirstNodeWithProjectNumber(graph, 2));
            AssertNonMultitargetingNode(GetFirstNodeWithProjectNumber(graph, 3));
        }

        [Fact]
        public void InnerBuildsCanHaveSeparateReferences()
        {
            var extraInnerBuildReferenceSpec = MultitargetingSpecification +
                                          $@"<ItemGroup>
                                                <ProjectReference Condition=`'$({InnerBuildPropertyName})'=='b'` Include=`4.proj;5.proj`/>
                                            </ItemGroup>".Cleanup();

            var root = CreateProjectFile(env: _env, projectNumber: 1, projectReferences: new[] {2, 3}, projectReferenceTargets: null, defaultTargets: null, extraContent: extraInnerBuildReferenceSpec).Path;
            CreateProjectFile(env: _env, projectNumber: 2, projectReferences: null, projectReferenceTargets: null, defaultTargets: null, extraContent: MultitargetingSpecification);
            CreateProjectFile(env: _env, projectNumber: 3);
            CreateProjectFile(env: _env, projectNumber: 4, projectReferences: null, projectReferenceTargets: null, defaultTargets: null, extraContent: MultitargetingSpecification);
            CreateProjectFile(env: _env, projectNumber: 5);

            var graph = new ProjectGraph(root);

            var dot = graph.ToDot();

            graph.ProjectNodes.Count.ShouldBe(11);

            AssertOuterBuildAsRoot(graph.GraphRoots.First());
            AssertOuterBuildAsNonRoot(GetNodesWithProjectNumber(graph, 2).First(IsOuterBuild));
            AssertOuterBuildAsNonRoot(GetNodesWithProjectNumber(graph, 4).First(IsOuterBuild));

            AssertNonMultitargetingNode(GetFirstNodeWithProjectNumber(graph, 3));
            AssertNonMultitargetingNode(GetFirstNodeWithProjectNumber(graph, 5));

            var innerBuildWithCommonReferences = GetNodesWithProjectNumber(graph, 1).First(n => n.ProjectInstance.GlobalProperties.TryGetValue(InnerBuildPropertyName, out string p) && p == "a");

            innerBuildWithCommonReferences.ProjectReferences.Count.ShouldBe(4);
            var referenceNumbersSet = innerBuildWithCommonReferences.ProjectReferences.Select(r => Path.GetFileNameWithoutExtension(r.ProjectInstance.FullPath)).ToHashSet();
            referenceNumbersSet.ShouldBeEquivalentTo(new HashSet<string>{"2", "3"});

            var innerBuildWithAdditionalReferences = GetNodesWithProjectNumber(graph, 1).First(n => n.ProjectInstance.GlobalProperties.TryGetValue(InnerBuildPropertyName, out string p) && p == "b");

            innerBuildWithAdditionalReferences.ProjectReferences.Count.ShouldBe(8);
            referenceNumbersSet = innerBuildWithAdditionalReferences.ProjectReferences.Select(r => Path.GetFileNameWithoutExtension(r.ProjectInstance.FullPath)).ToHashSet();
            referenceNumbersSet.ShouldBeEquivalentTo(new HashSet<string>{"2", "3", "4", "5"});
        }

        [Fact]
        public void InnerBuildProducedByOuterBuildCanBeReferencedByAnotherNode()
        {
            var referenceToInnerBuild = $@"<ItemGroup>
                                               <ProjectReference Include='1.proj' Properties='{InnerBuildPropertyName}=a'/>
                                           </ItemGroup>";

            var additionalGlobalProperties = new Dictionary<string, string>{{"x", "y"}};

            var graph = new ProjectGraph(new []
            {
                CreateProjectFile(env: _env, projectNumber: 1, projectReferences: null, projectReferenceTargets: null, defaultTargets: null, extraContent: MultitargetingSpecification).Path,
                CreateProjectFile(env: _env, projectNumber: 2, projectReferences: null, projectReferenceTargets: null, defaultTargets: null, extraContent: referenceToInnerBuild).Path
            },
            additionalGlobalProperties);

            var dot = graph.ToDot();

            graph.ProjectNodes.Count.ShouldBe(4);

            var outerBuild = graph.GraphRoots.First(IsOuterBuild);

            AssertOuterBuildAsRoot(outerBuild, additionalGlobalProperties);
            AssertNonMultitargetingNode(GetFirstNodeWithProjectNumber(graph, 2), additionalGlobalProperties);

            var referencedInnerBuild = GetNodesWithProjectNumber(graph, 1).First(n => n.ProjectInstance.GetPropertyValue(InnerBuildPropertyName) == "a");

            var two = GetFirstNodeWithProjectNumber(graph, 2);

            two.ProjectReferences.ShouldHaveSingleItem();
            two.ProjectReferences.First().ShouldBe(referencedInnerBuild);

            referencedInnerBuild.ReferencingProjects.ShouldBeEquivalentTo(new []{two, outerBuild});
        }

        [Fact]
        public void StandaloneInnerBuildsCanBeReferencedWithoutOuterBuilds()
        {
            var referenceToInnerBuild = $@"<ItemGroup>
                                               <ProjectReference Include='2.proj' Properties='{InnerBuildPropertyName}=a'/>
                                           </ItemGroup>";

            var root = CreateProjectFile(env: _env, projectNumber: 1, projectReferences: null, projectReferenceTargets: null, defaultTargets: null, extraContent: referenceToInnerBuild).Path;
            CreateProjectFile(env: _env, projectNumber: 2, projectReferences: new []{3}, projectReferenceTargets: null, defaultTargets: null, extraContent: MultitargetingSpecification + $"<PropertyGroup><{InnerBuildPropertyName}>a</{InnerBuildPropertyName}></PropertyGroup>");
            CreateProjectFile(env: _env, projectNumber: 3);

            var additionalGlobalProperties = new Dictionary<string, string>{{"x", "y"}};

            var graph = new ProjectGraph(root, additionalGlobalProperties);

            var dot = graph.ToDot();

            graph.ProjectNodes.Count.ShouldBe(3);

            var rootNode = graph.GraphRoots.First();
            AssertNonMultitargetingNode(rootNode, additionalGlobalProperties);

            rootNode.ProjectReferences.ShouldHaveSingleItem();
            var innerBuildNode = rootNode.ProjectReferences.First();

            AssertInnerBuildEvaluation(innerBuildNode, false, additionalGlobalProperties);

            innerBuildNode.ProjectReferences.ShouldHaveSingleItem();
            AssertNonMultitargetingNode(innerBuildNode.ProjectReferences.First(), additionalGlobalProperties);
        }

        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/4262")]
        public void InnerBuildsProducedByOuterBuildsCanBeReferencedByOtherInnerBuilds()
        {
            var referenceToInnerBuild = $@"<ItemGroup>
                                               <ProjectReference Include='2.proj' Condition=`'$({InnerBuildPropertyName})' == 'a'` Properties='{InnerBuildPropertyName}=a'/>
                                           </ItemGroup>".Cleanup();

            var additionalGlobalProperties = new Dictionary<string, string>{{"x", "y"}};

            var root = CreateProjectFile(
                env: _env,
                projectNumber: 1,
                projectReferences: null,
                projectReferenceTargets: null,
                defaultTargets: null,
                extraContent: MultitargetingSpecification + referenceToInnerBuild)
                .Path;

            CreateProjectFile(
                env: _env,
                projectNumber: 2,
                projectReferences: null,
                projectReferenceTargets: null,
                defaultTargets: null,
                extraContent: MultitargetingSpecification);

            var graph = new ProjectGraph(new [] { root }, additionalGlobalProperties);

            var dot = graph.ToDot();

            graph.ProjectNodes.Count.ShouldBe(5);

            var outerBuild1 = GetNodesWithProjectNumber(graph, 1).First(IsOuterBuild);

            AssertOuterBuildAsRoot(outerBuild1, additionalGlobalProperties);

            var innerBuild1WithReferenceToInnerBuild2 = outerBuild1.ProjectReferences.FirstOrDefault(n => IsInnerBuild(n) && n.ProjectInstance.GlobalProperties[InnerBuildPropertyName] == "a");
            innerBuild1WithReferenceToInnerBuild2.ShouldNotBeNull();

            var outerBuild2 = GetNodesWithProjectNumber(graph, 2).FirstOrDefault(IsOuterBuild);
            outerBuild2.ShouldNotBeNull();

            var innerBuild2 = GetNodesWithProjectNumber(graph, 2).FirstOrDefault(IsInnerBuild);
            innerBuild2.ShouldNotBeNull();

            innerBuild2.ProjectInstance.GlobalProperties[InnerBuildPropertyName].ShouldBe("a");

            // project 2 has two nodes: the outer build and the referenced inner build
            // the outer build is necessary as the referencing inner build can still call targets on it
            GetNodesWithProjectNumber(graph, 2).Count().ShouldBe(2);

            innerBuild1WithReferenceToInnerBuild2.ProjectReferences.ShouldBeEquivalentTo(new []{outerBuild2, innerBuild2});
        }

        public static IEnumerable<object[]> AllNodesShouldHaveGraphBuildGlobalPropertyData
        {
            get
            {
                var globalVariablesArray = new[]
                {
                    //todo add null
                    new Dictionary<string, string>(),
                    new Dictionary<string, string>
                    {
                        {"a", "b"},
                        {"c", "d"}
                    }
                };

                var graph1 = new Dictionary<int, int[]>
                {
                    {1, new[] {3, 2}},
                    {2, new[] {3}},
                    {3, new[] {5, 4}},
                    {4, new[] {5}}
                };

                var graph2 = new Dictionary<int, int[]>
                {
                    {1, new[] {5, 4, 7}},
                    {2, new[] {5}},
                    {3, new[] {6, 5}},
                    {4, new[] {7}},
                    {5, new[] {7, 8}},
                    {6, new[] {7, 9}}
                };

                foreach (var globalVariables in globalVariablesArray)
                {
                    yield return new object[]
                    {
                        new Dictionary<int, int[]>(),
                        new int[] {},
                        globalVariables
                    };

                    yield return new object[]
                    {
                        new Dictionary<int, int[]>
                        {
                            {1, null}
                        },
                        new[] {1},
                        globalVariables
                    };

                    yield return new object[]
                    {
                        graph1,
                        new[] {1},
                        globalVariables
                    };

                    yield return new object[]
                    {
                        graph1,
                        new[] {1, 4, 3},
                        globalVariables
                    };

                    yield return new object[]
                    {
                        graph2,
                        new[] {1, 2, 3},
                        globalVariables
                    };

                    yield return new object[]
                    {
                        graph2,
                        new[] {1, 2, 6, 4, 3, 7},
                        globalVariables
                    };
                }
            }
        }

        [Theory]
        [MemberData(nameof(AllNodesShouldHaveGraphBuildGlobalPropertyData))]
        public void AllNodesShouldHaveGraphBuildGlobalProperty(Dictionary<int, int[]> edges, int[] roots, Dictionary<string, string> globalProperties)
        {
            using (var env = TestEnvironment.Create())
            {
                var projectGraph = Helpers.CreateProjectGraph(env, edges, globalProperties, null, roots);

                var dot = projectGraph.ToDot();

                var expectedGlobalProperties = new Dictionary<string, string>(globalProperties);
                expectedGlobalProperties[PropertyNames.IsGraphBuild] = "true";

                foreach (var node in projectGraph.ProjectNodes)
                {
                    node.ProjectInstance.GlobalProperties.ShouldBeEquivalentTo(expectedGlobalProperties);
                }
            }
        }

        [Fact]
        public void UserValuesForIsGraphBuildGlobalPropertyShouldBePreserved()
        {
            using (var env = TestEnvironment.Create())
            {
                var projectGraph = Helpers.CreateProjectGraph(
                    env,
                    new Dictionary<int, int[]> {{1, null}},
                    new Dictionary<string, string> {{PropertyNames.IsGraphBuild, "xyz"}});

                projectGraph.ProjectNodes.First().ProjectInstance.GlobalProperties[PropertyNames.IsGraphBuild].ShouldBe("xyz");
            }
        }

        private static ProjectGraphNode GetFirstNodeWithProjectNumber(ProjectGraph graph, int projectNum) => GetNodesWithProjectNumber(graph, projectNum).First();

        private static IEnumerable<ProjectGraphNode> GetNodesWithProjectNumber(ProjectGraph graph, int projectNum)
        {
            return graph.ProjectNodes.Where(node => node.ProjectInstance.FullPath.EndsWith(projectNum + ".proj"));
        }

        private static string GetProjectFileName(ProjectGraphNode node) => Path.GetFileNameWithoutExtension(node.ProjectInstance.FullPath);

        internal static TransientTestFile CreateProjectFile(
            TestEnvironment env,
            int projectNumber,
            int[] projectReferences = null,
            Dictionary<string, string[]> projectReferenceTargets = null,
            string defaultTargets = null,
            string extraContent = null
            )
        {
            return Helpers.CreateProjectFile(
                env,
                projectNumber,
                projectReferences,
                projectReferenceTargets,
                // Use "Build" when the default target is unspecified since in practice that is usually the default target.
                defaultTargets ?? "Build",
                extraContent);
        }

        public void Dispose()
        {
            _env.Dispose();
        }
    }
}
