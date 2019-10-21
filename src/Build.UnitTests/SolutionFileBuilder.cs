// --------------------------------------------------------------------
// 
// Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// --------------------------------------------------------------------

// --------------------------------------------------------------------
// 
// Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// --------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Construction;
using Microsoft.Build.Graph;
using Microsoft.Build.Graph.UnitTests;
using Microsoft.Build.UnitTests;
using Shouldly;

namespace Microsoft.Build.Engine.UnitTests
{
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
                    n => GraphTestingUtilities.GetProjectNumber((ProjectGraphNode) n).ToString(),
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

            sb.AppendLine(@"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
VisualStudioVersion = 15.0.27004.2009
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
