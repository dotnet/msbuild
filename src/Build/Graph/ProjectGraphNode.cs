// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Build.Execution;

namespace Microsoft.Build.Experimental.Graph
{
    /// <summary>
    /// Represents the node for a particular project in a project graph.
    /// </summary>
    public sealed class ProjectGraphNode
    {
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
        /// Gets the global properties which should be used to evaluate and execute this node in the graph.
        /// </summary>
        public IReadOnlyDictionary<string, string> GlobalProperties { get; }

        internal void AddProjectReference(ProjectGraphNode projectGraphNode) => _projectReferences.Add(projectGraphNode);

        internal void AddReferencingProject(ProjectGraphNode projectGraphNode) => _referencingProjects.Add(projectGraphNode);
    }
}
