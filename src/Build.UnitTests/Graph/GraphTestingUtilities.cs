// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Shouldly;

namespace Microsoft.Build.Graph.UnitTests
{
    internal static class GraphTestingUtilities
    {
        public static readonly ImmutableDictionary<string, string> EmptyGlobalProperties = new Dictionary<string, string> {{PropertyNames.IsGraphBuild, "true"}}.ToImmutableDictionary();

        public static readonly string InnerBuildPropertyName = "InnerBuild";
        public static readonly string InnerBuildPropertiesName = "InnerBuildProperties";

        public static readonly string CrosstargetingSpecificationPropertyGroup = $@"<PropertyGroup>
                                                                        <InnerBuildProperty>{InnerBuildPropertyName}</InnerBuildProperty>
                                                                        <InnerBuildPropertyValues>{InnerBuildPropertiesName}</InnerBuildPropertyValues>
                                                                        <{InnerBuildPropertiesName}>a;b</{InnerBuildPropertiesName}>
                                                                     </PropertyGroup>";
        public static readonly string HardCodedInnerBuildWithCrosstargetingSpecification = $@"<PropertyGroup>
                                                                        <InnerBuildProperty>{InnerBuildPropertyName}</InnerBuildProperty>
                                                                        <InnerBuildPropertyValues>{InnerBuildPropertiesName}</InnerBuildPropertyValues>
                                                                        <{InnerBuildPropertyName}>a</{InnerBuildPropertyName}>
                                                                     </PropertyGroup>";

        public static readonly string EnableTransitiveProjectReferencesPropertyGroup = @"<PropertyGroup>
                                                                                            <AddTransitiveProjectReferencesInStaticGraph>true</AddTransitiveProjectReferencesInStaticGraph>
                                                                                         </PropertyGroup>";

        public static void AssertOuterBuildAsNonRoot(
            ProjectGraphNode outerBuild,
            ProjectGraph graph,
            Dictionary<string, string> additionalGlobalProperties = null,
            int expectedInnerBuildCount = 2)
        {
            additionalGlobalProperties ??= new Dictionary<string, string>();

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

                innerBuilds.Length.ShouldBe(expectedInnerBuildCount);

                foreach (var innerBuild in innerBuilds)
                {
                    AssertInnerBuildEvaluation(innerBuild, true, additionalGlobalProperties);

                    innerBuild.ReferencingProjects.ShouldContain(outerBuildReferencer);
                    innerBuild.ReferencingProjects.ShouldNotContain(outerBuild);

                    graph.TestOnly_Edges.HasEdge((outerBuild, innerBuild)).ShouldBeFalse();

                    var edgeToOuterBuild = graph.TestOnly_Edges[(outerBuildReferencer, outerBuild)];
                    var edgeToInnerBuild = graph.TestOnly_Edges[(outerBuildReferencer, innerBuild)];

                    edgeToOuterBuild.ShouldBe(edgeToInnerBuild);
                }
            }
        }

        public static void AssertNonMultitargetingNode(ProjectGraphNode node, Dictionary<string, string> additionalGlobalProperties = null)
        {
            additionalGlobalProperties = additionalGlobalProperties ?? new Dictionary<string, string>();

            IsNotMultitargeting(node).ShouldBeTrue();
            node.ProjectInstance.GlobalProperties.ShouldBeSameIgnoringOrder(EmptyGlobalProperties.AddRange(additionalGlobalProperties));
            node.ProjectInstance.GetProperty(InnerBuildPropertyName).ShouldBeNull();
        }

        public static void AssertOuterBuildEvaluation(ProjectGraphNode outerBuild, Dictionary<string, string> additionalGlobalProperties)
        {
            additionalGlobalProperties.ShouldNotBeNull();

            IsOuterBuild(outerBuild).ShouldBeTrue();
            IsInnerBuild(outerBuild).ShouldBeFalse();

            outerBuild.ProjectInstance.GetProperty(InnerBuildPropertyName).ShouldBeNull();
            outerBuild.ProjectInstance.GlobalProperties.ShouldBeSameIgnoringOrder(EmptyGlobalProperties.AddRange(additionalGlobalProperties));
        }

        public static void AssertInnerBuildEvaluation(
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
                innerBuild.ProjectInstance.GlobalProperties.ShouldBeSameIgnoringOrder(
                    EmptyGlobalProperties
                        .Add(InnerBuildPropertyName, innerBuildPropertyValue)
                        .AddRange(additionalGlobalProperties));
            }
        }

        internal static bool IsOuterBuild(ProjectGraphNode project)
        {
            return ProjectInterpretation.GetProjectType(project.ProjectInstance) == ProjectInterpretation.ProjectType.OuterBuild;
        }

        internal static bool IsInnerBuild(ProjectGraphNode project)
        {
            return ProjectInterpretation.GetProjectType(project.ProjectInstance) == ProjectInterpretation.ProjectType.InnerBuild;
        }

        internal static bool IsNotMultitargeting(ProjectGraphNode project)
        {
            return ProjectInterpretation.GetProjectType(project.ProjectInstance) == ProjectInterpretation.ProjectType.NonMultitargeting;
        }

        internal static ProjectGraphNode GetFirstNodeWithProjectNumber(ProjectGraph graph, int projectNum)
        {
            return GetNodesWithProjectNumber(graph, projectNum).First();
        }

        internal static IEnumerable<ProjectGraphNode> GetNodesWithProjectNumber(ProjectGraph graph, int projectNum)
        {
            return graph.ProjectNodes.Where(node => node.ProjectInstance.FullPath.EndsWith(projectNum + ".proj"));
        }

        internal static ProjectGraphNode GetOuterBuild(ProjectGraph graph, int projectNumber)
        {
            return GetNodesWithProjectNumber(graph, projectNumber).FirstOrDefault(IsOuterBuild);
        }

        internal static IReadOnlyCollection<ProjectGraphNode> GetInnerBuilds(ProjectGraph graph, int projectNumber)
        {
            var outerBuild = GetOuterBuild(graph, projectNumber);

            if (outerBuild == null)
            {
                return ImmutableArray<ProjectGraphNode>.Empty;
            }
            else
            {
                var innerBuilds = GetNodesWithProjectNumber(graph, projectNumber)
                    .Where(p => IsInnerBuild(p) && p.ProjectInstance.FullPath.Equals(outerBuild.ProjectInstance.FullPath))
                    .ToArray();

                innerBuilds.ShouldNotBeEmpty();

                return innerBuilds;
            }
        }

        internal static string GetProjectFileName(ProjectGraphNode node)
        {
            node.ShouldNotBeNull();
            return Path.GetFileNameWithoutExtension(node.ProjectInstance.FullPath);
        }

        internal static string GetProjectFileName(ConfigurationMetadata config)
        {
            config.ShouldNotBeNull();
            return Path.GetFileNameWithoutExtension(config.ProjectFullPath);
        }

        internal static int GetProjectNumber(ProjectGraphNode node)
        {
            node.ShouldNotBeNull();
            return int.Parse(GetProjectFileName(node));
        }

        internal static int GetProjectNumber(ConfigurationMetadata config)
        {
            config.ShouldNotBeNull();
            return int.Parse(GetProjectFileName(config));
        }

        internal static string GetProjectPath(ProjectGraphNode node)
        {
            node.ShouldNotBeNull();
            return node.ProjectInstance.FullPath;
        }

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

        internal static IEnumerable<ProjectGraphNode> ComputeClosure(ProjectGraphNode node)
        {
            foreach (var reference in node.ProjectReferences)
            {
                yield return reference;

                foreach (var closureReference in ComputeClosure(reference))
                {
                    yield return closureReference;
                }
            }
        }

        internal static void AssertReferencesIgnoringOrder(this ProjectGraph graph, Dictionary<int, int[]> expectedReferencesForNode)
        {
            foreach (var kvp in expectedReferencesForNode)
            {
                var node = GetFirstNodeWithProjectNumber(graph, kvp.Key);
                node.AssertReferencesIgnoringOrder(kvp.Value);
            }
        }

        internal static void AssertReferencesIgnoringOrder(this ProjectGraphNode node, int[] expectedReferences)
        {
            node.ProjectReferences.Select(GetProjectNumber).ShouldBeSameIgnoringOrder(expectedReferences);
        }
    }
}
