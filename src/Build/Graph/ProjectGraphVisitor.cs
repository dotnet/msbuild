// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Graph;

/// <summary>
/// This class helps to visit a graph of nodes.
/// </summary>
public class ProjectGraphVisitor
{
    private readonly HashSet<ProjectGraphNode> _cache;
    private ProjectGraphNode? _startingNode;
    private ProjectGraphNodeDirection _direction;

    /// <summary>
    /// Create an instance of this class.
    /// </summary>
    public ProjectGraphVisitor()
    {
        _cache = new HashSet<ProjectGraphNode>();
    }

    /// <summary>
    /// Find all the nodes with the specified direction.
    /// </summary>
    /// <param name="graphNode">The starting node from which to find other nodes.</param>
    /// <param name="direction">The direction to search for nodes.</param>
    /// <returns>An enumeration of nodes.</returns>
    public IEnumerable<ProjectGraphNode> FindAll(ProjectGraphNode graphNode, ProjectGraphNodeDirection direction)
    {
        ErrorUtilities.VerifyThrowArgumentNull(graphNode, nameof(graphNode));

        if (direction == ProjectGraphNodeDirection.Current)
        {
            yield return graphNode;
            yield break;
        }

        _direction = direction;
        _startingNode = graphNode;
        try
        {
            foreach (var node in Find(graphNode))
            {
                yield return node;
            }
        }
        finally
        {
            _cache.Clear();
        }
    }

    private IEnumerable<ProjectGraphNode> Find(ProjectGraphNode graphNode)
    {
        if (!_cache.Add(graphNode)) yield break;

        foreach (var node in _direction == ProjectGraphNodeDirection.Down ? graphNode.ProjectReferences : graphNode.ReferencingProjects)
        {
            foreach (var subnode in Find(node))
            {
                yield return subnode;
            }
        }

        // Don't report the starting node
        if (_startingNode != graphNode)
        {
            yield return graphNode;
        }
    }
}
