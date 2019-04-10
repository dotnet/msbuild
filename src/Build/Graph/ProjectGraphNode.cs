// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.Graph
{
    /// <summary>
    /// Represents the node for a particular project in a project graph.
    /// </summary>
    public sealed class ProjectGraphNode
    {
        private readonly HashSet<ProjectGraphNode> _projectReferences = new HashSet<ProjectGraphNode>();
        private readonly HashSet<ProjectGraphNode> _referencingProjects = new HashSet<ProjectGraphNode>();

        // No public creation.
        internal ProjectGraphNode(ProjectInstance projectInstance)
        {
            ErrorUtilities.VerifyThrowInternalNull(projectInstance, nameof(projectInstance));
            ProjectInstance = projectInstance;
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

        internal void AddProjectReference(ProjectGraphNode reference)
        {
            _projectReferences.Add(reference);
            reference._referencingProjects.Add(this);
        }

        internal void RemoveReferences()
        {
            foreach (var reference in _projectReferences)
            {
                ErrorUtilities.VerifyThrow(reference._referencingProjects.Contains(this), "references should point the nodes referencing them");
                reference._referencingProjects.Remove(this);
            }

            _projectReferences.Clear();
        }
    }
}
