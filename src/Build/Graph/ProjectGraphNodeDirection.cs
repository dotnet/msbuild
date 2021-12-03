// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Graph;

/// <summary>
/// Defines the direction to find nodes.
/// </summary>
public enum ProjectGraphNodeDirection
{
    /// <summary>
    /// Return only the stating node.
    /// </summary>
    Current,

    /// <summary>
    /// Return all the nodes referenced transitively by the starting node.
    /// </summary>
    Down,

    /// <summary>
    /// Return all the nodes referencing transitively the starting node.
    /// </summary>
    Up,
}
