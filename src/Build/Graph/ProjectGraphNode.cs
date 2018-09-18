// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Build.Execution;

namespace Microsoft.Build.Graph
{
    /// <summary>
    /// Represents the node for a particular project in a project graph.
    /// </summary>
    public sealed class ProjectGraphNode
    {
        private readonly List<ProjectGraphNode> _projectReferences = new List<ProjectGraphNode>();

        // No public creation.
        internal ProjectGraphNode(ProjectInstance project)
        {
            Project = project;
        }

        /// <summary>
        /// Gets an unordered collection of graph nodes for projects which this project references.
        /// </summary>
        public IReadOnlyCollection<ProjectGraphNode> ProjectReferences => _projectReferences;

        /// <summary>
        /// Gets the evaluated project represented by this node in the graph.
        /// </summary>
        public ProjectInstance Project { get; }

        internal void AddProjectReference(ProjectGraphNode projectGraphNode) => _projectReferences.Add(projectGraphNode);
    }
}
