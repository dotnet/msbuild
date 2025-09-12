// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Framework;

/// <summary>
/// Marker interface for Task Factory which creates tasks in a way compatible with out-of-process execution.
/// Currently only TaskFactories shipped with MSBuild support out-of-process execution. 
/// They are marked with this intrerface to distinguish them from exterally defined TaskFactories.
/// </summary>
internal interface IOutOfProcTaskFactory
{
    /// <summary>
    /// Gets the assembly path for the compiled task that can be used by out-of-process task hosts.
    /// </summary>
    /// <returns>The absolute path to the assembly file that contains the compiled task, or null if not available.</returns>
    string GetAssemblyPath();
}
