// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Construction;
using Microsoft.Build.UnitTests;
using Microsoft.Build.Utilities;
using Shouldly;

namespace Microsoft.Build.Experimental.Graph.UnitTests
{
    internal static class GraphTestingUtilities
    {
        internal static ProjectGraphNode GetFirstNodeWithProjectNumber(ProjectGraph graph, int projectNum)
        {
            return GetNodesWithProjectNumber(graph, projectNum).First();
        }

        internal static IEnumerable<ProjectGraphNode> GetNodesWithProjectNumber(ProjectGraph graph, int projectNum)
        {
            return graph.ProjectNodes.Where(node => node.ProjectInstance.FullPath.EndsWith(projectNum + ".proj"));
        }

        internal static string GetProjectFileName(ProjectGraphNode node)
        {
            node.ShouldNotBeNull();
            return Path.GetFileNameWithoutExtension(node.ProjectInstance.FullPath);
        }

        internal static int GetProjectNumber(ProjectGraphNode node)
        {
            node.ShouldNotBeNull();
            return int.Parse(GetProjectFileName(node));
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

            readonly struct ProjectInSolution
            {
                public string Name { get; }
                public string Path { get; }
                public string ProjectTypeGuid { get; }
                public string Guid { get; }

                public ProjectInSolution(string name, string path, string projectTypeGuid, string guid)
                {
                    Name = name;
                    Path = path;
                    ProjectTypeGuid = projectTypeGuid;
                    Guid = guid;
                }
            }

            public string BuildSolution()
            {
                var projectInfo = Projects.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new ProjectInSolution(
                        kvp.Key,
                        kvp.Value,
                        "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}",
                        Guid.NewGuid()
                            .ToString("B")));

                ProjectConfigurations = ProjectConfigurations ?? projectInfo.ToDictionary(
                        p => p.Key,
                        p => SolutionConfigurationPlatforms.ToDictionary(
                                sc => sc,
                                sc => new ProjectConfigurationInSolution(
                                    configurationName: sc.ConfigurationName,
                                    platformName: sc.PlatformName,
                                    includeInBuild: true)
                            )
                    );

                var sb = new StringBuilder();

                sb.AppendLine($@"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version {VisualStudioVersion.VersionLatest}
VisualStudioVersion = {VisualStudioVersion.VersionLatest}.0.0.0
MinimumVisualStudioVersion = 10.0.40219.1");

                foreach (var project in projectInfo.Values)
                {
                    sb.AppendLine($@"
Project(""{project.ProjectTypeGuid}"") = ""{project.Name}"", ""{project.Path}"", ""{project.Guid}""
EndProject
");
                }

                sb.AppendLine("Global");

                sb.AppendLine("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution");

                foreach (var solutionConfigurationPlatform in SolutionConfigurationPlatforms)
                {
                    sb.AppendLine($"\t\t{solutionConfigurationPlatform.FullName} = {solutionConfigurationPlatform.FullName}");
                }

                sb.AppendLine("\tEndGlobalSection");

                sb.AppendLine("\tGlobalSection(ProjectConfigurationPlatforms) = postSolution");

                foreach (var project in projectInfo)
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
