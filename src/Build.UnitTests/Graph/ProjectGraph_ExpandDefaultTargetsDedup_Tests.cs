// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Graph.UnitTests
{
    /// <summary>
    /// Regression coverage for the dedup inside <see cref="ProjectGraph.ExpandDefaultTargets"/>.
    ///
    /// Background: <c>GetTargetLists</c> performs a BFS over the graph and calls
    /// <c>ExpandDefaultTargets</c> once per traversed edge. Each call's output becomes the
    /// next hop's input. If the propagated list contains duplicates from any source —
    /// marker expansion against duplicate-bearing
    /// <c>&lt;ProjectReferenceTargets Targets="…"/&gt;</c> metadata, multiple PRT items
    /// independently emitting marker-containing metadata for the same entry target, an
    /// explicit literal duplicate like <c>Targets="Build;Build;Build"</c>, or upstream
    /// propagation accumulating duplicates — the next hop's
    /// <see cref="ProjectInterpretation.TargetsToPropagate.FromProjectAndEntryTargets"/>
    /// iterates each duplicate entry target against every matching PRT, multiplying the
    /// propagated count by N per hop and producing N^depth growth.
    ///
    /// The fix dedupes inside <c>ExpandDefaultTargets</c> regardless of input shape, so
    /// the result handed back to the BFS is always duplicate-free. The outer post-BFS
    /// dedup in <c>GetTargetLists</c> still runs and collapses each per-node final list,
    /// so the publicly-observable result of <c>GetTargetLists</c> is unchanged — this is a
    /// purely structural / performance fix.
    /// </summary>
    public class ProjectGraph_ExpandDefaultTargetsDedup_Tests
    {
        private readonly ITestOutputHelper _output;

        public ProjectGraph_ExpandDefaultTargetsDedup_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// Marker expansion produces duplicates: a literal <c>Build</c> in the input plus
        /// the <see cref="MSBuildConstants.DefaultTargetsMarker"/> that expands to default
        /// targets <c>[Build, X]</c>. Result is deduped to <c>[Build, X]</c>.
        /// </summary>
        [Fact]
        public void Dedupes_WhenMarkerExpansionProducesDuplicates()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            ProjectItemInstance edge = CreateEdge();
            string[] input = ["Build", MSBuildConstants.DefaultTargetsMarker];
            List<string> defaultTargets = ["Build", "X"];

            string[] result = ProjectGraph.ExpandDefaultTargets(input, defaultTargets, edge);

            result.ShouldBe(["Build", "X"]);
        }

        /// <summary>
        /// The PRT-or-default marker path: two markers in the input each expand to the
        /// edge's Targets metadata (<c>A;B;A</c>), producing the geometric amplification
        /// shape this fix prevents. Verify the per-call result collapses to the unique set.
        /// </summary>
        [Fact]
        public void Dedupes_PRTOrDefaultMarker_WhenTargetsMetadataDuplicatesExpansion()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            ProjectItemInstance edge = CreateEdgeWithTargetsMetadata("A;B;A");
            string[] input = [
                MSBuildConstants.ProjectReferenceTargetsOrDefaultTargetsMarker,
                MSBuildConstants.ProjectReferenceTargetsOrDefaultTargetsMarker];
            List<string> defaultTargets = ["DefaultsShouldNotBeUsed"];

            string[] result = ProjectGraph.ExpandDefaultTargets(input, defaultTargets, edge);

            result.ShouldBe(["A", "B"]);
        }

        /// <summary>
        /// Explicit non-marker duplicates in the input must also be deduped. Without this,
        /// a <c>&lt;ProjectReferenceTargets Include="Build" Targets="Build;Build;Build"/&gt;</c>
        /// shape would explode geometrically across BFS hops even though no marker is
        /// involved — <see cref="ProjectInterpretation.TargetsToPropagate.FromProjectAndEntryTargets"/>
        /// iterates each entry target against every matching PRT, so an N-duplicate
        /// arriving at an edge becomes N^2 propagated at the next hop.
        /// </summary>
        [Fact]
        public void Dedupes_ExplicitNonMarkerDuplicates()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            ProjectItemInstance edge = CreateEdge();
            string[] input = ["Build", "Build", "Build"];
            List<string> defaultTargets = ["DefaultsShouldNotBeUsed"];

            string[] result = ProjectGraph.ExpandDefaultTargets(input, defaultTargets, edge);

            result.ShouldBe(["Build"]);
        }

        /// <summary>
        /// Dedup is case-insensitive ordinal (matching the equality semantics used by both
        /// the outer post-BFS dedup at <c>ProjectGraph.GetTargetLists</c> and
        /// <c>ProjectGraphBuildRequest.Equals</c>). First-occurrence wins.
        /// </summary>
        [Fact]
        public void Dedupes_IgnoresCase()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            ProjectItemInstance edge = CreateEdge();
            string[] input = ["Build", MSBuildConstants.DefaultTargetsMarker];
            List<string> defaultTargets = ["BUILD", "x"];

            string[] result = ProjectGraph.ExpandDefaultTargets(input, defaultTargets, edge);

            result.ShouldBe(["Build", "x"]);
        }

        /// <summary>
        /// Fast path: when no marker is present and no duplicate is detected, the original
        /// input reference is returned unchanged — zero allocations beyond the lookup HashSet.
        /// </summary>
        [Fact]
        public void NoMarkerNoDuplicates_ReturnsSameInstance()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            ProjectItemInstance edge = CreateEdge();
            string[] input = ["Build", "X", "Y"];
            List<string> defaultTargets = ["ShouldNotBeUsed"];

            string[] result = ProjectGraph.ExpandDefaultTargets(input, defaultTargets, edge);

            result.ShouldBeSameAs(input);
        }

        /// <summary>
        /// Marker is expanded but the resulting list has no duplicates. The buffer is
        /// allocated (because we crossed the marker), but no entries are dropped.
        /// </summary>
        [Fact]
        public void MarkerExpanded_NoDuplicates_ReturnsExpandedList()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            ProjectItemInstance edge = CreateEdge();
            string[] input = [MSBuildConstants.DefaultTargetsMarker];
            List<string> defaultTargets = ["A", "B"];

            string[] result = ProjectGraph.ExpandDefaultTargets(input, defaultTargets, edge);

            result.ShouldBe(["A", "B"]);
        }

        /// <summary>
        /// Edge case: a marker that expands to an empty <c>defaultTargets</c> list. The
        /// marker is consumed and the result is empty.
        /// </summary>
        [Fact]
        public void MarkerExpandsToEmptyDefaults_ReturnsEmptyList()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            ProjectItemInstance edge = CreateEdge();
            string[] input = [MSBuildConstants.DefaultTargetsMarker];
            List<string> defaultTargets = [];

            string[] result = ProjectGraph.ExpandDefaultTargets(input, defaultTargets, edge);

            result.ShouldBeEmpty();
        }

        /// <summary>
        /// Edge case: every entry collapses to the same target. Verifies the buffer copies
        /// the unique prefix correctly when the prefix is length 1.
        /// </summary>
        [Fact]
        public void AllEntriesCollapseToSingleton()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            ProjectItemInstance edge = CreateEdge();
            string[] input = ["Build", MSBuildConstants.DefaultTargetsMarker, MSBuildConstants.DefaultTargetsMarker];
            List<string> defaultTargets = ["Build"];

            string[] result = ProjectGraph.ExpandDefaultTargets(input, defaultTargets, edge);

            result.ShouldBe(["Build"]);
        }

        /// <summary>
        /// Verifies the order of first-occurrence wins across a mix of literal, marker
        /// expansion, and post-marker literal entries.
        /// </summary>
        [Fact]
        public void PreservesFirstOccurrenceOrder()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            ProjectItemInstance edge = CreateEdge();
            string[] input = ["First", MSBuildConstants.DefaultTargetsMarker, "Last"];
            List<string> defaultTargets = ["Middle", "First"];

            string[] result = ProjectGraph.ExpandDefaultTargets(input, defaultTargets, edge);

            // "First" (literal at index 0) precedes "Middle" (from marker expansion); the
            // second "First" inside defaultTargets is dropped; "Last" follows.
            result.ShouldBe(["First", "Middle", "Last"]);
        }

        /// <summary>
        /// End-to-end smoke: at depth 12 with the post-PR-#13427 vcxproj shape (two
        /// <c>PRT(Build)</c> items where one item embeds the marker twice inside its
        /// Targets metadata), the graph build completes correctly and the per-node final
        /// target list contains each target at most once. With the fix in place the BFS
        /// runs in sub-second time; without the fix it explodes geometrically.
        /// </summary>
        [Fact]
        public void GetTargetLists_DuplicateMarkerPRT_StaysBoundedAcrossChain()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            const int depth = 12;
            IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> targetLists =
                BuildChainAndGetTargetLists(env, depth, DuplicateMarkerPRTContent);

            foreach (KeyValuePair<ProjectGraphNode, ImmutableList<string>> pair in targetLists)
            {
                IGrouping<string, string>? firstDup = pair.Value
                    .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault(g => g.Count() > 1);

                firstDup.ShouldBeNull(
                    $"per-node final target list must be duplicate-free, but '{firstDup?.Key}' appeared {firstDup?.Count()} times on {pair.Key.ProjectInstance.FullPath}");
            }
        }

        /// <summary>
        /// Sanity: a single-PRT(Build)-with-single-marker chain at depth 6 produces one
        /// <c>Build</c> per node — verifies the common case is unaffected.
        /// </summary>
        [Fact]
        public void GetTargetLists_SingleMarkerPRT_PropagatesLinearly()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            const int depth = 6;
            IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> targetLists =
                BuildChainAndGetTargetLists(env, depth, SingleMarkerPRTContent);

            foreach (KeyValuePair<ProjectGraphNode, ImmutableList<string>> pair in targetLists)
            {
                pair.Value.Count(t => t.Equals("Build", StringComparison.OrdinalIgnoreCase))
                    .ShouldBe(1, $"single-marker chain should resolve to one Build per node on {pair.Key.ProjectInstance.FullPath}");
            }
        }

        private static ProjectItemInstance CreateEdge() => CreateEdgeWithTargetsMetadata(targetsMetadata: null);

        private static ProjectItemInstance CreateEdgeWithTargetsMetadata(string? targetsMetadata)
        {
            ProjectInstance projectInstance = new Project().CreateProjectInstance();
            ProjectItemInstance edge = new ProjectItemInstance(projectInstance, "ProjectReference", "ref.csproj", projectInstance.FullPath);
            if (!string.IsNullOrEmpty(targetsMetadata))
            {
                edge.SetMetadata("Targets", targetsMetadata);
            }

            return edge;
        }

        private static IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> BuildChainAndGetTargetLists(
            TestEnvironment env,
            int depth,
            string extraContent)
        {
            // Chain: 1 -> 2 -> 3 -> ... -> depth.
            var edges = new Dictionary<int, int[]>();
            for (int i = 1; i < depth; i++)
            {
                edges[i] = [i + 1];
            }
            edges[depth] = [];

            ProjectGraph graph = Helpers.CreateProjectGraph(
                env: env,
                dependencyEdges: edges,
                extraContentForAllNodes: extraContent);

            return graph.GetTargetLists(["Build"]);
        }

        // Mirrors the post-PR-#13427 vcxproj state:
        //   - PRT(Build, "marker;GetNativeManifest;_GCTODI")
        //   - PRT(Build, "BGS;marker;GetNativeManifest;BC;BL;marker;GetNativeManifest;_GCTODI")
        // The second item embeds the marker TWICE because its authoring source prepends to
        // $(ProjectReferenceTargetsForBuild) after Common already populated the property with
        // one marker. Net per Build entry: 3 markers => 3 Builds emitted per visit => 3^n
        // growth without the dedup.
        private const string DuplicateMarkerPRTContent = $"""
            <ItemGroup>
                <ProjectReferenceTargets Include="Build" Targets="{MSBuildConstants.ProjectReferenceTargetsOrDefaultTargetsMarker};GetNativeManifest;_GetCopyToOutputDirectoryItemsFromThisProject" />
                <ProjectReferenceTargets Include="Build" Targets="BuildGenerateSources;{MSBuildConstants.ProjectReferenceTargetsOrDefaultTargetsMarker};GetNativeManifest;BuildCompile;BuildLink;{MSBuildConstants.ProjectReferenceTargetsOrDefaultTargetsMarker};GetNativeManifest;_GetCopyToOutputDirectoryItemsFromThisProject" />
                <ProjectReferenceTargets Include="BuildGenerateSources" Targets="BuildGenerateSources" />
                <ProjectReferenceTargets Include="BuildCompile" Targets="BuildCompile" />
                <ProjectReferenceTargets Include="BuildLink" Targets="BuildLink" />
            </ItemGroup>
            <Target Name="BuildGenerateSources" />
            <Target Name="BuildCompile" />
            <Target Name="BuildLink" />
            <Target Name="GetNativeManifest" />
            <Target Name="_GetCopyToOutputDirectoryItemsFromThisProject" />
            """;

        // Pre-PR-#13427 single-PRT shape: one PRT(Build) item with one marker. Common case.
        private const string SingleMarkerPRTContent = $"""
            <ItemGroup>
                <ProjectReferenceTargets Include="Build" Targets="{MSBuildConstants.ProjectReferenceTargetsOrDefaultTargetsMarker}" />
            </ItemGroup>
            """;
    }
}
