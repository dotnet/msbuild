// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Graph.UnitTests
{
    public class ProjectGraphTests
    {
        [Fact]
        public void ConstructWithSingleNode()
        {
            using (var env = TestEnvironment.Create())
            {
                TransientTestFile entryProject = CreateProject(env, 1);
                var projectGraph = new ProjectGraph(entryProject.Path);
                projectGraph.ProjectNodes.Count.ShouldBe(1);
                projectGraph.ProjectNodes.First().Project.FullPath.ShouldBe(entryProject.Path);
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
                TransientTestFile entryProject = CreateProject(env, 1, new[] { 2, 3 });
                CreateProject(env, 2);
                CreateProject(env, 3);
                ProjectGraph graph = new ProjectGraph(entryProject.Path);

                graph.ProjectNodes.Count.ShouldBe(3);
                GetNodeForProject(graph, 1).ProjectReferences.Count.ShouldBe(2);
                GetNodeForProject(graph, 2).ProjectReferences.Count.ShouldBe(0);
                GetNodeForProject(graph, 3).ProjectReferences.Count.ShouldBe(0);
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
                CreateProject(env, 1, new[] { 4, 5 });
                TransientTestFile entryProject = CreateProject(env, 2, new[] { 3, 5, 6 });
                CreateProject(env, 3);
                CreateProject(env, 4);
                CreateProject(env, 5, new[] { 7 });
                CreateProject(env, 6, new[] { 1 });
                CreateProject(env, 7);

                ProjectGraph graph = new ProjectGraph(entryProject.Path);

                graph.ProjectNodes.Count.ShouldBe(7);
                GetNodeForProject(graph, 1).ProjectReferences.Count.ShouldBe(2);
                GetNodeForProject(graph, 2).ProjectReferences.Count.ShouldBe(3);
                GetNodeForProject(graph, 3).ProjectReferences.Count.ShouldBe(0);
                GetNodeForProject(graph, 4).ProjectReferences.Count.ShouldBe(0);
                GetNodeForProject(graph, 5).ProjectReferences.Count.ShouldBe(1);
                GetNodeForProject(graph, 6).ProjectReferences.Count.ShouldBe(1);
                GetNodeForProject(graph, 7).ProjectReferences.Count.ShouldBe(0);

                // confirm that there is a path from 2 -> 6 -> 1 -> 5 -> 7
                GetNodeForProject(graph, 2).ProjectReferences.ShouldContain(GetNodeForProject(graph, 6));
                GetNodeForProject(graph, 6).ProjectReferences.ShouldContain(GetNodeForProject(graph, 1));
                GetNodeForProject(graph, 1).ProjectReferences.ShouldContain(GetNodeForProject(graph, 5));
                GetNodeForProject(graph, 5).ProjectReferences.ShouldContain(GetNodeForProject(graph, 7));
            }
        }

        [Fact]
        public void ConstructWithCycle()
        {
            using (var env = TestEnvironment.Create())
            {
                TransientTestFile entryProject = CreateProject(env, 1, new[] { 2 });
                CreateProject(env, 2, new[] { 3 });
                CreateProject(env, 3, new[] { 1 });

                // TODO: This should eventually throw, but for now not infinite-looping is sufficient.
                ProjectGraph graph = new ProjectGraph(entryProject.Path);
                graph.ProjectNodes.Count.ShouldBe(3);
            }
        }

        [Fact]
        public void GetTargetListsAggregatesFromMultipleEdges()
        {
            using (var env = TestEnvironment.Create())
            {
                TransientTestFile entryProject = CreateProject(env, 1, new[] { 2, 3 }, new Dictionary<string, string[]> { { "A", new[] { "B" } } });
                CreateProject(env, 2, new[] { 4 }, new Dictionary<string, string[]> { { "B", new[] { "C" } } });
                CreateProject(env, 3, new[] { 4 }, new Dictionary<string, string[]> { { "B", new[] { "D" } } });
                CreateProject(env, 4);

                var projectGraph = new ProjectGraph(entryProject.Path);
                projectGraph.ProjectNodes.Count.ShouldBe(4);

                IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> targetLists = projectGraph.GetTargetLists(new[] { "A" });
                targetLists.Count.ShouldBe(projectGraph.ProjectNodes.Count);
                targetLists[GetNodeForProject(projectGraph, 1)].ShouldBe(new[] { "A" });
                targetLists[GetNodeForProject(projectGraph, 2)].ShouldBe(new[] { "B" });
                targetLists[GetNodeForProject(projectGraph, 3)].ShouldBe(new[] { "B" });
                targetLists[GetNodeForProject(projectGraph, 4)].ShouldBe(new[] { "C", "D" }); // From B => C and B => D
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
                TransientTestFile entryProject = CreateProject(env, 1, new[] { 2 }, projectReferenceTargets);
                CreateProject(env, 2, new[] { 3 }, projectReferenceTargets);
                CreateProject(env, 3, Array.Empty<int>(), projectReferenceTargets);

                var projectGraph = new ProjectGraph(entryProject.Path);
                projectGraph.ProjectNodes.Count.ShouldBe(3);

                IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> targetLists = projectGraph.GetTargetLists(new[] { "A" });
                targetLists.Count.ShouldBe(projectGraph.ProjectNodes.Count);
                targetLists[GetNodeForProject(projectGraph, 1)].ShouldBe(new[] { "A" });
                targetLists[GetNodeForProject(projectGraph, 2)].ShouldBe(new[] { "B", "X", "C" });
                targetLists[GetNodeForProject(projectGraph, 3)].ShouldBe(new[] { "X", "Y", "Z" }); // Simplified from X, Y, X, Z
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
                TransientTestFile entryProject = CreateProject(env, 1, new[] { 2, 3, 5 }, projectReferenceTargets);
                CreateProject(env, 2, new[] { 4, 5 }, projectReferenceTargets);
                CreateProject(env, 3, new[] { 5, 6 }, projectReferenceTargets);
                CreateProject(env, 4, new[] { 5 }, projectReferenceTargets);
                CreateProject(env, 5, new[] { 6 }, projectReferenceTargets);
                CreateProject(env, 6, Array.Empty<int>(), projectReferenceTargets);

                var projectGraph = new ProjectGraph(entryProject.Path);
                projectGraph.ProjectNodes.Count.ShouldBe(6);

                IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> targetLists = projectGraph.GetTargetLists(new[] { "A" });
                targetLists.Count.ShouldBe(projectGraph.ProjectNodes.Count);
                targetLists[GetNodeForProject(projectGraph, 1)].ShouldBe(new[] { "A" });
                targetLists[GetNodeForProject(projectGraph, 2)].ShouldBe(new[] { "B" });
                targetLists[GetNodeForProject(projectGraph, 3)].ShouldBe(new[] { "B" });
                targetLists[GetNodeForProject(projectGraph, 4)].ShouldBe(new[] { "C" });
                targetLists[GetNodeForProject(projectGraph, 5)].ShouldBe(new[] { "B", "C", "D" });
                targetLists[GetNodeForProject(projectGraph, 6)].ShouldBe(new[] { "C", "D", "E" });
            }
        }

        [Fact]
        public void GetTargetListsNullEntryTargets()
        {
            using (var env = TestEnvironment.Create())
            {
                TransientTestFile entryProject = CreateProject(env, 1, new[] { 2 }, new Dictionary<string, string[]> { { "A", new[] { "B" } } }, "A");
                CreateProject(env, 2);

                var projectGraph = new ProjectGraph(entryProject.Path);
                projectGraph.ProjectNodes.Count.ShouldBe(2);

                IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> targetLists = projectGraph.GetTargetLists(null);
                targetLists.Count.ShouldBe(projectGraph.ProjectNodes.Count);
                targetLists[GetNodeForProject(projectGraph, 1)].ShouldBe(new[] { "A" });
                targetLists[GetNodeForProject(projectGraph, 2)].ShouldBe(new[] { "B" });
            }
        }

        [Fact]
        public void GetTargetListsDefaultTargetsAreExpanded()
        {
            using (var env = TestEnvironment.Create())
            {
                TransientTestFile entryProject = CreateProject(env, 1, new[] { 2 }, new Dictionary<string, string[]> { { "A", new[] { ".default" } } }, defaultTargets: "A");
                CreateProject(env, 2, defaultTargets: "B");

                var projectGraph = new ProjectGraph(entryProject.Path);
                projectGraph.ProjectNodes.Count.ShouldBe(2);

                IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> targetLists = projectGraph.GetTargetLists(null);
                targetLists.Count.ShouldBe(projectGraph.ProjectNodes.Count);
                targetLists[GetNodeForProject(projectGraph, 1)].ShouldBe(new[] { "A" });
                targetLists[GetNodeForProject(projectGraph, 2)].ShouldBe(new[] { "B" });
            }
        }

        [Fact]
        public void GetTargetListsUnspecifiedTargetsDefaultToBuild()
        {
            using (var env = TestEnvironment.Create())
            {
                TransientTestFile entryProject = CreateProject(env, 1, new[] { 2 }, new Dictionary<string, string[]> { { "Build", new[] { "A", ".default" } } });
                CreateProject(env, 2);

                var projectGraph = new ProjectGraph(entryProject.Path);
                projectGraph.ProjectNodes.Count.ShouldBe(2);

                IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> targetLists = projectGraph.GetTargetLists(null);
                targetLists.Count.ShouldBe(projectGraph.ProjectNodes.Count);
                targetLists[GetNodeForProject(projectGraph, 1)].ShouldBe(new[] { "Build" });
                targetLists[GetNodeForProject(projectGraph, 2)].ShouldBe(new[] { "A", "Build" });
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
                TransientTestFile entryProject = CreateProject(env, 1, new[] { 2, 3, 4 }, projectReferenceTargets, defaultTargets: null);
                CreateProject(env, 2, new[] { 5 }, projectReferenceTargets, defaultTargets: null);
                CreateProject(env, 3, new[] { 6 }, projectReferenceTargets, defaultTargets: "X");
                CreateProject(env, 4, new[] { 7 }, projectReferenceTargets, defaultTargets: "Y");
                CreateProject(env, 5, defaultTargets: null);
                CreateProject(env, 6, defaultTargets: null);
                CreateProject(env, 7, defaultTargets: "Z;W");

                var projectGraph = new ProjectGraph(entryProject.Path);
                projectGraph.ProjectNodes.Count.ShouldBe(7);

                IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> targetLists = projectGraph.GetTargetLists(null);
                targetLists.Count.ShouldBe(projectGraph.ProjectNodes.Count);
                targetLists[GetNodeForProject(projectGraph, 1)].ShouldBe(new[] { "Build" });
                targetLists[GetNodeForProject(projectGraph, 2)].ShouldBe(new[] { "A", "Build" });
                targetLists[GetNodeForProject(projectGraph, 3)].ShouldBe(new[] { "A", "X" });
                targetLists[GetNodeForProject(projectGraph, 4)].ShouldBe(new[] { "A", "Y" });
                targetLists[GetNodeForProject(projectGraph, 5)].ShouldBe(new[] { "A", "Build" });
                targetLists[GetNodeForProject(projectGraph, 6)].ShouldBe(new[] { "B", "Build" });
                targetLists[GetNodeForProject(projectGraph, 7)].ShouldBe(new[] { "C", "Z", "W" });
            }
        }

        private static TransientTestFile CreateProject(
            TestEnvironment env,
            int projectNumber,
            int[] projectReferences = null,
            Dictionary<string, string[]> projectReferenceTargets = null,
            string defaultTargets = null)
        {
            var sb = new StringBuilder(64);

            // Use "Build" when the default target is unspecified since in practice that is usually the default target.
            sb.AppendFormat("<Project DefaultTargets=\"{0}\"><ItemGroup>", defaultTargets ?? "Build");

            if (projectReferences != null)
            {
                foreach (int projectReference in projectReferences)
                {
                    sb.AppendFormat("<ProjectReference Include=\"{0}.proj\" />", projectReference);
                }
            }

            if (projectReferenceTargets != null)
            {
                foreach (KeyValuePair<string, string[]> pair in projectReferenceTargets)
                {
                    sb.AppendFormat("<ProjectReferenceTargets Include=\"{0}\" Targets=\"{1}\" />", pair.Key, string.Join(";", pair.Value));
                }
            }

            sb.Append("</ItemGroup></Project>");

            return env.CreateFile(projectNumber + ".proj", sb.ToString());
        }

        private static ProjectGraphNode GetNodeForProject(ProjectGraph graph, int projectNum) => graph.ProjectNodes.First(node => node.Project.FullPath.EndsWith(projectNum + ".proj"));
    }

}
