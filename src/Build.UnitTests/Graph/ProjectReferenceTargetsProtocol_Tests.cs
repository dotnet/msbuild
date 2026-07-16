// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Shouldly;
using static Microsoft.Build.Graph.UnitTests.GraphTestingUtilities;

#nullable disable

namespace Microsoft.Build.Graph.UnitTests
{
    /// <summary>
    /// Tests verifying that the ProjectReferenceTargets protocol defined in
    /// Microsoft.Common.CurrentVersion.targets, Microsoft.Common.CrossTargeting.targets,
    /// and Microsoft.Managed.After.targets correctly propagates targets through the graph.
    ///
    /// These tests import the real targets files via $(MSBuildToolsPath) so the assertions
    /// run against the production protocol, not a copy-pasted snapshot. Changes to the
    /// real ProjectReferenceTargets sections in those files will exercise these tests directly.
    /// </summary>
    [TestClass]
    public class ProjectReferenceTargetsProtocolTests : IDisposable
    {
        private readonly TestEnvironment _env;

        /// <summary>
        /// Microsoft.Common.CrossTargeting.targets unconditionally imports $(NuGetRestoreTargets),
        /// which defaults to $(MSBuildToolsPath)\NuGet.targets — a file not present in the
        /// unit-test bin directory. The cross-targeting tests override the property to point at
        /// this empty stub so the import resolves to a no-op. (Microsoft.Common.CurrentVersion.targets
        /// guards its NuGet import with Exists(), so the non-cross-targeting tests don't need this.)
        /// </summary>
        private readonly TransientTestFile _emptyNuGetTargets;

        public ProjectReferenceTargetsProtocolTests(TestContext output)
        {
            _env = TestEnvironment.Create(output);
            _emptyNuGetTargets = _env.CreateFile("NuGet.targets", "<Project/>");
        }

        /// <summary>
        /// Synthetic project content for a non-managed, non-cross-targeting project that imports
        /// Microsoft.Common.targets (which in turn imports Microsoft.Common.CurrentVersion.targets).
        /// This gives the test the real core Build/Clean/Rebuild ProjectReferenceTargets protocol.
        /// </summary>
        private const string CommonImports = """
            <PropertyGroup>
              <Configuration>Debug</Configuration>
              <Platform>AnyCPU</Platform>
              <OutputPath>bin\Debug\</OutputPath>
            </PropertyGroup>
            <Import Project="$(MSBuildToolsPath)\Microsoft.Common.targets" />
            """;

        /// <summary>
        /// Synthetic project content for a managed project. Imports Microsoft.Common.targets plus
        /// Microsoft.Managed.After.targets (which adds the managed-specific Publish / DeployOnBuild
        /// extensions). Mirrors the shape of a real C#/VB project's import chain for the purpose
        /// of these protocol tests, without pulling in the language-specific compile targets.
        /// </summary>
        private const string ManagedImports = """
            <PropertyGroup>
              <Configuration>Debug</Configuration>
              <Platform>AnyCPU</Platform>
              <OutputPath>bin\Debug\</OutputPath>
            </PropertyGroup>
            <Import Project="$(MSBuildToolsPath)\Microsoft.Common.targets" />
            <Import Project="$(MSBuildToolsPath)\Microsoft.Managed.After.targets" />
            """;

        /// <summary>
        /// Synthetic content for a multitargeting (cross-targeting) managed project. The same XML
        /// is evaluated multiple times by ProjectGraph: once for the outer build (no TargetFramework
        /// global property, IsCrossTargetingBuild=true) and once per inner build (TargetFramework
        /// set, IsCrossTargetingBuild=false). The conditional imports route each evaluation to
        /// the right targets file, matching the production SDK pattern.
        /// </summary>
        private const string MultitargetingManagedImports = """
            <PropertyGroup>
              <TargetFrameworks>net8.0;net9.0</TargetFrameworks>
              <InnerBuildProperty>TargetFramework</InnerBuildProperty>
              <InnerBuildPropertyValues>TargetFrameworks</InnerBuildPropertyValues>
              <Configuration>Debug</Configuration>
              <Platform>AnyCPU</Platform>
              <OutputPath>bin\Debug\</OutputPath>
              <IsCrossTargetingBuild Condition="'$(TargetFrameworks)' != '' and '$(TargetFramework)' == ''">true</IsCrossTargetingBuild>
            </PropertyGroup>
            <Import Project="$(MSBuildToolsPath)\Microsoft.Common.CrossTargeting.targets" Condition="'$(IsCrossTargetingBuild)' == 'true'" />
            <Import Project="$(MSBuildToolsPath)\Microsoft.Common.targets" Condition="'$(IsCrossTargetingBuild)' != 'true'" />
            <Import Project="$(MSBuildToolsPath)\Microsoft.Managed.After.targets" />
            """;

        /// <summary>
        /// A non-managed project type (importing Common targets only, not Managed.After.targets) should
        /// get the core Build/Clean/Rebuild ProjectReferenceTargets in a graph build.
        /// </summary>
        [MSBuildTestMethod]
        public void NonManagedProject_GetsCoreBuildCleanRebuild_InGraphBuild()
        {
            // Project 1 → Project 2, both only importing the Common targets
            ProjectGraph graph = Helpers.CreateProjectGraph(
                env: _env,
                dependencyEdges: new Dictionary<int, int[]>
                {
                    { 1, new[] { 2 } },
                },
                extraContentForAllNodes: CommonImports);

            graph.ProjectNodes.Count.ShouldBe(2);

            // Build propagation: entry target Build → reference gets the full Build protocol
            IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> buildTargets =
                graph.GetTargetLists(new[] { "Build" });

            buildTargets[GetFirstNodeWithProjectNumber(graph, 1)].ShouldBe(new[] { "Build" });

            ImmutableList<string> refBuildTargets = buildTargets[GetFirstNodeWithProjectNumber(graph, 2)];
            // Should include the core Build targets from Common.CurrentVersion.targets
            refBuildTargets.ShouldContain("GetTargetFrameworks");
            refBuildTargets.ShouldContain("GetNativeManifest");
            refBuildTargets.ShouldContain("GetCopyToOutputDirectoryItems");
            refBuildTargets.ShouldContain("GetTargetFrameworksWithPlatformForSingleTargetFramework");

            // Clean propagation
            IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> cleanTargets =
                graph.GetTargetLists(new[] { "Clean" });

            ImmutableList<string> refCleanTargets = cleanTargets[GetFirstNodeWithProjectNumber(graph, 2)];
            refCleanTargets.ShouldContain("Clean");
            refCleanTargets.ShouldContain("GetTargetFrameworks");
            refCleanTargets.ShouldContain("GetTargetFrameworksWithPlatformForSingleTargetFramework");

            // Rebuild propagation
            IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> rebuildTargets =
                graph.GetTargetLists(new[] { "Rebuild" });

            ImmutableList<string> refRebuildTargets = rebuildTargets[GetFirstNodeWithProjectNumber(graph, 2)];
            refRebuildTargets.ShouldContain("Clean");
            refRebuildTargets.ShouldContain("GetNativeManifest");
            refRebuildTargets.ShouldContain("GetCopyToOutputDirectoryItems");
            refRebuildTargets.ShouldContain("GetTargetFrameworksWithPlatformForSingleTargetFramework");
        }

        /// <summary>
        /// A managed project (importing both Common and Managed.After targets) should get
        /// the same core Build/Clean/Rebuild targets as before the move, plus Publish support.
        /// </summary>
        [MSBuildTestMethod]
        public void ManagedProject_GraphBuildTargets_MatchExpectedProtocol()
        {
            ProjectGraph graph = Helpers.CreateProjectGraph(
                env: _env,
                dependencyEdges: new Dictionary<int, int[]>
                {
                    { 1, new[] { 2 } },
                },
                extraContentForAllNodes: ManagedImports);

            graph.ProjectNodes.Count.ShouldBe(2);

            // Build targets should include the core protocol
            IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> buildTargets =
                graph.GetTargetLists(new[] { "Build" });

            ImmutableList<string> refBuildTargets = buildTargets[GetFirstNodeWithProjectNumber(graph, 2)];
            refBuildTargets.ShouldContain("GetTargetFrameworks");
            refBuildTargets.ShouldContain("GetNativeManifest");
            refBuildTargets.ShouldContain("GetCopyToOutputDirectoryItems");
            refBuildTargets.ShouldContain("GetTargetFrameworksWithPlatformForSingleTargetFramework");

            // Publish targets should include managed-specific targets
            IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> publishTargets =
                graph.GetTargetLists(new[] { "Publish" });

            ImmutableList<string> refPublishTargets = publishTargets[GetFirstNodeWithProjectNumber(graph, 2)];
            refPublishTargets.ShouldContain("GetTargetFrameworks");
            refPublishTargets.ShouldContain("GetNativeManifest");
            refPublishTargets.ShouldContain("GetCopyToPublishDirectoryItems");
        }

        /// <summary>
        /// When DeployOnBuild=true, the Build entry target should include the Publish protocol targets
        /// in addition to the standard Build targets. This verifies the property-to-item migration
        /// where DeployOnBuild handling was refactored from appending to the ProjectReferenceTargetsForBuild
        /// property to adding separate ProjectReferenceTargets items.
        /// </summary>
        [MSBuildTestMethod]
        public void DeployOnBuild_GraphBuild_IncludesPublishTargetsForBuildAndRebuild()
        {
            ProjectGraph graph = Helpers.CreateProjectGraph(
                env: _env,
                dependencyEdges: new Dictionary<int, int[]>
                {
                    { 1, new[] { 2 } },
                },
                globalProperties: new Dictionary<string, string> { { "DeployOnBuild", "true" } },
                extraContentForAllNodes: ManagedImports);

            graph.ProjectNodes.Count.ShouldBe(2);

            // Build should include both core Build targets AND Publish targets
            IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> buildTargets =
                graph.GetTargetLists(new[] { "Build" });

            ImmutableList<string> refBuildTargets = buildTargets[GetFirstNodeWithProjectNumber(graph, 2)];
            // Core build protocol
            refBuildTargets.ShouldContain("GetNativeManifest");
            refBuildTargets.ShouldContain("GetCopyToOutputDirectoryItems");
            // Publish targets added via DeployOnBuild
            refBuildTargets.ShouldContain("GetCopyToPublishDirectoryItems");

            // Rebuild should also include Publish targets when DeployOnBuild=true
            IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> rebuildTargets =
                graph.GetTargetLists(new[] { "Rebuild" });

            ImmutableList<string> refRebuildTargets = rebuildTargets[GetFirstNodeWithProjectNumber(graph, 2)];
            refRebuildTargets.ShouldContain("Clean");
            refRebuildTargets.ShouldContain("GetNativeManifest");
            refRebuildTargets.ShouldContain("GetCopyToPublishDirectoryItems");
        }

        /// <summary>
        /// When BuildProjectReferences=false, the Build protocol should use GetTargetPath instead of
        /// .projectReferenceTargetsOrDefaultTargets, both for Build and Publish.
        /// </summary>
        [MSBuildTestMethod]
        public void BuildProjectReferencesFalse_UsesGetTargetPath()
        {
            ProjectGraph graph = Helpers.CreateProjectGraph(
                env: _env,
                dependencyEdges: new Dictionary<int, int[]>
                {
                    { 1, new[] { 2 } },
                },
                globalProperties: new Dictionary<string, string> { { "BuildProjectReferences", "false" } },
                extraContentForAllNodes: ManagedImports);

            IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> buildTargets =
                graph.GetTargetLists(new[] { "Build" });

            ImmutableList<string> refBuildTargets = buildTargets[GetFirstNodeWithProjectNumber(graph, 2)];
            // Should use GetTargetPath instead of default targets
            refBuildTargets.ShouldContain("GetTargetPath");
            refBuildTargets.ShouldContain("GetNativeManifest");
            refBuildTargets.ShouldContain("GetCopyToOutputDirectoryItems");
        }

        /// <summary>
        /// A multitargeting project using the cross-targeting protocol should dispatch
        /// via .default and properly handle outer/inner build target assignment.
        /// </summary>
        [MSBuildTestMethod]
        public void CrossTargetingProject_UsesDefaultTargetDispatch()
        {
            // Project 1 is multitargeting (outer build dispatches to inner builds via cross-targeting imports).
            // Project 2 is a single-targeting reference using the Common imports.
            ProjectGraph graph = Helpers.CreateProjectGraph(
                env: _env,
                dependencyEdges: new Dictionary<int, int[]>
                {
                    { 1, new[] { 2 } },
                },
                globalProperties: new Dictionary<string, string> { { "NuGetRestoreTargets", _emptyNuGetTargets.Path } },
                extraContentPerProjectNumber: new Dictionary<int, string>
                {
                    { 1, MultitargetingManagedImports },
                    { 2, CommonImports },
                });

            // Should have: 1 outer build + 2 inner builds (net8.0, net9.0) for project 1, + project 2
            // Inner builds of project 1 should reference project 2
            ProjectGraphNode outerBuild1 = GetOuterBuild(graph, 1);
            outerBuild1.ShouldNotBeNull();

            IReadOnlyCollection<ProjectGraphNode> innerBuilds1 = GetInnerBuilds(graph, 1);
            innerBuilds1.Count.ShouldBe(2);

            // Outer build dispatches to inner builds with .default
            IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> buildTargets =
                graph.GetTargetLists(new[] { "Build" });

            // The outer build should get Build
            buildTargets[outerBuild1].ShouldBe(new[] { "Build" });

            // Inner builds should get Build (propagated)
            foreach (ProjectGraphNode inner in innerBuilds1)
            {
                buildTargets[inner].ShouldBe(new[] { "Build" });
            }

            // Project 2 (referenced by inner builds) should get the full inner build protocol
            ImmutableList<string> refTargets = buildTargets[GetFirstNodeWithProjectNumber(graph, 2)];
            refTargets.ShouldContain("GetTargetFrameworks");
            refTargets.ShouldContain("GetNativeManifest");
            refTargets.ShouldContain("GetCopyToOutputDirectoryItems");
        }

        /// <summary>
        /// Multitargeting project with BuildProjectReferences=false should use GetTargetPath
        /// for inner builds while outer builds still dispatch via .default.
        /// </summary>
        [MSBuildTestMethod]
        public void CrossTargetingProject_BuildProjectReferencesFalse_UsesGetTargetPath()
        {
            ProjectGraph graph = Helpers.CreateProjectGraph(
                env: _env,
                dependencyEdges: new Dictionary<int, int[]>
                {
                    { 1, new[] { 2 } },
                },
                globalProperties: new Dictionary<string, string>
                {
                    { "BuildProjectReferences", "false" },
                    { "NuGetRestoreTargets", _emptyNuGetTargets.Path },
                },
                extraContentPerProjectNumber: new Dictionary<int, string>
                {
                    { 1, MultitargetingManagedImports },
                    { 2, CommonImports },
                });

            IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> buildTargets =
                graph.GetTargetLists(new[] { "Build" });

            // Project 2 should get GetTargetPath (not default targets) since BuildProjectReferences=false
            ImmutableList<string> refTargets = buildTargets[GetFirstNodeWithProjectNumber(graph, 2)];
            refTargets.ShouldContain("GetTargetPath");
            refTargets.ShouldContain("GetNativeManifest");
        }

        /// <summary>
        /// Clean on a cross-targeting project should propagate Clean to inner builds
        /// and to referenced projects.
        /// </summary>
        [MSBuildTestMethod]
        public void CrossTargetingProject_CleanPropagatesCorrectly()
        {
            ProjectGraph graph = Helpers.CreateProjectGraph(
                env: _env,
                dependencyEdges: new Dictionary<int, int[]>
                {
                    { 1, new[] { 2 } },
                },
                globalProperties: new Dictionary<string, string> { { "NuGetRestoreTargets", _emptyNuGetTargets.Path } },
                extraContentPerProjectNumber: new Dictionary<int, string>
                {
                    { 1, MultitargetingManagedImports },
                    { 2, CommonImports },
                });

            IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> cleanTargets =
                graph.GetTargetLists(new[] { "Clean" });

            // Outer build should get Clean
            cleanTargets[GetOuterBuild(graph, 1)].ShouldBe(new[] { "Clean" });

            // Inner builds should get Clean
            foreach (ProjectGraphNode inner in GetInnerBuilds(graph, 1))
            {
                cleanTargets[inner].ShouldBe(new[] { "Clean" });
            }

            // Referenced project should get Clean
            ImmutableList<string> refCleanTargets = cleanTargets[GetFirstNodeWithProjectNumber(graph, 2)];
            refCleanTargets.ShouldContain("Clean");
            refCleanTargets.ShouldContain("GetTargetFrameworks");
            refCleanTargets.ShouldContain("GetTargetFrameworksWithPlatformForSingleTargetFramework");
        }

        /// <summary>
        /// When DeployOnBuild is NOT set, Build should NOT include Publish targets.
        /// This confirms the DeployOnBuild condition is properly gating the Publish items.
        /// </summary>
        [MSBuildTestMethod]
        public void NoDeployOnBuild_BuildDoesNotIncludePublishTargets()
        {
            ProjectGraph graph = Helpers.CreateProjectGraph(
                env: _env,
                dependencyEdges: new Dictionary<int, int[]>
                {
                    { 1, new[] { 2 } },
                },
                extraContentForAllNodes: ManagedImports);

            IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> buildTargets =
                graph.GetTargetLists(new[] { "Build" });

            ImmutableList<string> refBuildTargets = buildTargets[GetFirstNodeWithProjectNumber(graph, 2)];
            refBuildTargets.ShouldNotContain("GetCopyToPublishDirectoryItems");
        }

        /// <summary>
        /// Managed project with Publish entry target should call the correct Publish protocol
        /// on referenced projects regardless of DeployOnBuild setting.
        /// </summary>
        [MSBuildTestMethod]
        public void ManagedProject_PublishTarget_CallsPublishProtocol()
        {
            ProjectGraph graph = Helpers.CreateProjectGraph(
                env: _env,
                dependencyEdges: new Dictionary<int, int[]>
                {
                    { 1, new[] { 2 } },
                },
                extraContentForAllNodes: ManagedImports);

            IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> publishTargets =
                graph.GetTargetLists(new[] { "Publish" });

            ImmutableList<string> refPublishTargets = publishTargets[GetFirstNodeWithProjectNumber(graph, 2)];
            refPublishTargets.ShouldContain("GetTargetFrameworks");
            refPublishTargets.ShouldContain("GetNativeManifest");
            refPublishTargets.ShouldContain("GetCopyToPublishDirectoryItems");
        }

        /// <summary>
        /// The core protocol should work for a chain of 3 projects, verifying
        /// that targets propagate transitively through the graph.
        /// </summary>
        [MSBuildTestMethod]
        public void NonManagedProjectChain_PropagatesTargetsThroughGraph()
        {
            // 1 → 2 → 3
            ProjectGraph graph = Helpers.CreateProjectGraph(
                env: _env,
                dependencyEdges: new Dictionary<int, int[]>
                {
                    { 1, new[] { 2 } },
                    { 2, new[] { 3 } },
                },
                extraContentForAllNodes: CommonImports);

            graph.ProjectNodes.Count.ShouldBe(3);

            IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> buildTargets =
                graph.GetTargetLists(new[] { "Build" });

            // Project 2 gets targets from project 1's protocol
            ImmutableList<string> proj2Targets = buildTargets[GetFirstNodeWithProjectNumber(graph, 2)];
            proj2Targets.ShouldContain("GetNativeManifest");
            proj2Targets.ShouldContain("GetCopyToOutputDirectoryItems");

            // Project 3 gets targets from project 2's protocol
            ImmutableList<string> proj3Targets = buildTargets[GetFirstNodeWithProjectNumber(graph, 3)];
            proj3Targets.ShouldContain("GetNativeManifest");
            proj3Targets.ShouldContain("GetCopyToOutputDirectoryItems");
        }

        /// <summary>
        /// When NoBuild=true, the Publish protocol should use GetTargetPath
        /// instead of .projectReferenceTargetsOrDefaultTargets for the main reference target.
        /// </summary>
        [MSBuildTestMethod]
        public void NoBuild_PublishUsesGetTargetPath()
        {
            ProjectGraph graph = Helpers.CreateProjectGraph(
                env: _env,
                dependencyEdges: new Dictionary<int, int[]>
                {
                    { 1, new[] { 2 } },
                },
                globalProperties: new Dictionary<string, string> { { "NoBuild", "true" } },
                extraContentForAllNodes: ManagedImports);

            IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> publishTargets =
                graph.GetTargetLists(new[] { "Publish" });

            ImmutableList<string> refPublishTargets = publishTargets[GetFirstNodeWithProjectNumber(graph, 2)];
            // With NoBuild=true, Publish should use GetTargetPath instead of building
            refPublishTargets.ShouldContain("GetTargetPath");
            refPublishTargets.ShouldContain("GetTargetFrameworks");
            refPublishTargets.ShouldContain("GetCopyToPublishDirectoryItems");
        }

        public void Dispose()
        {
            _env.Dispose();
        }
    }
}
