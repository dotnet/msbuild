// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Graph;

/// <summary>
/// A delegate that should return the filepath where associated cache file for a graph node is stored.
/// </summary>
/// <param name="graphNode">A graph node.</param>
/// <returns>The filepath where associated cache file for a graph node is stored</returns>
public delegate string GraphBuildCacheFilePathDelegate(ProjectGraphNode graphNode);
