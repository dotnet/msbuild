// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Exceptions;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.Build.Experimental.Graph.UnitTests.GraphTestingUtilities;
using static Microsoft.Build.UnitTests.Helpers;

namespace Microsoft.Build.Experimental.Graph.UnitTests
{
    public class GraphLoadedFromSolutionTests : IDisposable
    {
        private TestEnvironment _env;

        public GraphLoadedFromSolutionTests(ITestOutputHelper output)
        {
            _env = TestEnvironment.Create(output);
        }

        [Theory]
        [InlineData("1.sln", "2.sln")]
        [InlineData("1.sln", "2.proj")]
        public void ASolutionShouldBeTheSingleEntryPoint(params string[] files)
        {
            _env.DoNotLaunchDebugger();

            for (var i = 0; i < files.Length; i++)
            {
                files[i] = _env.CreateFile(files[i], string.Empty).Path;
            }

            var exception = Should.Throw<ArgumentException>(
                () =>
                {
                    new ProjectGraph(files);
                });

            exception.Message.ShouldContain("MSB4261");
        }

        [Fact]
        public void GraphConstructionFailsOnNonExistentSolution()
        {
            _env.DoNotLaunchDebugger();

            var exception = Should.Throw<InvalidProjectFileException>(
                () =>
                {
                    new ProjectGraph("nonExistent.sln");
                });

            exception.Message.ShouldContain("The project file could not be loaded. Could not find file");
        }

        [Fact]
        public void StaticGraphShouldNotSupportNestedSolutions()
        {
            var solutionFile = _env.CreateFile("solutionReference.sln", string.Empty).Path;

            var referenceToSolution = $@"<ItemGroup>
                                           <ProjectReference Include='{solutionFile}' />
                                       </ItemGroup>".Cleanup();

            var root = GraphTestingUtilities.CreateProjectFile(
                env: _env,
                projectNumber: 1,
                projectReferences: null,
                projectReferenceTargets: null,
                defaultTargets: null,
                extraContent: referenceToSolution);

            _env.DoNotLaunchDebugger();

            var exception = Should.Throw<InvalidOperationException>(
                () =>
                {
                    new ProjectGraph(root.Path);
                });

            exception.Message.ShouldContain("MSB4264:");
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
                        {1, new []{2, 3}},
                        {2, new []{4}},
                        {3, new []{4}},
                        {5, new []{3, 2}}
                    }
                };
            }
        }

        public static IEnumerable<object[]> GraphsWithUniformSolutionConfigurations
        {
            get
            {
                foreach (var graph in Graphs)
                {
                    foreach (
                        var currentSolutionConfigurationPlatform in SolutionFileBuilder.SolutionConfigurationPlatformsDefaults.Concat(new SolutionConfigurationInSolution[] {null}))
                    {
                        yield return new[]
                        {
                            graph.First(),
                            currentSolutionConfigurationPlatform,
                            SolutionFileBuilder.SolutionConfigurationPlatformsDefaults
                        };
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(GraphsWithUniformSolutionConfigurations))]
        public void GraphConstructionCanLoadEntryPointsFromSolution(
            Dictionary<int, int[]> edges,
            SolutionConfigurationInSolution currentSolutionConfiguration,
            IReadOnlyCollection<SolutionConfigurationInSolution> solutionConfigurations)
        {
            AssertSolutionBasedGraph(edges, currentSolutionConfiguration, solutionConfigurations);
        }

        [Theory]
        [MemberData(nameof(GraphsWithUniformSolutionConfigurations))]
        public void SolutionBasedGraphCanMatchProjectSpecificConfigurations(
            Dictionary<int, int[]> edges,
            SolutionConfigurationInSolution currentSolutionConfiguration,
            IReadOnlyCollection<SolutionConfigurationInSolution> solutionConfigurations)
        {
            var graph = CreateProjectGraph(_env, edges);

            var projectSpecificConfigurations = graph.ProjectNodes.ToDictionary(
                node => GetProjectNumber(node).ToString(),
                n => solutionConfigurations.ToDictionary(
                    sc => sc,
                    sc => new ProjectConfigurationInSolution(
                        configurationName: $"{sc.ConfigurationName}_{GetProjectNumber(n)}",
                        platformName: $"{sc.PlatformName}_{GetProjectNumber(n)}",
                        includeInBuild: true)));

            AssertSolutionBasedGraph(edges, currentSolutionConfiguration, solutionConfigurations, projectSpecificConfigurations);
        }

        [Fact]
        public void SolutionParserIgnoresProjectConfigurationsThatDoNotFullyMatchAnySolutionConfiguration()
        {
            var solutionContents = new SolutionFileBuilder
            {
                Projects = new Dictionary<string, string>
                {
                    {"1", _env.CreateFile("1.csproj", string.Empty).Path}
                },
                SolutionConfigurationPlatforms = new[] {new SolutionConfigurationInSolution("Foo", "Bar")},
                ProjectConfigurations = new Dictionary<string, Dictionary<SolutionConfigurationInSolution, ProjectConfigurationInSolution>>
                {
                    {
                        "1", new Dictionary<SolutionConfigurationInSolution, ProjectConfigurationInSolution>
                        {
                            {
                                new SolutionConfigurationInSolution("NonMatchingConfiguration", "NonMatchingPlatform"),
                                new ProjectConfigurationInSolution("1a", "1b", true)
                            },
                            {
                                new SolutionConfigurationInSolution("Foo", "NonMatchingPlatform"),
                                new ProjectConfigurationInSolution("1c", "1d", true)
                            }
                        }
                    }
                }
            }.BuildSolution();

            var solutionFile = _env.CreateFile("solution.sln", solutionContents).Path;

            var graph = new ProjectGraph(solutionFile);

            graph.ProjectNodes.ShouldBeEmpty();
        }

        private void AssertSolutionBasedGraph(
            Dictionary<int, int[]> edges,
            SolutionConfigurationInSolution currentSolutionConfiguration,
            IReadOnlyCollection<SolutionConfigurationInSolution> solutionConfigurations,
            Dictionary<string, Dictionary<SolutionConfigurationInSolution, ProjectConfigurationInSolution>> projectConfigurations = null)
        {
            var graph = CreateProjectGraph(_env, edges);

            var solutionFileBuilder = new SolutionFileBuilder
            {
                Projects = graph.ProjectNodes.ToDictionary(
                    n => GetProjectNumber(n)
                        .ToString(),
                    n => n.ProjectInstance.FullPath),
                ProjectConfigurations = projectConfigurations
            };

            var solutionContents = solutionFileBuilder.BuildSolution();

            var solutionPath = _env.CreateFile("TheSolution.sln", solutionContents).Path;
            var globalProperties = currentSolutionConfiguration != null
                ? new Dictionary<string, string>
                {
                    ["Configuration"] = currentSolutionConfiguration.ConfigurationName,
                    ["Platform"] = currentSolutionConfiguration.PlatformName
                }
                : new Dictionary<string, string>();

            var graphFromSolution = new ProjectGraph(
                new ProjectGraphEntryPoint(
                    solutionPath,
                    globalProperties),
                _env.CreateProjectCollection().Collection);

            // in the solution, all nodes are entry points
            graphFromSolution.EntryPointNodes.Select(GetProjectPath)
                .ShouldBeSetEquivalentTo(graph.ProjectNodes.Select(GetProjectPath));

            if (projectConfigurations == null || graphFromSolution.ProjectNodes.All(n => n.ProjectReferences.Count == 0))
            {
                graphFromSolution.GraphRoots.Select(GetProjectPath)
                    .ShouldBeEquivalentTo(graph.GraphRoots.Select(GetProjectPath));

                graphFromSolution.ProjectNodes.Select(GetProjectPath)
                    .ShouldBeEquivalentTo(graph.ProjectNodes.Select(GetProjectPath));
            }

            var expectedCurrentConfiguration = currentSolutionConfiguration ?? solutionConfigurations.First();
            var actualProjectConfigurations = projectConfigurations ?? solutionFileBuilder.ProjectConfigurations;

            foreach (var node in graphFromSolution.ProjectNodes)
            {
                // Project references get duplicated, once as entry points from the solution (handled in the if block) and once as nodes
                // produced by ProjectReference items (handled in the else block).
                if (node.ReferencingProjects.Count == 0)
                {
                    var expectedProjectConfiguration = actualProjectConfigurations[GetProjectNumber(node).ToString()][expectedCurrentConfiguration];
                    GetConfiguration(node).ShouldBe(expectedProjectConfiguration.ConfigurationName);
                    GetPlatform(node).ShouldBe(expectedProjectConfiguration.PlatformName);
                }
                else
                {
                    GetConfiguration(node).ShouldBe(GetConfiguration(node.ReferencingProjects.First()));
                    GetPlatform(node).ShouldBe(GetPlatform(node.ReferencingProjects.First()));
                }
            }
        }

        private static string GetConfiguration(ProjectGraphNode node)
        {
            return node.ProjectInstance.GlobalProperties["Configuration"];
        }

        private static string GetPlatform(ProjectGraphNode node)
        {
            return node.ProjectInstance.GlobalProperties["Platform"];
        }

        public void Dispose()
        {
            _env.Dispose();
        }
    }
}
