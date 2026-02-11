// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Internal;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Represents the configuration data needed to launch a node process.
    /// </summary>
    /// <param name="MSBuildLocation">The path to the executable to launch (e.g., MSBuild.exe or dotnet.exe).</param>
    /// <param name="CommandLineArgs">The command line arguments to pass to the executable.</param>
    /// <param name="Handshake">The handshake data used to establish communication with the node process.</param>
    /// <param name="EnvironmentOverrides">
    /// Optional environment variable overrides for the process.
    /// A non-null value sets or overrides that variable. A null value removes the variable
    /// from the child process environment - this is used to clear architecture-specific
    /// DOTNET_ROOT variants (e.g., DOTNET_ROOT_X64) that would otherwise take precedence
    /// over DOTNET_ROOT when launching an app host.
    /// </param>
    internal readonly record struct NodeLaunchData(
        string MSBuildLocation,
        string CommandLineArgs,
        Handshake? Handshake = null,
        IDictionary<string, string>? EnvironmentOverrides = null);

    internal interface INodeLauncher
    {
        /// <summary>
        /// Creates a new MSBuild process using the specified launch configuration.
        /// </summary>
        /// <param name="launchData">The configuration data for launching the node process.</param>
        /// <param name="nodeId">The unique identifier for the node being launched.</param>
        /// <returns>The started process.</returns>
        Process Start(NodeLaunchData launchData, int nodeId);
    }
}
