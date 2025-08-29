﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Graph
{
    /// <summary>
    /// Represents the node for a particular project in a project graph.
    /// A node is defined by (ProjectPath, ToolsVersion, GlobalProperties).
    /// </summary>
    [DebuggerDisplay(@"{DebugString()}")]
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

        private string DebugString()
        {
            var truncatedProjectFile = FileUtilities.TruncatePathToTrailingSegments(ProjectInstance.FullPath, 2);

            return
                $"{truncatedProjectFile}, #GlobalProps={ProjectInstance.GlobalProperties.Count}, #Props={ProjectInstance.Properties.Count}, #Items={ProjectInstance.Items.Count}, #in={ReferencingProjects.Count}, #out={ProjectReferences.Count}";
        }

        internal void AddProjectReference(ProjectGraphNode reference, ProjectItemInstance projectReferenceItem, GraphBuilder.GraphEdges edges)
        {
            _projectReferences.Add(reference);
            reference._referencingProjects.Add(this);

            edges.AddOrUpdateEdge((this, reference), projectReferenceItem);
        }

        internal void RemoveReference(ProjectGraphNode reference, GraphBuilder.GraphEdges edges)
        {
            _projectReferences.Remove(reference);
            reference._referencingProjects.Remove(reference);

            edges.RemoveEdge((this, reference));
        }

        internal void RemoveReferences(GraphBuilder.GraphEdges edges)
        {
            foreach (var reference in _projectReferences)
            {
                ErrorUtilities.VerifyThrow(reference._referencingProjects.Contains(this), "references should point to the nodes referencing them");
                reference._referencingProjects.Remove(this);

                edges.RemoveEdge((this, reference));
            }

            _projectReferences.Clear();
        }

        internal ConfigurationMetadata ToConfigurationMetadata()
        {
            return new ConfigurationMetadata(ProjectInstance.FullPath, ProjectInstance.GlobalPropertiesDictionary);
        }
    }
}
