// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Construction;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Microsoft.Build.Utilities;
using Shouldly;

namespace Microsoft.Build.Experimental.Graph.UnitTests
{
    internal static class GraphTestingUtilities
    {
        public static readonly ImmutableDictionary<string, string> EmptyGlobalProperties = new Dictionary<string, string> {{PropertyNames.IsGraphBuild, "true"}}.ToImmutableDictionary();

        public static readonly string InnerBuildPropertyName = "InnerBuild";

        public static readonly string MultitargetingSpecification = $@"<PropertyGroup>
                                                                        <InnerBuildProperty>{InnerBuildPropertyName}</InnerBuildProperty>
                                                                        <InnerBuildPropertyValues>InnerBuildProperties</InnerBuildPropertyValues>
                                                                        <InnerBuildProperties>a;b</InnerBuildProperties>
                                                                     </PropertyGroup>";

        public static void AssertOuterBuildAsNonRoot(
            ProjectGraphNode outerBuild,
            ProjectGraph graph,
            Dictionary<string, string> additionalGlobalProperties = null,
            int expectedInnerBuildCount = 2)
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

                innerBuilds.Length.ShouldBe(expectedInnerBuildCount);

                foreach (var innerBuild in innerBuilds)
                {
                    AssertInnerBuildEvaluation(innerBuild, true, additionalGlobalProperties);

                    innerBuild.ReferencingProjects.ShouldContain(outerBuildReferencer);
                    innerBuild.ReferencingProjects.ShouldNotContain(outerBuild);

                    graph.TestOnly_Edges.TestOnly_HasEdge((outerBuild, innerBuild)).ShouldBeFalse();

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
            node.ProjectInstance.GlobalProperties.ShouldBeEquivalentTo(EmptyGlobalProperties.AddRange(additionalGlobalProperties));
            node.ProjectInstance.GetProperty(InnerBuildPropertyName).ShouldBeNull();
        }

        public static void AssertOuterBuildEvaluation(ProjectGraphNode outerBuild, Dictionary<string, string> additionalGlobalProperties)
        {
            additionalGlobalProperties.ShouldNotBeNull();

            IsOuterBuild(outerBuild).ShouldBeTrue();
            IsInnerBuild(outerBuild).ShouldBeFalse();

            outerBuild.ProjectInstance.GetProperty(InnerBuildPropertyName).ShouldBeNull();
            outerBuild.ProjectInstance.GlobalProperties.ShouldBeEquivalentTo(EmptyGlobalProperties.AddRange(additionalGlobalProperties));
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
                innerBuild.ProjectInstance.GlobalProperties.ShouldBeEquivalentTo(
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
    }

    internal class SolutionFileBuilder
    {
        public static readonly IReadOnlyCollection<SolutionConfigurationInSolution> SolutionConfigurationPlatformsDefaults = new[]
        {
            new SolutionConfigurationInSolution("Debug", "AnyCPU"),
            new SolutionConfigurationInSolution("Release", "AnyCPU")
        };

        /// <summary>
        /// projectName -> projectPath
        /// </summary>
        public IReadOnlyDictionary<string, string> Projects { get; set; }

        public IReadOnlyCollection<SolutionConfigurationInSolution> SolutionConfigurationPlatforms { get; set; } = SolutionConfigurationPlatformsDefaults;

        public Dictionary<string, Dictionary<SolutionConfigurationInSolution, ProjectConfigurationInSolution>> ProjectConfigurations { get; set; }

        public (string, string)[] SolutionDependencies { get; set; }
        public (string, string[])[] SolutionDependenciesProjectNameToGuids { get; set; }

        private readonly struct ProjectInfo
        {
            private readonly List<ProjectInfo> _dependencies;
            private readonly List<string> _dependencyGuids;
            public string Name { get; }
            public string Path { get; }
            public string ProjectTypeGuid { get; }
            public string Guid { get; }

            public IReadOnlyCollection<ProjectInfo> Dependencies => _dependencies;

            public IReadOnlyCollection<string> DependencyGuids => _dependencyGuids;

            public ProjectInfo(string name, string path, string projectTypeGuid, string guid)
            {
                Name = name;
                Path = path;
                ProjectTypeGuid = projectTypeGuid;
                Guid = guid;
                _dependencies = new List<ProjectInfo>();
                _dependencyGuids = new List<string>();
            }

            public void AddDependency(ProjectInfo dependency)
            {
                _dependencies.Add(dependency);
            }

            internal void AddGuidDependencies(string[] dependencyGuids)
            {
                _dependencyGuids.AddRange(dependencyGuids);
            }
        }

        public static SolutionFileBuilder FromGraphEdges(
            TestEnvironment env,
            Dictionary<int, int[]> edges,
            (string, string[])[] solutionDependenciesProjectNameToGuids = null)
        {
            return FromGraph(Helpers.CreateProjectGraph(env, edges), solutionDependenciesProjectNameToGuids: solutionDependenciesProjectNameToGuids);
        }

        public static SolutionFileBuilder FromGraph(
            ProjectGraph graph,
            Dictionary<string, Dictionary<SolutionConfigurationInSolution, ProjectConfigurationInSolution>> projectConfigurations = null,
            (string, string)[] solutionDependencies = null,
            (string, string[])[] solutionDependenciesProjectNameToGuids = null)
        {
            return new SolutionFileBuilder
            {
                Projects = graph.ProjectNodes.ToDictionary(
                    n => GraphTestingUtilities.GetProjectNumber(n).ToString(),
                    n => n.ProjectInstance.FullPath),
                ProjectConfigurations = projectConfigurations,
                SolutionDependencies = solutionDependencies,
                SolutionDependenciesProjectNameToGuids = solutionDependenciesProjectNameToGuids
            };
        }

        public string BuildSolution()
        {
            var projectInfos = Projects.ToDictionary(
                kvp => kvp.Key,
                kvp => new ProjectInfo(
                    kvp.Key,
                    kvp.Value,
                    "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}",
                    Guid.NewGuid()
                        .ToString("B")));

            if (SolutionDependencies != null && SolutionDependencies.Length > 0)
            {
                foreach (var (parent, dependency) in SolutionDependencies)
                {
                    projectInfos.ShouldContainKey(parent);
                    projectInfos.ShouldContainKey(dependency);

                    projectInfos[parent].AddDependency(projectInfos[dependency]); 
                }
            }

            if (SolutionDependenciesProjectNameToGuids!= null && SolutionDependenciesProjectNameToGuids.Length > 0)
            {
                foreach (var (parent, dependencyGuids) in SolutionDependenciesProjectNameToGuids)
                {
                    projectInfos.ShouldContainKey(parent);
                    projectInfos[parent].AddGuidDependencies(dependencyGuids);
                }
            }

            ProjectConfigurations = ProjectConfigurations ?? projectInfos.ToDictionary(
                p => p.Key,
                p => SolutionConfigurationPlatforms.ToDictionary(
                    sc => sc,
                    sc => new ProjectConfigurationInSolution(
                        sc.ConfigurationName,
                        sc.PlatformName,
                        true)
                    )
                );

            var sb = new StringBuilder();

            sb.AppendLine($@"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version {VisualStudioVersion.VersionLatest}
VisualStudioVersion = {VisualStudioVersion.VersionLatest}.0.0.0
MinimumVisualStudioVersion = 10.0.40219.1");

            foreach (var project in projectInfos.Values)
            {
                sb.AppendLine($@"
Project(""{project.ProjectTypeGuid}"") = ""{project.Name}"", ""{project.Path}"", ""{project.Guid}""");

                if (project.Dependencies.Count > 0 || project.DependencyGuids.Count > 0)
                {
                    sb.AppendLine($"\tProjectSection(ProjectDependencies) = postProject");

                    foreach (var dependency in project.Dependencies)
                    {
                        sb.AppendLine($"\t\t{dependency.Guid} = {dependency.Guid}");
                    }

                    foreach (var dependencyGuid in project.DependencyGuids)
                    {
                        sb.AppendLine($"\t\t{dependencyGuid} = {dependencyGuid}");
                    }

                    sb.AppendLine($"\tEndProjectSection");
                }

                sb.AppendLine($@"
EndProject");
            }

            sb.AppendLine("Global");

            sb.AppendLine("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution");

            foreach (var solutionConfigurationPlatform in SolutionConfigurationPlatforms)
            {
                sb.AppendLine($"\t\t{solutionConfigurationPlatform.FullName} = {solutionConfigurationPlatform.FullName}");
            }

            sb.AppendLine("\tEndGlobalSection");

            sb.AppendLine("\tGlobalSection(ProjectConfigurationPlatforms) = postSolution");

            foreach (var project in projectInfos)
            {
                foreach (var configurationMapping in ProjectConfigurations[project.Key])
                {
                    var solutionConfiguration = configurationMapping.Key;
                    var projectConfiguration = configurationMapping.Value;
                    sb.AppendLine($"\t\t{project.Value.Guid}.{solutionConfiguration.FullName}.ActiveCfg = {projectConfiguration.FullName}");
                    sb.AppendLine($"\t\t{project.Value.Guid}.{solutionConfiguration.FullName}.Build.0 = {projectConfiguration.FullName}");
                }
            }

            sb.AppendLine("\tEndGlobalSection");

            sb.AppendLine("EndGlobal");

            return sb.ToString();
        }
    }
}
