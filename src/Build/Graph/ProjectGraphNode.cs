// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Build.Execution;

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
        private const string IsCrossTargetingBuild = "IsCrossTargetingBuild";
        private const string DispatchToInnerBuilds = "DispatchToInnerBuilds";
        private const string InnerTargets = "InnerTargets";

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

        public BuildData ComputeBuildData(IReadOnlyCollection<string> targets)
        {
            if (targets.Count == 0)
            {
                return new BuildData(new string[0], new Dictionary<string, string>());
            }

            return IsCrossTargeting(ProjectInstance)
                ? new BuildData(new[] {DispatchToInnerBuilds}, new Dictionary<string, string> {{ InnerTargets, string.Join(";", targets)}})
                : new BuildData(targets, new Dictionary<string, string>());
        }

        /// <summary>
        /// Gets the global properties which should be used to evaluate and execute this node in the graph.
        /// </summary>
        public IReadOnlyDictionary<string, string> GlobalProperties { get; }

        internal void AddProjectReference(ProjectGraphNode projectGraphNode) => _projectReferences.Add(projectGraphNode);

        internal void AddReferencingProject(ProjectGraphNode projectGraphNode) => _referencingProjects.Add(projectGraphNode);

        private static bool IsCrossTargeting(ProjectInstance projectInstance)
        {
            return projectInstance.GetPropertyValue(IsCrossTargetingBuild).Equals("true", StringComparison.Ordinal);
        }

    }
}
