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
	/// Returns the file system path of the task assembly produced by the factory, if available.
	/// </summary>
	/// <remarks>
	/// Implementations should return an absolute path when out-of-proc execution is enabled. When not applicable,
	/// they may return <see langword="null"/>.
	/// </remarks>
	string? GetAssemblyPath();
}
