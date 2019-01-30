// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Graph
{
    public readonly struct BuildData
    {
        public IReadOnlyCollection<string> Targets { get; }
        public IReadOnlyDictionary<string, string> GlobalProperties { get; }

        public BuildData(IReadOnlyCollection<string> targets, IReadOnlyDictionary<string, string> globalProperties)
        {
            Targets = targets;
            GlobalProperties = globalProperties;
        }
    }

    /// <summary>
    /// Represents the node for a particular project in a project graph.
    /// </summary>
    public sealed class ProjectGraphNode
    {
        private const string BuildTargetsForGraphBuild = "BuildTargetsForGraphBuild";
        private const string GraphBuildEntryTargets = "GraphBuildEntryTargets";

        private readonly List<ProjectGraphNode> _projectReferences = new List<ProjectGraphNode>();
        private readonly List<ProjectGraphNode> _referencingProjects = new List<ProjectGraphNode>();

        // No public creation.
        internal ProjectGraphNode(
            ProjectInstance projectInstance,
            IReadOnlyDictionary<string, string> globalProperties)
        {
            ProjectInstance = projectInstance;
            GlobalProperties = globalProperties;
        }

        /// <summary>
        /// Gets an unordered collection of graph nodes for projects which this project references.
        /// </summary>
        public IReadOnlyCollection<ProjectGraphNode> ProjectReferences => _projectReferences;

        /// <summary>
        /// Gets a list of graph nodes for projects that have a project reference for this project
        /// </summary>
        public IReadOnlyCollection<ProjectGraphNode> ReferencingProjects => _referencingProjects;

        /// <summary>
        /// Gets the evaluated project instance represented by this node in the graph.
        /// </summary>
        public ProjectInstance ProjectInstance { get; }

        /// <summary>
        /// Given some entry targets for the node, this method computes the required build request targets and global properties.
        /// It includes the global properties the node was evaluated with.
        /// </summary>
        /// <param name="targets"></param>
        /// <returns></returns>
        public BuildData ComputeBuildData(IReadOnlyCollection<string> targets)
        {
            ErrorUtilities.VerifyThrowArgumentLength(targets, nameof(targets));

            return ImplementsGraphBuildEntryPoint(ProjectInstance)
                ? new BuildData(new[] {BuildTargetsForGraphBuild}, AddEntryTargetsToGlobalProperties())
                : new BuildData(targets, GlobalProperties);

            IReadOnlyDictionary<string, string> AddEntryTargetsToGlobalProperties()
            {
                var dictionary = GlobalProperties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                ErrorUtilities.VerifyThrow(!dictionary.ContainsKey(GraphBuildEntryTargets), "that property should be reserved, and rejected at graph construction time");

                dictionary[GraphBuildEntryTargets] = string.Join(";", targets);

                return dictionary;
            }
        }

        /// <summary>
        /// Gets the global properties which should be used to evaluate and execute this node in the graph.
        /// </summary>
        public IReadOnlyDictionary<string, string> GlobalProperties { get; }

        internal void AddProjectReference(ProjectGraphNode projectGraphNode) => _projectReferences.Add(projectGraphNode);

        internal void AddReferencingProject(ProjectGraphNode projectGraphNode) => _referencingProjects.Add(projectGraphNode);

        private static bool ImplementsGraphBuildEntryPoint(ProjectInstance projectInstance)
        {
            return projectInstance.Targets.ContainsKey(BuildTargetsForGraphBuild);
        }

    }
}
