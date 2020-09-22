// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.Build.UnitTests.Helpers;

using ExpectedNodeBuildOutput = System.Collections.Generic.Dictionary<Microsoft.Build.Graph.ProjectGraphNode, string[]>;
using OutputCacheDictionary = System.Collections.Generic.Dictionary<Microsoft.Build.Graph.ProjectGraphNode, string>;

namespace Microsoft.Build.Graph.UnitTests
{
    public class ResultCacheBasedBuilds_Tests : IDisposable
    {
        public ResultCacheBasedBuilds_Tests(ITestOutputHelper output)
        {
            _output = output;
            _env = TestEnvironment.Create(_output);
            _logger = new MockLogger(_output);
        }

        // isolated is turned on for either input or output
        public void Dispose()
        {
            _env.Dispose();
        }

        private readonly ITestOutputHelper _output;
        private readonly TestEnvironment _env;
        private readonly MockLogger _logger;

        [Theory]
        [InlineData(new byte[] {})]
        [InlineData(new byte[] {1})]
        [InlineData(new byte[] {0})]
        [InlineData(new byte[] {1, 1})]
        [InlineData(new byte[] {1, 1, 90, 23})]
        public void InvalidCacheFilesShouldLogError(byte[] cacheContents)
        {
            var project = CreateProjectFileWithBuildTargetAndItems(_env, 1).Path;
            var existingFile = _env.CreateFile(
                "FileExists",
                new string(
                    cacheContents.Select(Convert.ToChar)
                        .ToArray())).Path;

            var result = BuildProjectFileUsingBuildManager(
                project,
                _logger,
                new BuildParameters
                {
                    InputResultsCacheFiles = new[] {existingFile}
                });

            result.OverallResult.ShouldBe(BuildResultCode.Failure);

            _logger.FullLog.ShouldContain("MSB4256:");
            _logger.AllBuildEvents.Count.ShouldBe(4);
            _logger.ErrorCount.ShouldBe(1);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void ShouldGeneratePathForEmptyOutputPath(string emptyInput)
        {
            var project = CreateProjectFileWithBuildTargetAndItems(_env, 1).Path;

            var result = BuildProjectFileUsingBuildManager(
                project,
                _logger,
                new BuildParameters
                {
                    OutputResultsCacheFile = emptyInput
                });

            result.OverallResult.ShouldBe(BuildResultCode.Success);

            _logger.FullLog.ShouldContain("Writing build results caches to:");
            _logger.FullLog.ShouldContain("msbuild-cache");
            _logger.ErrorCount.ShouldBe(0);
        }

        [Fact]
        public void CachesGetLogged()
        {
            using (var buildManager = new BuildManager())
            {
                buildManager.BeginBuild(new BuildParameters
                {
                    InputResultsCacheFiles = new []{"a", "b"},
                    OutputResultsCacheFile = "c",
                    Loggers = new []{_logger}
                });

                buildManager.EndBuild();
            }

            _logger.FullLog.ShouldContain("Using input build results caches: a;b");
            _logger.FullLog.ShouldContain("Writing build results caches to: c");
            _logger.Errors.First().Message.ShouldContain("MSB4255:");
            _logger.ErrorCount.ShouldBe(1);
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("", "Build")]
        [InlineData("Build", "")]
        [InlineData("Build", "Build")]
        public void RebuildSingleProjectFromCache(string defaultTargets, string explicitTargets)
        {
            var projectFile = CreateProjectFileWithBuildTargetAndItems(_env, 1, null, defaultTargets, explicitTargets).Path;

            var outputCache = _env.DefaultTestDirectory.CreateFile("referenceCache").Path;

            var result = BuildProjectFileUsingBuildManager(
                projectFile,
                null,
                new BuildParameters
                {
                    OutputResultsCacheFile = outputCache
                });

            result.OverallResult.ShouldBe(BuildResultCode.Success);
            result.ResultsByTarget["Build"].Items.Length.ShouldBe(1);
            result.ResultsByTarget["Build"].Items.First()
                .ItemSpec.ShouldBe("1");
            File.Exists(outputCache).ShouldBeTrue();

            var project = Project.FromFile(
                projectFile,
                new ProjectOptions
                {
                    ProjectCollection = _env.CreateProjectCollection()
                        .Collection
                });

            project.RemoveItems(project.GetItems("i"));
            project.Save();

            // can see new changes when not using the cache
            result = BuildProjectFileUsingBuildManager(projectFile);

            result.OverallResult.ShouldBe(BuildResultCode.Success);
            result.ResultsByTarget["Build"].Items.ShouldBeEmpty();

            // cannot see new changes when loading from cache
            var resultFromCachedBuild = BuildProjectFileUsingBuildManager(
                projectFile,
                _logger,
                new BuildParameters
                {
                    InputResultsCacheFiles = new[] {outputCache}
                });

            resultFromCachedBuild.OverallResult.ShouldBe(BuildResultCode.Success);
            resultFromCachedBuild.ResultsByTarget["Build"].Items.Length.ShouldBe(1);
            resultFromCachedBuild.ResultsByTarget["Build"].Items.First().ItemSpec.ShouldBe("1");
            _logger.ErrorCount.ShouldBe(0);
        }

        public static IEnumerable<object[]> BuildGraphData
        {
            get
            {
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
                        {1, new[] {2}}
                    }
                };

                yield return new object[]
                {
                    new Dictionary<int, int[]>
                    {
                        {1, new[] {2}},
                        {2, null}
                    }
                };

                yield return new object[]
                {
                    new Dictionary<int, int[]>
                    {
                        {1, new[] {2}},
                        {2, new[] {3}}
                    }
                };

                yield return new object[]
                {
                    new Dictionary<int, int[]>
                    {
                        {1, new[] {2, 3}}
                    }
                };

                yield return new object[]
                {
                    new Dictionary<int, int[]>
                    {
                        {1, new[] {2, 3}},
                        {2, new[] {4}},
                        {3, new[] {4}}
                    }
                };

                yield return new object[]
                {
                    new Dictionary<int, int[]>
                    {
                        {1, new[] {2, 3, 4}},
                        {2, new[] {4}},
                        {3, new[] {4}}
                    }
                };

                yield return new object[]
                {
                    new Dictionary<int, int[]>
                    {
                        {1, new[] {2}},
                        {3, new[] {4}}
                    }
                };

                yield return new object[]
                {
                    new Dictionary<int, int[]>
                    {
                        {1, new[] {2, 4}},
                        {3, new[] {4}}
                    }
                };

                yield return new object[]
                {
                    new Dictionary<int, int[]>
                    {
                        {1, new[] {4, 5}},
                        {2, new[] {5}},
                        {3, new[] {5, 6}},
                        {4, new[] {7}},
                        {5, new[] {7, 8}},
                        {6, new[] {7, 9}}
                    }
                };
            }
        }

        [Theory]
        [MemberData(nameof(BuildGraphData))]
        public void BuildProjectGraphUsingCaches(Dictionary<int, int[]> edges)
        {
            var topoSortedNodes =
                CreateProjectGraph(
                    env: _env,
                    dependencyEdges: edges,
                    globalProperties: null,
                    createProjectFile: CreateProjectFileWrapper)
                    .ProjectNodesTopologicallySorted.ToArray();

            var expectedOutput = new ExpectedNodeBuildOutput();

            var outputCaches = new OutputCacheDictionary();

            // Build unchanged project files using caches.
            BuildUsingCaches(topoSortedNodes, expectedOutput, outputCaches, generateCacheFiles: true);

            // Change the project files to remove all items.
            var collection = _env.CreateProjectCollection().Collection;

            foreach (var node in topoSortedNodes)
            {
                var project = Project.FromFile(
                    node.ProjectInstance.FullPath,
                    new ProjectOptions
                    {
                        ProjectCollection = collection
                    });

                project.RemoveItems(project.GetItems("i"));
                project.Save();
            }

            // Build again using the first caches. Project file changes from references should not be visible.
            BuildUsingCaches(
                topoSortedNodes,
                expectedOutput,
                outputCaches,
                generateCacheFiles: false,
                assertBuildResults: true,
                // there are no items in the second build. The references are loaded from cache and have items,
                // but the current project is loaded from file and has no items
                (node, localExpectedOutput) => localExpectedOutput[node].Skip(1).ToArray());
        }

        [Fact]
        public void OutputCacheShouldNotContainInformationFromInputCaches()
        {
            var topoSortedNodes =
                CreateProjectGraph(
                    env: _env,
                    dependencyEdges: new Dictionary<int, int[]> { { 1, new[] { 2, 3 } } },
                    globalProperties: null,
                    createProjectFile: CreateProjectFileWrapper)
                    .ProjectNodesTopologicallySorted.ToArray();

            var expectedOutput = new ExpectedNodeBuildOutput();

            var outputCaches = new OutputCacheDictionary();

            BuildUsingCaches(topoSortedNodes, expectedOutput, outputCaches, generateCacheFiles: true);

            var rootNode = topoSortedNodes.First(n => Path.GetFileNameWithoutExtension(n.ProjectInstance.FullPath) == "1");
            var outputCache = outputCaches[rootNode];

            outputCache.ShouldNotBeNull();

            var deserializationInfo = CacheSerialization.DeserializeCaches(outputCache);

            deserializationInfo.exception.ShouldBeNull();

            var buildResults = deserializationInfo.ResultsCache.GetEnumerator().ToArray();
            buildResults.ShouldHaveSingleItem();

            var rootNodeBuildResult = buildResults.First();
            rootNodeBuildResult.ResultsByTarget["Build"].Items.Select(i => i.ItemSpec).ToArray().ShouldBe(expectedOutput[rootNode]);

            var configEntries = deserializationInfo.ConfigCache.GetEnumerator().ToArray();
            configEntries.ShouldHaveSingleItem();

            configEntries.First().ConfigurationId.ShouldBe(rootNodeBuildResult.ConfigurationId);
        }

        [Fact]
        public void MissingResultFromCacheShouldErrorDueToIsolatedBuildCacheEnforcement()
        {
            var topoSortedNodes =
                CreateProjectGraph(
                    env: _env,
                    dependencyEdges: new Dictionary<int, int[]> { { 1, new[] { 2, 3 } } },
                    globalProperties: null,
                    createProjectFile: CreateProjectFileWrapper)
                    .ProjectNodesTopologicallySorted.ToArray();

            var expectedOutput = new ExpectedNodeBuildOutput();

            var outputCaches = new OutputCacheDictionary();

            BuildUsingCaches(topoSortedNodes, expectedOutput, outputCaches, generateCacheFiles: true);

            // remove cache for project 3 to cause a cache miss
            outputCaches.Remove(expectedOutput.Keys.First(n => ProjectNumber(n) == "3"));

            var results = BuildUsingCaches(topoSortedNodes, expectedOutput, outputCaches, generateCacheFiles: false, assertBuildResults: false);

            results["3"].Result.OverallResult.ShouldBe(BuildResultCode.Success);
            results["2"].Result.OverallResult.ShouldBe(BuildResultCode.Success);

            results["1"].Result.OverallResult.ShouldBe(BuildResultCode.Failure);
            results["1"].Logger.ErrorCount.ShouldBe(1);
            results["1"].Logger.Errors.First().Message.ShouldContain("MSB4252");

            results["1"].Logger.Errors.First().BuildEventContext.NodeId.ShouldNotBe(BuildEventContext.InvalidNodeId);
            results["1"].Logger.Errors.First().BuildEventContext.ProjectInstanceId.ShouldNotBe(BuildEventContext.InvalidProjectInstanceId);
            results["1"].Logger.Errors.First().BuildEventContext.ProjectContextId.ShouldNotBe(BuildEventContext.InvalidProjectContextId);
            results["1"].Logger.Errors.First().BuildEventContext.TargetId.ShouldNotBe(BuildEventContext.InvalidTargetId);
            results["1"].Logger.Errors.First().BuildEventContext.TaskId.ShouldNotBe(BuildEventContext.InvalidTaskId);
        }

        /// <summary>
        /// This method runs in two modes.
        /// When <param name="generateCacheFiles"></param> is true, the method will fill in the empty <param name="outputCaches"/> and <param name="expectedNodeBuildOutput"/>, simulating a build from scratch.
        /// When it is false, it uses the filled in <param name="outputCaches"/> and <param name="expectedNodeBuildOutput"/> to simulate a fully cached build.
        /// 
        /// </summary>
        /// <param name="topoSortedNodes"></param>
        /// <param name="expectedNodeBuildOutput"></param>
        /// <param name="outputCaches"></param>
        /// <param name="generateCacheFiles"></param>
        /// <param name="assertBuildResults"></param>
        /// <param name="expectedOutputProducer"></param>
        /// <returns></returns>
        private Dictionary<string, (BuildResult Result, MockLogger Logger)> BuildUsingCaches(
            IReadOnlyCollection<ProjectGraphNode> topoSortedNodes,
            ExpectedNodeBuildOutput expectedNodeBuildOutput,
            OutputCacheDictionary outputCaches,
            bool generateCacheFiles,
            bool assertBuildResults = true,
            // (current node, expected output dictionary) -> actual expected output for current node
            Func<ProjectGraphNode, ExpectedNodeBuildOutput, string[]> expectedOutputProducer = null)
        {
            expectedOutputProducer ??= ((node, expectedOutputs) => expectedOutputs[node]);

            var results = new Dictionary<string, (BuildResult Result, MockLogger Logger)>(topoSortedNodes.Count);

            if (generateCacheFiles)
            {
                outputCaches.ShouldBeEmpty();
                expectedNodeBuildOutput.ShouldBeEmpty();
            }

            foreach (var node in topoSortedNodes)
            {
                if (generateCacheFiles)
                {
                    expectedNodeBuildOutput[node] = ExpectedBuildOutputForNode(node);
                }

                var cacheFilesForReferences = node.ProjectReferences.Where(r => outputCaches.ContainsKey(r)).Select(r => outputCaches[r]).ToArray();

                var buildParameters = new BuildParameters
                {
                    InputResultsCacheFiles = cacheFilesForReferences
                };

                if (generateCacheFiles)
                {
                    outputCaches[node] = _env.DefaultTestDirectory.CreateFile($"OutputCache-{ProjectNumber(node)}").Path;
                    buildParameters.OutputResultsCacheFile = outputCaches[node];
                }

                var logger = new MockLogger();

                buildParameters.Loggers = new[] {logger};

                var result = BuildProjectFileUsingBuildManager(
                    node.ProjectInstance.FullPath,
                    null,
                    buildParameters);

                results[ProjectNumber(node)] = (result, logger);

                if (assertBuildResults)
                {
                    result.OverallResult.ShouldBe(BuildResultCode.Success);

                    var actualOutput = result.ResultsByTarget["Build"].Items.Select(i => i.ItemSpec).ToArray();

                    var expectedOutputForNode = expectedOutputProducer(node, expectedNodeBuildOutput);

                    actualOutput.ShouldBe(expectedOutputForNode);
                }
            }

            return results;

            string[] ExpectedBuildOutputForNode(ProjectGraphNode node)
            {
                var expectedOutputForNode = new List<string>();

                expectedOutputForNode.Add(ProjectNumber(node));

                foreach (var referenceOutput in node.ProjectReferences.SelectMany(n => expectedNodeBuildOutput[n]))
                {
                    if (!expectedOutputForNode.Contains(referenceOutput))
                    {
                        expectedOutputForNode.Add(referenceOutput);
                    }
                }

                return expectedOutputForNode.ToArray();
            }
        }

        private static string ProjectNumber(ProjectGraphNode node) => Path.GetFileNameWithoutExtension(node.ProjectInstance.FullPath);

        private static TransientTestFile CreateProjectFileWrapper(TestEnvironment env, int projectNumber, int[] projectReferences, Dictionary<string, string[]> projectReferenceTargets, string defaultTargets, string extraContent)
        {
            return CreateProjectFileWithBuildTargetAndItems(env, projectNumber, projectReferences, defaultTargets);
        }

        internal static TransientTestFile CreateProjectFileWithBuildTargetAndItems(
            TestEnvironment env,
            int projectNumber,
            int[] projectReferences = null,
            string defaultTargets = null,
            string explicitTargets = null
            )
        {
            var sb = new StringBuilder();

            sb.Append(
                projectReferences == null
                    ? @"<Target Name='Build' Returns='@(i)'/>"
                    : $@"<Target Name='Build' Returns='@(i)'>
                        <MSBuild
                            Projects='{string.Join(";", projectReferences.Select(i => $"{i}.proj"))}'
                            {(explicitTargets != null
                                ? $"Targets='{explicitTargets}'"
                                : string.Empty)}
                            >
                            <Output TaskParameter='TargetOutputs' ItemName='i' />  
                        </MSBuild>
                    </Target>");

            sb.Append(@"<ItemGroup>
                            <i Include='").Append(projectNumber).Append(@"'/>
                        </ItemGroup>");

            return CreateProjectFile(
                env,
                projectNumber,
                projectReferences,
                null,
                defaultTargets,
                sb.ToString()
                );
        }

        [Fact]
        public void NonExistingInputResultsCacheShouldLogError()
        {
            var project = CreateProjectFileWithBuildTargetAndItems(_env, 1).Path;
            var existingFile = _env.CreateFile("FileExists", string.Empty).Path;

            var result = BuildProjectFileUsingBuildManager(
                project,
                _logger,
                new BuildParameters
                {
                    InputResultsCacheFiles = new[] {"FileDoesNotExist1", existingFile, "FileDoesNotExist2"}
                });

            result.OverallResult.ShouldBe(BuildResultCode.Failure);

            _logger.AllBuildEvents.Count.ShouldBe(4);
            _logger.Errors.First().Message.ShouldContain("MSB4255:");
            _logger.Errors.First().Message.ShouldContain("FileDoesNotExist1");
            _logger.Errors.First().Message.ShouldContain("FileDoesNotExist2");
            _logger.ErrorCount.ShouldBe(1);
        }
    }
}
