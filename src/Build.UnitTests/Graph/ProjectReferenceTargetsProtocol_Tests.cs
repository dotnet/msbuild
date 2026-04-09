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
using Xunit;
using Xunit.Abstractions;
using static Microsoft.Build.Graph.UnitTests.GraphTestingUtilities;

#nullable disable

namespace Microsoft.Build.Graph.UnitTests
{
    /// <summary>
    /// Tests verifying that the ProjectReferenceTargets protocol defined in
    /// Microsoft.Common.CurrentVersion.targets, Microsoft.Common.CrossTargeting.targets,
    /// and Microsoft.Managed.After.targets correctly propagates targets through the graph.
    ///
    /// These tests use synthetic project XML that mirrors the real targets files to ensure
    /// the protocol is correct regardless of which project types import the targets.
    /// </summary>
    public class ProjectReferenceTargetsProtocolTests : IDisposable
    {
        private readonly TestEnvironment _env;

        public ProjectReferenceTargetsProtocolTests(ITestOutputHelper output)
        {
            _env = TestEnvironment.Create(output);
        }

        /// <summary>
        /// The core ProjectReferenceTargets protocol as defined in Microsoft.Common.CurrentVersion.targets
        /// (the "ProjectReferenceTargets for Static Graph" section, around line 5183).
        /// Keep in sync with the real file — if the protocol changes there, update this constant.
        /// This applies to all project types that import the Common targets.
        /// </summary>
        private const string CommonCurrentVersionTargetsProtocol = """
            <PropertyGroup>
              <_RecursiveTargetForContentCopying>GetCopyToOutputDirectoryItems</_RecursiveTargetForContentCopying>
            </PropertyGroup>

            <PropertyGroup Condition="'$(IsGraphBuild)' == 'true' and '$(IsCrossTargetingBuild)' != 'true'">
              <_MainReferenceTargetForBuild Condition="'$(BuildProjectReferences)' == '' or '$(BuildProjectReferences)' == 'true'">.projectReferenceTargetsOrDefaultTargets</_MainReferenceTargetForBuild>
              <_MainReferenceTargetForBuild Condition="'$(_MainReferenceTargetForBuild)' == ''">GetTargetPath</_MainReferenceTargetForBuild>

              <ProjectReferenceTargetsForBuild>$(_MainReferenceTargetForBuild);GetNativeManifest;$(_RecursiveTargetForContentCopying);$(ProjectReferenceTargetsForBuild)</ProjectReferenceTargetsForBuild>
            </PropertyGroup>
            <PropertyGroup Condition="'$(IsGraphBuild)' == 'true'">
              <ProjectReferenceTargetsForClean>Clean;$(ProjectReferenceTargetsForClean)</ProjectReferenceTargetsForClean>
              <ProjectReferenceTargetsForRebuild>$(ProjectReferenceTargetsForClean);$(ProjectReferenceTargetsForBuild);$(ProjectReferenceTargetsForRebuild)</ProjectReferenceTargetsForRebuild>
            </PropertyGroup>

            <ItemGroup Condition="'$(IsGraphBuild)' == 'true'">
              <ProjectReferenceTargets Include="Build" Targets="$(ProjectReferenceTargetsForBuildInOuterBuild)" Condition=" '$(ProjectReferenceTargetsForBuildInOuterBuild)' != '' " OuterBuild="true" />
              <ProjectReferenceTargets Include="Build" Targets="GetTargetFrameworks" OuterBuild="true" SkipNonexistentTargets="true" Condition="'$(IsCrossTargetingBuild)' != 'true'" />
              <ProjectReferenceTargets Include="Build" Targets="$(ProjectReferenceTargetsForBuild)" Condition=" '$(ProjectReferenceTargetsForBuild)' != '' " />

              <ProjectReferenceTargets Include="Clean" Targets="$(ProjectReferenceTargetsForCleanInOuterBuild)" Condition=" '$(ProjectReferenceTargetsForCleanInOuterBuild)' != '' " OuterBuild="true" />
              <ProjectReferenceTargets Include="Clean" Targets="GetTargetFrameworks" OuterBuild="true" SkipNonexistentTargets="true" Condition="'$(IsCrossTargetingBuild)' != 'true'" />
              <ProjectReferenceTargets Include="Clean" Targets="$(ProjectReferenceTargetsForClean)" Condition=" '$(ProjectReferenceTargetsForClean)' != '' " />

              <ProjectReferenceTargets Include="Build" Targets="GetTargetFrameworksWithPlatformForSingleTargetFramework" SkipNonexistentTargets="true" Condition="'$(IsCrossTargetingBuild)' != 'true'" />
              <ProjectReferenceTargets Include="Clean" Targets="GetTargetFrameworksWithPlatformForSingleTargetFramework" SkipNonexistentTargets="true" Condition="'$(IsCrossTargetingBuild)' != 'true'" />
              <ProjectReferenceTargets Include="Rebuild" Targets="GetTargetFrameworksWithPlatformForSingleTargetFramework" SkipNonexistentTargets="true" Condition="'$(IsCrossTargetingBuild)' != 'true'" />

              <ProjectReferenceTargets Include="Rebuild" Targets="$(ProjectReferenceTargetsForRebuild)" Condition=" '$(ProjectReferenceTargetsForRebuild)' != '' " />
            </ItemGroup>
            """;

        /// <summary>
        /// The cross-targeting protocol as defined in Microsoft.Common.CrossTargeting.targets
        /// (the "ProjectReferenceTargets for Static Graph (Cross-Targeting)" section).
        /// Keep in sync with the real file.
        /// </summary>
        private const string CrossTargetingProtocol = """
            <PropertyGroup Condition="'$(IsGraphBuild)' == 'true'">
              <ProjectReferenceTargetsForBuild>.default;$(ProjectReferenceTargetsForBuild)</ProjectReferenceTargetsForBuild>
              <ProjectReferenceTargetsForClean>Clean;$(ProjectReferenceTargetsForClean)</ProjectReferenceTargetsForClean>
              <ProjectReferenceTargetsForRebuild>$(ProjectReferenceTargetsForClean);$(ProjectReferenceTargetsForBuild);$(ProjectReferenceTargetsForRebuild)</ProjectReferenceTargetsForRebuild>
            </PropertyGroup>
            <ItemGroup Condition="'$(IsGraphBuild)' == 'true'">
              <ProjectReferenceTargets Include="Build" Targets="$(ProjectReferenceTargetsForBuild)" Condition=" '$(ProjectReferenceTargetsForBuild)' != '' " />
              <ProjectReferenceTargets Include="Clean" Targets="$(ProjectReferenceTargetsForClean)" Condition=" '$(ProjectReferenceTargetsForClean)' != '' " />
              <ProjectReferenceTargets Include="Rebuild" Targets="$(ProjectReferenceTargetsForRebuild)" Condition=" '$(ProjectReferenceTargetsForRebuild)' != '' " />
            </ItemGroup>
            """;

        /// <summary>
        /// The managed-specific extensions from Microsoft.Managed.After.targets for Publish and DeployOnBuild.
        /// Keep in sync with the real file.
        /// </summary>
        private const string ManagedAfterTargetsProtocol = """
            <PropertyGroup Condition="'$(IsGraphBuild)' == 'true' and '$(IsCrossTargetingBuild)' != 'true'">
              <_MainReferenceTargetForPublish Condition="'$(NoBuild)' == 'true'">GetTargetPath</_MainReferenceTargetForPublish>
              <_MainReferenceTargetForPublish Condition="'$(NoBuild)' != 'true'">$(_MainReferenceTargetForBuild)</_MainReferenceTargetForPublish>
              <ProjectReferenceTargetsForPublish>GetTargetFrameworks;$(_MainReferenceTargetForPublish);GetNativeManifest;GetCopyToPublishDirectoryItems;$(ProjectReferenceTargetsForPublish)</ProjectReferenceTargetsForPublish>

              <ProjectReferenceTargetsForGetCopyToPublishDirectoryItems>GetCopyToPublishDirectoryItems;$(ProjectReferenceTargetsForGetCopyToPublishDirectoryItems)</ProjectReferenceTargetsForGetCopyToPublishDirectoryItems>
            </PropertyGroup>

            <ItemGroup Condition="'$(IsGraphBuild)' == 'true'">
              <ProjectReferenceTargets Include="Build" Targets="$(ProjectReferenceTargetsForPublish)" Condition="'$(DeployOnBuild)' == 'true' and '$(IsCrossTargetingBuild)' != 'true' and '$(ProjectReferenceTargetsForPublish)' != ''" />
              <ProjectReferenceTargets Include="Rebuild" Targets="$(ProjectReferenceTargetsForPublish)" Condition="'$(DeployOnBuild)' == 'true' and '$(IsCrossTargetingBuild)' != 'true' and '$(ProjectReferenceTargetsForPublish)' != ''" />

              <ProjectReferenceTargets Include="Publish" Targets="$(ProjectReferenceTargetsForPublish)" Condition=" '$(ProjectReferenceTargetsForPublish)' != '' " />

              <ProjectReferenceTargets Include="GetCopyToPublishDirectoryItems" Targets="$(ProjectReferenceTargetsForGetCopyToPublishDirectoryItems)" Condition=" '$(ProjectReferenceTargetsForGetCopyToPublishDirectoryItems)' != '' " />
            </ItemGroup>
            """;

        /// <summary>
        /// Multitargeting specification using TargetFramework / TargetFrameworks, as in real managed projects.
        /// </summary>
        private const string MultitargetingSpec = """
            <PropertyGroup>
              <TargetFrameworks>net8.0;net9.0</TargetFrameworks>
            </PropertyGroup>
            <PropertyGroup Condition="'$(TargetFrameworks)' != '' and '$(TargetFramework)' == ''">
              <IsCrossTargetingBuild>true</IsCrossTargetingBuild>
            </PropertyGroup>
            <PropertyGroup>
              <InnerBuildProperty>TargetFramework</InnerBuildProperty>
              <InnerBuildPropertyValues>TargetFrameworks</InnerBuildPropertyValues>
            </PropertyGroup>
            """;

        private string AllDummyTargets => """
            <Target Name="Build" />
            <Target Name="Clean" />
            <Target Name="Rebuild" />
            <Target Name="Publish" />
            <Target Name="GetTargetPath" />
            <Target Name="GetNativeManifest" />
            <Target Name="GetCopyToOutputDirectoryItems" />
            <Target Name="GetCopyToPublishDirectoryItems" />
            <Target Name="GetTargetFrameworks" />
            <Target Name="GetTargetFrameworksWithPlatformForSingleTargetFramework" />
            """;

        /// <summary>
        /// A non-managed project type (importing Common targets only, not Managed.After.targets) should
        /// get the core Build/Clean/Rebuild ProjectReferenceTargets in a graph build.
        /// </summary>
        [Fact]
        public void NonManagedProject_GetsCoreBuildCleanRebuild_InGraphBuild()
        {
            // Project 1 → Project 2, both only importing the Common protocol
            string commonProtocol = CommonCurrentVersionTargetsProtocol + AllDummyTargets;

            ProjectGraph graph = Helpers.CreateProjectGraph(
                env: _env,
                dependencyEdges: new Dictionary<int, int[]>
                {
                    { 1, new[] { 2 } },
                },
                extraContentForAllNodes: commonProtocol);

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
        [Fact]
        public void ManagedProject_GraphBuildTargets_MatchExpectedProtocol()
        {
            // Managed project: Common protocol + Managed extensions
            string managedProtocol = CommonCurrentVersionTargetsProtocol + ManagedAfterTargetsProtocol + AllDummyTargets;

            ProjectGraph graph = Helpers.CreateProjectGraph(
                env: _env,
                dependencyEdges: new Dictionary<int, int[]>
                {
                    { 1, new[] { 2 } },
                },
                extraContentForAllNodes: managedProtocol);

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
        [Fact]
        public void DeployOnBuild_GraphBuild_IncludesPublishTargetsForBuildAndRebuild()
        {
            string managedProtocol = CommonCurrentVersionTargetsProtocol + ManagedAfterTargetsProtocol + AllDummyTargets;

            ProjectGraph graph = Helpers.CreateProjectGraph(
                env: _env,
                dependencyEdges: new Dictionary<int, int[]>
                {
                    { 1, new[] { 2 } },
                },
                globalProperties: new Dictionary<string, string> { { "DeployOnBuild", "true" } },
                extraContentForAllNodes: managedProtocol);

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
        [Fact]
        public void BuildProjectReferencesFalse_UsesGetTargetPath()
        {
            string managedProtocol = CommonCurrentVersionTargetsProtocol + ManagedAfterTargetsProtocol + AllDummyTargets;

            ProjectGraph graph = Helpers.CreateProjectGraph(
                env: _env,
                dependencyEdges: new Dictionary<int, int[]>
                {
                    { 1, new[] { 2 } },
                },
                globalProperties: new Dictionary<string, string> { { "BuildProjectReferences", "false" } },
                extraContentForAllNodes: managedProtocol);

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
        [Fact]
        public void CrossTargetingProject_UsesDefaultTargetDispatch()
        {
            // Project 1 is multitargeting (outer build dispatches to inner builds)
            // Project 2 is a single-targeting reference
            string innerBuildContent = CommonCurrentVersionTargetsProtocol + AllDummyTargets;

            // The outer build uses CrossTargeting protocol + CommonCurrentVersion protocol.
            // Both are needed because the same project file serves as outer and inner builds;
            // IsCrossTargetingBuild conditions determine which protocol items activate.
            string outerBuildContent = MultitargetingSpec + CrossTargetingProtocol + CommonCurrentVersionTargetsProtocol + AllDummyTargets;

            ProjectGraph graph = Helpers.CreateProjectGraph(
                env: _env,
                dependencyEdges: new Dictionary<int, int[]>
                {
                    { 1, new[] { 2 } },
                },
                extraContentPerProjectNumber: new Dictionary<int, string>
                {
                    { 1, outerBuildContent },
                    { 2, innerBuildContent },
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
        [Fact]
        public void CrossTargetingProject_BuildProjectReferencesFalse_UsesGetTargetPath()
        {
            string innerBuildContent = CommonCurrentVersionTargetsProtocol + AllDummyTargets;
            string outerBuildContent = MultitargetingSpec + CrossTargetingProtocol + CommonCurrentVersionTargetsProtocol + AllDummyTargets;

            ProjectGraph graph = Helpers.CreateProjectGraph(
                env: _env,
                dependencyEdges: new Dictionary<int, int[]>
                {
                    { 1, new[] { 2 } },
                },
                globalProperties: new Dictionary<string, string> { { "BuildProjectReferences", "false" } },
                extraContentPerProjectNumber: new Dictionary<int, string>
                {
                    { 1, outerBuildContent },
                    { 2, innerBuildContent },
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
        [Fact]
        public void CrossTargetingProject_CleanPropagatesCorrectly()
        {
            string innerBuildContent = CommonCurrentVersionTargetsProtocol + AllDummyTargets;
            string outerBuildContent = MultitargetingSpec + CrossTargetingProtocol + CommonCurrentVersionTargetsProtocol + AllDummyTargets;

            ProjectGraph graph = Helpers.CreateProjectGraph(
                env: _env,
                dependencyEdges: new Dictionary<int, int[]>
                {
                    { 1, new[] { 2 } },
                },
                extraContentPerProjectNumber: new Dictionary<int, string>
                {
                    { 1, outerBuildContent },
                    { 2, innerBuildContent },
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
        [Fact]
        public void NoDeployOnBuild_BuildDoesNotIncludePublishTargets()
        {
            string managedProtocol = CommonCurrentVersionTargetsProtocol + ManagedAfterTargetsProtocol + AllDummyTargets;

            ProjectGraph graph = Helpers.CreateProjectGraph(
                env: _env,
                dependencyEdges: new Dictionary<int, int[]>
                {
                    { 1, new[] { 2 } },
                },
                extraContentForAllNodes: managedProtocol);

            IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> buildTargets =
                graph.GetTargetLists(new[] { "Build" });

            ImmutableList<string> refBuildTargets = buildTargets[GetFirstNodeWithProjectNumber(graph, 2)];
            refBuildTargets.ShouldNotContain("GetCopyToPublishDirectoryItems");
        }

        /// <summary>
        /// Managed project with Publish entry target should call the correct Publish protocol
        /// on referenced projects regardless of DeployOnBuild setting.
        /// </summary>
        [Fact]
        public void ManagedProject_PublishTarget_CallsPublishProtocol()
        {
            string managedProtocol = CommonCurrentVersionTargetsProtocol + ManagedAfterTargetsProtocol + AllDummyTargets;

            ProjectGraph graph = Helpers.CreateProjectGraph(
                env: _env,
                dependencyEdges: new Dictionary<int, int[]>
                {
                    { 1, new[] { 2 } },
                },
                extraContentForAllNodes: managedProtocol);

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
        [Fact]
        public void NonManagedProjectChain_PropagatesTargetsThroughGraph()
        {
            string commonProtocol = CommonCurrentVersionTargetsProtocol + AllDummyTargets;

            // 1 → 2 → 3
            ProjectGraph graph = Helpers.CreateProjectGraph(
                env: _env,
                dependencyEdges: new Dictionary<int, int[]>
                {
                    { 1, new[] { 2 } },
                    { 2, new[] { 3 } },
                },
                extraContentForAllNodes: commonProtocol);

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
        [Fact]
        public void NoBuild_PublishUsesGetTargetPath()
        {
            string managedProtocol = CommonCurrentVersionTargetsProtocol + ManagedAfterTargetsProtocol + AllDummyTargets;

            ProjectGraph graph = Helpers.CreateProjectGraph(
                env: _env,
                dependencyEdges: new Dictionary<int, int[]>
                {
                    { 1, new[] { 2 } },
                },
                globalProperties: new Dictionary<string, string> { { "NoBuild", "true" } },
                extraContentForAllNodes: managedProtocol);

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
