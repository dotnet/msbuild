// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Execution;
using Microsoft.Build.Experimental.ProjectCache;
using Microsoft.Build.Framework;
using Microsoft.Build.Graph;
using Microsoft.Build.Shared;
using Microsoft.Build.Unittest;
using Microsoft.Build.UnitTests;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.Build.Engine.UnitTests.ProjectCache
{
    public class ProjectCacheTests : IDisposable
    {
        public ProjectCacheTests(ITestOutputHelper output)
        {
            _output = output;
            _env = TestEnvironment.Create(output);

            BuildManager.ProjectCacheItems.ShouldBeEmpty();
            _env.WithInvariant(new CustomConditionInvariant(() => BuildManager.ProjectCacheItems.Count == 0));
        }

        public void Dispose()
        {
            _env.Dispose();
        }

        private static readonly string AssemblyMockCache = nameof(AssemblyMockCache);

        private static readonly Lazy<string> SamplePluginAssemblyPath =
            new Lazy<string>(
                () =>
                {
                    return Directory.EnumerateFiles(
                        Path.GetFullPath(
                            Path.Combine(
                                BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory,
                                "..",
                                "..",
                                "..",
                                "Samples",
                                "ProjectCachePlugin")),
                        "ProjectCachePlugin.dll",
                        SearchOption.AllDirectories).First();
                });

        public class GraphCacheResponse
        {
            public const string CacheHitByProxy = nameof(CacheHitByProxy);
            public const string CacheHitByTargetResult = nameof(CacheHitByTargetResult);

            private static readonly string P2PTargets =
                @$"
                    <ItemGroup>
                        <ProjectReferenceTargets Include=`Build` Targets=`Build` />
                        <{ItemTypeNames.ProjectCachePlugin} Include=`{SamplePluginAssemblyPath.Value}` />
                    </ItemGroup>

                    <Target Name=`Build` Returns=`@(ReturnValue)`>
                        <MSBuild Projects=`@(ProjectReference)` Targets=`Build`>
                            <Output TaskParameter=`TargetOutputs` ItemName=`ReferenceReturns` />
                        </MSBuild>

                        <Message Text=`Reference: %(ReferenceReturns.Identity) : %(ReferenceReturns.File)` Importance=`High` />
                        <Error Text=`Reference file [%(ReferenceReturns.File)] does not exist` Condition=`@(ReferenceReturns->Count()) != 0 and !Exists(%(ReferenceReturns.File))` />

                        <ItemGroup>
                            <ReturnValue Include=`$(MSBuildProjectName)` File=`$(MSBuildProjectFile)` />
                        </ItemGroup>
                    </Target>

                    <Target Name=`ProxyBuild` Returns=`@(ReturnValue)`>
                        <ItemGroup>
                            <ReturnValue Include=`$(MSBuildProjectName)` File=`$(MSBuildProjectFile)` {CacheHitByProxy}=`true`/>
                        </ItemGroup>
                    </Target>";

            private Dictionary<int, int[]> GraphEdges { get; }

            public Dictionary<int, CacheResult> NonCacheMissResults { get; }

            public GraphCacheResponse(Dictionary<int, int[]> graphEdges, Dictionary<int, CacheResult>? nonCacheMissResults = null)
            {
                GraphEdges = graphEdges;
                NonCacheMissResults = nonCacheMissResults ?? new Dictionary<int, CacheResult>();
            }

            public ProjectGraph CreateGraph(TestEnvironment env)
            {
                return Helpers.CreateProjectGraph(
                    env,
                    GraphEdges,
                    null,
                    P2PTargets);
            }

            public static CacheResult SuccessfulProxyTargetResult()
            {
                return CacheResult.IndicateCacheHit(
                    new ProxyTargets(
                        new Dictionary<string, string>
                        {
                            {"ProxyBuild", "Build"}
                        }));
            }

            public static CacheResult SuccessfulTargetResult(int projectNumber, string projectPath)
            {
                return CacheResult.IndicateCacheHit(
                    new[]
                    {
                        new PluginTargetResult(
                            "Build",
                            new ITaskItem2[]
                            {
                                new TaskItem(
                                    projectNumber.ToString(),
                                    new Dictionary<string, string>
                                    {
                                        {"File", projectPath},
                                        {CacheHitByTargetResult, "true"}
                                    })
                            },
                            BuildResultCode.Success
                            )
                    });
            }

            public CacheResult GetExpectedCacheResultForNode(ProjectGraphNode node)
            {
                return GetExpectedCacheResultForProjectNumber(GetProjectNumber(node));
            }

            public CacheResult GetExpectedCacheResultForProjectNumber(int projectNumber)
            {
                return NonCacheMissResults.TryGetValue(projectNumber, out var cacheResult)
                    ? cacheResult
                    : CacheResult.IndicateNonCacheHit(CacheResultType.CacheMiss);
            }

            public override string ToString()
            {
                //return base.ToString();
                return string.Join(
                    ", ",
                    GraphEdges.Select(e => $"{Node(e.Key)}->{FormatChildren(e.Value)}"));

                string FormatChildren(int[] children)
                {
                    return children == null
                        ? "Null"
                        : string.Join(",", children.Select(c => Node(c)));
                }

                string Node(int projectNumber)
                {
                    return $"{projectNumber}({Chr(projectNumber)})";
                }

                char Chr(int projectNumber)
                {
                    var cacheResult = GetExpectedCacheResultForProjectNumber(projectNumber);
                    return cacheResult.ResultType switch
                    {

                        CacheResultType.CacheHit => cacheResult.ProxyTargets != null
                            ? 'P'
                            : 'T',
                        CacheResultType.CacheMiss => 'M',
                        CacheResultType.CacheNotApplicable => 'N',
                        CacheResultType.None => 'E',
                        _ => throw new ArgumentOutOfRangeException()
                        };
                }
            }
        }

        [Flags]
        public enum ErrorLocations
        {
            Constructor = 1 << 0,
            BeginBuildAsync = 1 << 1,
            GetCacheResultAsync = 1 << 2,
            EndBuildAsync = 1 << 3
        }

        public enum ErrorKind
        {
            Exception,
            LoggedError
        }

        public class InstanceMockCache : ProjectCachePluginBase
        {
            private readonly GraphCacheResponse? _testData;
            public ConcurrentQueue<BuildRequestData> Requests { get; } = new ConcurrentQueue<BuildRequestData>();

            public bool BeginBuildCalled { get; set; }
            public bool EndBuildCalled { get; set; }

            public InstanceMockCache(GraphCacheResponse? testData = null)
            {
                _testData = testData;
            }

            public override Task BeginBuildAsync(CacheContext context, PluginLoggerBase logger, CancellationToken cancellationToken)
            {
                logger.LogMessage("MockCache: BeginBuildAsync", MessageImportance.High);

                BeginBuildCalled = true;

                return Task.CompletedTask;
            }

            public override Task<CacheResult> GetCacheResultAsync(
                BuildRequestData buildRequest,
                PluginLoggerBase logger,
                CancellationToken cancellationToken)
            {
                Requests.Enqueue(buildRequest);
                logger.LogMessage($"MockCache: GetCacheResultAsync for {buildRequest.ProjectFullPath}", MessageImportance.High);

                return
                    Task.FromResult(
                        _testData?.GetExpectedCacheResultForProjectNumber(GetProjectNumber(buildRequest.ProjectFullPath))
                        ?? CacheResult.IndicateNonCacheHit(CacheResultType.CacheMiss));
            }

            public override Task EndBuildAsync(PluginLoggerBase logger, CancellationToken cancellationToken)
            {
                logger.LogMessage("MockCache: EndBuildAsync", MessageImportance.High);

                EndBuildCalled = true;

                return Task.CompletedTask;
            }

            public CacheResult GetCacheResultForNode(ProjectGraphNode node)
            {
                throw new NotImplementedException();
            }
        }

        private readonly TestEnvironment _env;

        private readonly ITestOutputHelper _output;

        public static IEnumerable<GraphCacheResponse> SuccessfulGraphs
        {
            get
            {
                yield return new GraphCacheResponse(
                    new Dictionary<int, int[]>
                    {
                        {1, null!}
                    });

                yield return new GraphCacheResponse(
                    new Dictionary<int, int[]>
                    {
                        {1, null!}
                    },
                    new Dictionary<int, CacheResult>
                    {
                        {1, GraphCacheResponse.SuccessfulProxyTargetResult()}
                    });

                yield return new GraphCacheResponse(
                    new Dictionary<int, int[]>
                    {
                        {1, null!}
                    },
                    new Dictionary<int, CacheResult>
                    {
                        {1, GraphCacheResponse.SuccessfulTargetResult(1, "1.proj")}
                    });

                yield return new GraphCacheResponse(
                    new Dictionary<int, int[]>
                    {
                        {1, new[] {2}}
                    });

                yield return new GraphCacheResponse(
                    new Dictionary<int, int[]>
                    {
                        {1, new[] {2}}
                    },
                    new Dictionary<int, CacheResult>
                    {
                        {2, GraphCacheResponse.SuccessfulProxyTargetResult()}
                    });

                yield return new GraphCacheResponse(
                    new Dictionary<int, int[]>
                    {
                        {1, new[] {2}}
                    },
                    new Dictionary<int, CacheResult>
                    {
                        {2, GraphCacheResponse.SuccessfulTargetResult(2, "2.proj")}
                    });

                yield return new GraphCacheResponse(
                    new Dictionary<int, int[]>
                    {
                        {1, new[] {2}}
                    },
                    new Dictionary<int, CacheResult>
                    {
                        {1, GraphCacheResponse.SuccessfulProxyTargetResult()},
                        {2, GraphCacheResponse.SuccessfulTargetResult(2, "2.proj")}
                    });

                yield return new GraphCacheResponse(
                    new Dictionary<int, int[]>
                    {
                        {1, new[] {2, 3, 7}},
                        {2, new[] {4}},
                        {3, new[] {4}},
                        {4, new[] {5, 6, 7}}
                    });
            }
        }

        public static IEnumerable<object[]> MultiProcWithAndWithoutInProcNode
        {
            get
            {
                yield return new object[]
                {
                    new BuildParameters
                    {
                        DisableInProcNode = false,
                        MaxNodeCount = Environment.ProcessorCount
                    }
                };

                yield return new object[]
                {
                    new BuildParameters
                    {
                        DisableInProcNode = true,
                        MaxNodeCount = Environment.ProcessorCount
                    }
                };
            }
        }

        public static IEnumerable<object[]> SuccessfulGraphsWithBuildParameters
        {
            get
            {
                foreach (var graph in SuccessfulGraphs)
                {
                    foreach (var buildParameters in MultiProcWithAndWithoutInProcNode)
                    {
                        yield return new object[]
                        {
                            graph,
                            ((BuildParameters) buildParameters.First()).Clone()
                        };
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(SuccessfulGraphsWithBuildParameters))]
        public void ProjectCacheByBuildParametersAndGraphBuildWorks(GraphCacheResponse testData, BuildParameters buildParameters)
        {
            _output.WriteLine(testData.ToString());
            var graph = testData.CreateGraph(_env);
            var mockCache = new InstanceMockCache(testData);

            buildParameters.ProjectCacheDescriptor = ProjectCacheDescriptor.FromInstance(
                mockCache,
                null,
                graph);

            using var buildSession = new Helpers.BuildManagerSession(_env, buildParameters);

            var graphResult = buildSession.BuildGraph(graph);

            if (buildParameters.DisableInProcNode
                && testData.NonCacheMissResults.Any(c => c.Value.ProxyTargets is not null))
            {
                // TODO: remove this branch when the DisableInProcNode failure is fixed by https://github.com/dotnet/msbuild/pull/6400
                graphResult.OverallResult.ShouldBe(BuildResultCode.Failure);
                buildSession.Logger.Errors.First().Code.ShouldBe("MSB4223");
                return;
            }

            graphResult.OverallResult.ShouldBe(BuildResultCode.Success);

            buildSession.Dispose();

            buildSession.Logger.FullLog.ShouldContain("Static graph based");

            AssertCacheBuild(graph, testData, mockCache, buildSession.Logger, graphResult.ResultsByNode);
        }

        [Theory]
        [MemberData(nameof(SuccessfulGraphsWithBuildParameters))]
        public void ProjectCacheByBuildParametersAndBottomUpBuildWorks(GraphCacheResponse testData, BuildParameters buildParameters)
        {
            var graph = testData.CreateGraph(_env);
            var mockCache = new InstanceMockCache(testData);

            buildParameters.ProjectCacheDescriptor = ProjectCacheDescriptor.FromInstance(
                mockCache,
                null,
                graph);

            using var buildSession = new Helpers.BuildManagerSession(_env, buildParameters);
            var nodesToBuildResults = new Dictionary<ProjectGraphNode, BuildResult>();

            foreach (var node in graph.ProjectNodesTopologicallySorted)
            {
                var buildResult = buildSession.BuildProjectFile(node.ProjectInstance.FullPath);

                if (buildParameters.DisableInProcNode &&
                    testData.NonCacheMissResults.TryGetValue(GetProjectNumber(node), out var cacheResult) &&
                    cacheResult.ProxyTargets is not null)
                {
                    // TODO: remove this branch when the DisableInProcNode failure is fixed by https://github.com/dotnet/msbuild/pull/6400
                    buildResult.OverallResult.ShouldBe(BuildResultCode.Failure);
                    buildSession.Logger.Errors.First().Code.ShouldBe("MSB4223");
                    return;
                }

                buildResult.OverallResult.ShouldBe(BuildResultCode.Success);

                nodesToBuildResults[node] = buildResult;
            }

            buildSession.Dispose();

            buildSession.Logger.FullLog.ShouldContain("Static graph based");

            AssertCacheBuild(graph, testData, mockCache, buildSession.Logger, nodesToBuildResults);
        }

        [Theory]
        [MemberData(nameof(SuccessfulGraphsWithBuildParameters))]
        public void ProjectCacheByVSWorkaroundWorks(GraphCacheResponse testData, BuildParameters buildParameters)
        {
            var currentBuildEnvironment = BuildEnvironmentHelper.Instance;

            try
            {
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(
                    new BuildEnvironment(
                        currentBuildEnvironment.Mode,
                        currentBuildEnvironment.CurrentMSBuildExePath,
                        currentBuildEnvironment.RunningTests,
                        true,
                        currentBuildEnvironment.VisualStudioInstallRootDirectory));

                BuildManager.ProjectCacheItems.ShouldBeEmpty();

                var graph = testData.CreateGraph(_env);

                BuildManager.ProjectCacheItems.ShouldHaveSingleItem();

                using var buildSession = new Helpers.BuildManagerSession(_env, buildParameters);
                var nodesToBuildResults = new Dictionary<ProjectGraphNode, BuildResult>();

                foreach (var node in graph.ProjectNodesTopologicallySorted)
                {
                    var buildResult = buildSession.BuildProjectFile(
                        node.ProjectInstance.FullPath,
                        globalProperties:
                            new Dictionary<string, string> {{"SolutionPath", graph.GraphRoots.First().ProjectInstance.FullPath}});
                    buildResult.OverallResult.ShouldBe(BuildResultCode.Success);

                    nodesToBuildResults[node] = buildResult;
                }

                buildSession.Logger.FullLog.ShouldContain("Graph entrypoint based");

                AssertCacheBuild(graph, testData, null, buildSession.Logger, nodesToBuildResults);
            }
            finally
            {
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(currentBuildEnvironment);
                BuildManager.ProjectCacheItems.Clear();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void RunningProxyBuildsOnOutOfProcNodesShouldIssueWarning(bool disableInprocNodeViaEnvironmentVariable)
        {
            var testData = new GraphCacheResponse(
                new Dictionary<int, int[]>
                {
                    {1, new[] {2}}
                },
                new Dictionary<int, CacheResult>
                {
                    {1, GraphCacheResponse.SuccessfulProxyTargetResult()},
                    {2, GraphCacheResponse.SuccessfulProxyTargetResult()}
                });

            var graph = testData.CreateGraph(_env);
            var mockCache = new InstanceMockCache(testData);

            var buildParameters = new BuildParameters
            {
                MaxNodeCount = Environment.ProcessorCount,
                ProjectCacheDescriptor = ProjectCacheDescriptor.FromInstance(
                    mockCache,
                    null,
                    graph)
            };

            if (disableInprocNodeViaEnvironmentVariable)
            {
                _env.SetEnvironmentVariable("MSBUILDNOINPROCNODE", "1");
            }
            else
            {
                buildParameters.DisableInProcNode = true;
            }

            using var buildSession = new Helpers.BuildManagerSession(_env, buildParameters);

            var graphResult = buildSession.BuildGraph(graph);

            if (!disableInprocNodeViaEnvironmentVariable)
            {
                // TODO: remove this branch when the DisableInProcNode failure is fixed by https://github.com/dotnet/msbuild/pull/6400
                graphResult.OverallResult.ShouldBe(BuildResultCode.Failure);
                buildSession.Logger.Errors.First().Code.ShouldBe("MSB4223");
                return;
            }

            graphResult.OverallResult.ShouldBe(BuildResultCode.Success);

            buildSession.Dispose();

            buildSession.Logger.FullLog.ShouldContain("Static graph based");

            buildSession.Logger.AssertMessageCount("MSB4274", 1);

        }

        private void AssertCacheBuild(
            ProjectGraph graph,
            GraphCacheResponse testData,
            InstanceMockCache? instanceMockCache,
            MockLogger mockLogger,
            IReadOnlyDictionary<ProjectGraphNode, BuildResult> projectPathToBuildResults)
        {
            if (instanceMockCache != null)
            {
                mockLogger.FullLog.ShouldContain("MockCache: BeginBuildAsync");
                mockLogger.FullLog.ShouldContain("Instance based");
                mockLogger.FullLog.ShouldNotContain("Assembly path based");

                instanceMockCache.Requests.Count.ShouldBe(graph.ProjectNodes.Count);
            }
            else
            {
                mockLogger.FullLog.ShouldContain($"{AssemblyMockCache}: BeginBuildAsync");
                mockLogger.FullLog.ShouldContain("Assembly path based");
                mockLogger.FullLog.ShouldNotContain("Instance based");

                Regex.Matches(mockLogger.FullLog, $"{AssemblyMockCache}: GetCacheResultAsync for").Count.ShouldBe(graph.ProjectNodes.Count);
            }

            foreach (var node in graph.ProjectNodes)
            {
                var expectedCacheResponse = testData.GetExpectedCacheResultForNode(node);

                mockLogger.FullLog.ShouldContain($"====== Querying project cache for project {node.ProjectInstance.FullPath}");

                if (instanceMockCache != null)
                {
                    instanceMockCache.Requests.ShouldContain(r => r.ProjectFullPath.Equals(node.ProjectInstance.FullPath));
                    instanceMockCache.BeginBuildCalled.ShouldBeTrue();
                    instanceMockCache.EndBuildCalled.ShouldBeTrue();
                }
                else
                {
                    mockLogger.FullLog.ShouldContain($"{AssemblyMockCache}: GetCacheResultAsync for {node.ProjectInstance.FullPath}");
                }

                if (instanceMockCache == null)
                {
                    // Too complicated, not worth it to send expected results to the assembly plugin, so skip checking the build results.
                    continue;
                }

                switch (expectedCacheResponse.ResultType)
                {
                    case CacheResultType.CacheHit:
                        AssertBuildResultForCacheHit(node.ProjectInstance.FullPath, projectPathToBuildResults[node], expectedCacheResponse);
                        break;
                    case CacheResultType.CacheMiss:
                        break;
                    case CacheResultType.CacheNotApplicable:
                        break;
                    case CacheResultType.None:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private static int GetProjectNumber(ProjectGraphNode node)
        {
            return GetProjectNumber(node.ProjectInstance.FullPath);
        }

        private static int GetProjectNumber(string projectPath)
        {
            return int.Parse(Path.GetFileNameWithoutExtension(projectPath));
        }

        private void AssertBuildResultForCacheHit(
            string projectPath,
            BuildResult buildResult,
            CacheResult expectedCacheResponse)
        {
            // If the cache hit is via proxy targets then the build result should contain entry for both the real target
            // and the proxy target. Both target results should be the same.
            // If it's not a cache result by proxy targets then the cache constructed the target results by hand and only the real target result
            // exists in the BuildResult.

            var targetResult = buildResult.ResultsByTarget["Build"];

            targetResult.Items.ShouldHaveSingleItem();
            var itemResult = targetResult.Items.First();
            string expectedMetadata;

            if (expectedCacheResponse.ProxyTargets != null)
            {
                var proxyTargetResult = buildResult.ResultsByTarget["ProxyBuild"];
                SdkUtilities.EngineHelpers.AssertTargetResultsEqual(targetResult, proxyTargetResult);

                expectedMetadata = GraphCacheResponse.CacheHitByProxy;
            }
            else
            {
                expectedMetadata = GraphCacheResponse.CacheHitByTargetResult;
            }

            itemResult.ItemSpec.ShouldBe(GetProjectNumber(projectPath).ToString());
            itemResult.GetMetadata("File").ShouldBe(Path.GetFileName(projectPath));
            itemResult.GetMetadata(expectedMetadata).ShouldBe("true");
        }

        [Theory]
        [MemberData(nameof(MultiProcWithAndWithoutInProcNode))]
        public void CacheShouldNotGetQueriedForNestedBuildRequests(BuildParameters buildParameters)
        {
            var project1 = _env.CreateFile("1.proj", @"
                    <Project>
                        <Target Name=`Build`>
                            <MSBuild Projects=`2.proj` />
                        </Target>
                    </Project>".Cleanup());

            _env.CreateFile("2.proj", @"
                    <Project>
                        <Target Name=`Build`>
                            <Message Text=`Hello` Importance=`High` />
                        </Target>
                    </Project>".Cleanup());

            var mockCache = new InstanceMockCache();
            buildParameters.ProjectCacheDescriptor = ProjectCacheDescriptor.FromInstance(
                mockCache,
                new[] {new ProjectGraphEntryPoint(project1.Path)},
                null);

            using var buildSession = new Helpers.BuildManagerSession(_env, buildParameters);

            var buildResult = buildSession.BuildProjectFile(project1.Path);

            buildResult.OverallResult.ShouldBe(BuildResultCode.Success);

            buildSession.Logger.ProjectStartedEvents.Count.ShouldBe(2);

            mockCache.Requests.Count.ShouldBe(1);
            mockCache.Requests.First().ProjectFullPath.ShouldEndWith("1.proj");
        }

        [Fact]
        public void CacheViaBuildParametersCanDiscoverAndLoadPluginFromAssembly()
        {
            var testData = new GraphCacheResponse(
                new Dictionary<int, int[]>
                {
                    {1, new[] {2, 3}}
                }
                );

            var graph = testData.CreateGraph(_env);

            using var buildSession = new Helpers.BuildManagerSession(
                _env,
                new BuildParameters
                {
                    ProjectCacheDescriptor = ProjectCacheDescriptor.FromAssemblyPath(
                        SamplePluginAssemblyPath.Value,
                        graph.EntryPointNodes.Select(n => new ProjectGraphEntryPoint(n.ProjectInstance.FullPath)).ToArray(),
                        null)
                });

            var graphResult = buildSession.BuildGraph(graph);

            graphResult.OverallResult.ShouldBe(BuildResultCode.Success);

            buildSession.Logger.FullLog.ShouldContain("Graph entrypoint based");

            AssertCacheBuild(graph, testData, null, buildSession.Logger, graphResult.ResultsByNode);
        }

        [Fact]
        public void GraphBuildCanDiscoverAndLoadPluginFromAssembly()
        {
            var testData = new GraphCacheResponse(
                new Dictionary<int, int[]>
                {
                    {1, new[] {2, 3}}
                }
                );

            var graph = testData.CreateGraph(_env);

            using var buildSession = new Helpers.BuildManagerSession(_env);

            var graphResult = buildSession.BuildGraph(graph);

            graphResult.OverallResult.ShouldBe(BuildResultCode.Success);

            buildSession.Logger.FullLog.ShouldContain("Static graph based");

            AssertCacheBuild(graph, testData, null, buildSession.Logger, graphResult.ResultsByNode);
        }

        [Fact]
        public void BuildFailsWhenCacheBuildResultIsWrong()
        {
            var testData = new GraphCacheResponse(
                new Dictionary<int, int[]>
                {
                    {1, new[] {2}}
                },
                new Dictionary<int, CacheResult>
                {
                    {
                        2, CacheResult.IndicateCacheHit(
                            new[]
                            {
                                new PluginTargetResult(
                                    "Build",
                                    new ITaskItem2[]
                                    {
                                        new TaskItem(
                                            "NA",
                                            new Dictionary<string, string>
                                            {
                                                {"File", "Invalid file"}
                                            })
                                    },
                                    BuildResultCode.Success
                                    )
                            })
                    }
                }
                );

            var graph = testData.CreateGraph(_env);
            var mockCache = new InstanceMockCache(testData);

            using var buildSession = new Helpers.BuildManagerSession(
                _env,
                new BuildParameters
                {
                    ProjectCacheDescriptor =
                        ProjectCacheDescriptor.FromInstance(mockCache, null, graph)
                });

            var buildResult = buildSession.BuildGraph(graph);

            mockCache.Requests.Count.ShouldBe(2);

            buildResult.ResultsByNode.First(r => GetProjectNumber(r.Key) == 2).Value.OverallResult.ShouldBe(BuildResultCode.Success);
            buildResult.ResultsByNode.First(r => GetProjectNumber(r.Key) == 1).Value.OverallResult.ShouldBe(BuildResultCode.Failure);

            buildResult.OverallResult.ShouldBe(BuildResultCode.Failure);

            buildSession.Logger.FullLog.ShouldContain("Reference file [Invalid file] does not exist");
        }

        [Fact]
        public void GraphBuildErrorsIfMultiplePluginsAreFound()
        {
            _env.DoNotLaunchDebugger();

            var graph = Helpers.CreateProjectGraph(
                _env,
                new Dictionary<int, int[]>
                {
                    {1, new[] {2}}
                },
                extraContentPerProjectNumber: null,
                extraContentForAllNodes: @$"
<ItemGroup>
   <{ItemTypeNames.ProjectCachePlugin} Include='Plugin$(MSBuildProjectName)' />
</ItemGroup>
");

            using var buildSession = new Helpers.BuildManagerSession(_env);

            var graphResult = buildSession.BuildGraph(graph);

            graphResult.OverallResult.ShouldBe(BuildResultCode.Failure);
            graphResult.Exception.Message.ShouldContain("A single project cache plugin must be specified but multiple where found:");
        }

        [Fact]
        public void GraphBuildErrorsIfNotAllNodeDefineAPlugin()
        {
            _env.DoNotLaunchDebugger();

            var graph = Helpers.CreateProjectGraph(
                _env,
                dependencyEdges: new Dictionary<int, int[]>
                {
                    {1, new[] {2}}
                },
                extraContentPerProjectNumber: new Dictionary<int, string>
                {
                    {
                        2,
                        @$"
<ItemGroup>
   <{ItemTypeNames.ProjectCachePlugin} Include='Plugin$(MSBuildProjectName)' />
</ItemGroup>
"
                    }
                });

            using var buildSession = new Helpers.BuildManagerSession(_env);

            var graphResult = buildSession.BuildGraph(graph);

            graphResult.OverallResult.ShouldBe(BuildResultCode.Failure);
            graphResult.Exception.Message.ShouldContain("When any static graph node defines a project cache, all nodes must define the same project cache.");
        }

        public static IEnumerable<object[]> CacheExceptionLocationsTestData
        {
            get
            {
                // Plugin constructors cannot log errors, they can only throw exceptions.
                yield return new object[] { ErrorLocations.Constructor, ErrorKind.Exception };

                foreach (var errorKind in new[]{ErrorKind.Exception, ErrorKind.LoggedError})
                {
                    yield return new object[] { ErrorLocations.BeginBuildAsync, errorKind };
                    yield return new object[] { ErrorLocations.BeginBuildAsync | ErrorLocations.GetCacheResultAsync, errorKind };
                    yield return new object[] { ErrorLocations.BeginBuildAsync | ErrorLocations.GetCacheResultAsync | ErrorLocations.EndBuildAsync, errorKind };
                    yield return new object[] { ErrorLocations.BeginBuildAsync | ErrorLocations.EndBuildAsync, errorKind };

                    yield return new object[] { ErrorLocations.GetCacheResultAsync, errorKind };
                    yield return new object[] { ErrorLocations.GetCacheResultAsync | ErrorLocations.EndBuildAsync, errorKind };

                    yield return new object[] { ErrorLocations.EndBuildAsync, errorKind };
                }
            }
        }

        [Theory]
        [MemberData(nameof(CacheExceptionLocationsTestData))]
        public void EngineShouldHandleExceptionsFromCachePluginViaBuildParameters(ErrorLocations errorLocations, ErrorKind errorKind)
        {
            _env.DoNotLaunchDebugger();

            SetEnvironmentForErrorLocations(errorLocations, errorKind.ToString());

            var project = _env.CreateFile("1.proj", @$"
                    <Project>
                        <Target Name=`Build`>
                            <Message Text=`Hello World` Importance=`High` />
                        </Target>
                    </Project>".Cleanup());

            Helpers.BuildManagerSession? buildSession = null;
            MockLogger logger;

            try
            {
                buildSession = new Helpers.BuildManagerSession(
                    _env,
                    new BuildParameters
                    {
                        UseSynchronousLogging = true,
                        ProjectCacheDescriptor = ProjectCacheDescriptor.FromAssemblyPath(
                            SamplePluginAssemblyPath.Value,
                            new[] {new ProjectGraphEntryPoint(project.Path)},
                            null)
                    });

                logger = buildSession.Logger;
                var buildResult = buildSession.BuildProjectFile(project.Path);

                // Plugin construction, initialization, and query all end up throwing in BuildManager.ExecuteSubmission and thus
                // mark the submission as failed with exception.
                var exceptionsThatEndUpInBuildResult =
                    ErrorLocations.Constructor | ErrorLocations.BeginBuildAsync | ErrorLocations.GetCacheResultAsync;

                if ((exceptionsThatEndUpInBuildResult & errorLocations) != 0)
                {
                    buildResult.Exception.ShouldNotBeNull();
                    buildResult.Exception.ShouldBeOfType<ProjectCacheException>();

                    if (errorKind == ErrorKind.Exception)
                    {
                        buildResult.Exception.InnerException!.ShouldNotBeNull();
                        buildResult.Exception.InnerException!.Message.ShouldContain("Cache plugin exception from");
                    }
                    else
                    {
                        buildResult.Exception.InnerException.ShouldBeNull();
                    }
                }

                // BuildManager.EndBuild calls plugin.EndBuild, so if only plugin.EndBuild fails it means everything else passed,
                // so the build submission should be successful.
                if (errorLocations == ErrorLocations.EndBuildAsync)
                {
                    buildResult.OverallResult.ShouldBe(BuildResultCode.Success);
                }
                else
                {
                    buildResult.OverallResult.ShouldBe(BuildResultCode.Failure);
                }
            }
            finally
            {
                // These exceptions prevent the creation of a plugin so there's no plugin to shutdown.
                var exceptionsThatPreventEndBuildFromThrowing = ErrorLocations.Constructor |
                                                                ErrorLocations.BeginBuildAsync;

                if ((errorLocations & exceptionsThatPreventEndBuildFromThrowing) != 0 ||
                    !errorLocations.HasFlag(ErrorLocations.EndBuildAsync))
                {
                    Should.NotThrow(() => buildSession!.Dispose());
                }
                else if (errorLocations.HasFlag(ErrorLocations.EndBuildAsync))
                {
                    var e = Should.Throw<ProjectCacheException>(() => buildSession!.Dispose());

                    if (errorKind == ErrorKind.Exception)
                    {
                        e.InnerException!.ShouldNotBeNull();
                        e.InnerException!.Message.ShouldContain("Cache plugin exception from EndBuildAsync");
                    }
                    else
                    {
                        e.InnerException.ShouldBeNull();
                    }
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            logger.BuildFinishedEvents.First().Succeeded.ShouldBeFalse();

            // Plugin query must happen after plugin init. So if plugin init fails, then the plugin should not get queried.
            var exceptionsThatShouldPreventCacheQueryAndEndBuildAsync = ErrorLocations.Constructor | ErrorLocations.BeginBuildAsync;

            if ((exceptionsThatShouldPreventCacheQueryAndEndBuildAsync & errorLocations) != 0)
            {
                logger.FullLog.ShouldNotContain($"{AssemblyMockCache}: GetCacheResultAsync for");
                logger.FullLog.ShouldNotContain($"{AssemblyMockCache}: EndBuildAsync");
            }
            else
            {
                StringShouldContainSubstring(logger.FullLog, $"{AssemblyMockCache}: GetCacheResultAsync for", expectedOccurrences: 1);
                StringShouldContainSubstring(logger.FullLog, $"{AssemblyMockCache}: EndBuildAsync", expectedOccurrences: 1);
            }

            logger.FullLog.ShouldNotContain("Cache plugin exception from");

            if (errorKind == ErrorKind.LoggedError)
            {
                logger.FullLog.ShouldContain("Cache plugin logged error from");
            }
        }

        [Theory]
        [MemberData(nameof(CacheExceptionLocationsTestData))]
        public void EngineShouldHandleExceptionsFromCachePluginViaGraphBuild(ErrorLocations errorLocations, ErrorKind errorKind)
        {
            _env.DoNotLaunchDebugger();

            SetEnvironmentForErrorLocations(errorLocations, errorKind.ToString());

            var graph = Helpers.CreateProjectGraph(
                _env,
                new Dictionary<int, int[]>
                {
                    {1, new []{2}}
                },
                extraContentPerProjectNumber:null,
                extraContentForAllNodes:@$"
<ItemGroup>
    <{ItemTypeNames.ProjectCachePlugin} Include=`{SamplePluginAssemblyPath.Value}` />
    <{ItemTypeNames.ProjectReferenceTargets} Include=`Build` Targets=`Build` />
</ItemGroup>
<Target Name=`Build`>
    <Message Text=`Hello World` Importance=`High` />
</Target>
"
                );

            var buildSession = new Helpers.BuildManagerSession(
                _env,
                new BuildParameters
                {
                    UseSynchronousLogging = true,
                    MaxNodeCount = 1
                });

            var logger = buildSession.Logger;

            GraphBuildResult? buildResult = null;

            try
            {
                buildResult = buildSession.BuildGraph(graph);

                logger.FullLog.ShouldContain("Loading the following project cache plugin:");

                // Static graph build initializes and tears down the cache plugin so all cache plugin exceptions should end up in the GraphBuildResult
                buildResult.OverallResult.ShouldBe(BuildResultCode.Failure);

                buildResult.Exception.ShouldBeOfType<ProjectCacheException>();

                if (errorKind == ErrorKind.Exception)
                {
                    buildResult.Exception.InnerException!.ShouldNotBeNull();
                    buildResult.Exception.InnerException!.Message.ShouldContain("Cache plugin exception from");
                }

                logger.FullLog.ShouldNotContain("Cache plugin exception from");

                if (errorKind == ErrorKind.LoggedError)
                {
                    logger.FullLog.ShouldContain("Cache plugin logged error from");
                }
            }
            finally
            {
                // Since all plugin exceptions during a graph build end up in the GraphBuildResult, they should not get rethrown by BM.EndBuild
                Should.NotThrow(() => buildSession.Dispose());
            }

            logger.BuildFinishedEvents.First().Succeeded.ShouldBeFalse();

            var exceptionsThatShouldPreventCacheQueryAndEndBuildAsync = ErrorLocations.Constructor | ErrorLocations.BeginBuildAsync;

            if ((exceptionsThatShouldPreventCacheQueryAndEndBuildAsync & errorLocations) != 0)
            {
                logger.FullLog.ShouldNotContain($"{AssemblyMockCache}: GetCacheResultAsync for");
                logger.FullLog.ShouldNotContain($"{AssemblyMockCache}: EndBuildAsync");
            }
            else
            {
                // There's two projects, so there should be two cache queries logged ... unless a cache queries throws an exception. That ends the build.
                var expectedQueryOccurrences = errorLocations.HasFlag(ErrorLocations.GetCacheResultAsync)
                    ? 1
                    : 2;

                StringShouldContainSubstring(logger.FullLog, $"{AssemblyMockCache}: GetCacheResultAsync for", expectedQueryOccurrences);

                StringShouldContainSubstring(logger.FullLog, $"{AssemblyMockCache}: EndBuildAsync", expectedOccurrences: 1);
            }
        }

        [Fact]
        public void EndBuildShouldGetCalledOnceWhenItThrowsExceptionsFromGraphBuilds()
        {
            _env.DoNotLaunchDebugger();

            var project = _env.CreateFile(
                "1.proj",
                @$"
                    <Project>
                        <ItemGroup>
                            <{ItemTypeNames.ProjectCachePlugin} Include=`{SamplePluginAssemblyPath.Value}` />
                        </ItemGroup>
                        <Target Name=`Build`>
                            <Message Text=`Hello EngineShouldHandleExceptionsFromCachePlugin` Importance=`High` />
                        </Target>
                    </Project>".Cleanup());

            SetEnvironmentForErrorLocations(ErrorLocations.EndBuildAsync, ErrorKind.Exception.ToString());

            using var buildSession = new Helpers.BuildManagerSession(
                _env,
                new BuildParameters
                {
                    UseSynchronousLogging = true
                });

            var logger = buildSession.Logger;

            GraphBuildResult? buildResult = null;
            Should.NotThrow(
                () =>
                {
                    buildResult = buildSession.BuildGraph(new ProjectGraph(project.Path));
                });

            buildResult!.OverallResult.ShouldBe(BuildResultCode.Failure);
            buildResult.Exception.InnerException!.ShouldNotBeNull();
            buildResult.Exception.InnerException!.Message.ShouldContain("Cache plugin exception from EndBuildAsync");

            buildSession.Dispose();

            StringShouldContainSubstring(logger.FullLog, $"{nameof(AssemblyMockCache)}: EndBuildAsync", expectedOccurrences: 1);
        }

        private static void StringShouldContainSubstring(string aString, string substring, int expectedOccurrences)
        {
            aString.ShouldContain(substring);
            Regex.Matches(aString, substring).Count.ShouldBe(expectedOccurrences);
        }

        private void SetEnvironmentForErrorLocations(ErrorLocations errorLocations, string errorKind)
        {
            foreach (var enumValue in Enum.GetValues(typeof(ErrorLocations)))
            {
                var typedValue = (ErrorLocations) enumValue;
                if (errorLocations.HasFlag(typedValue))
                {
                    var exceptionLocation = typedValue.ToString();
                    _env.SetEnvironmentVariable(exceptionLocation, errorKind);
                    _output.WriteLine($"Set exception location: {exceptionLocation}");
                }
            }
        }
    }
}
