// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Build.BackEnd
{
    internal interface INodeLauncher
    {
        /// <summary>
        /// Creates a new MSBuild process with optional environment variable overrides.
        /// </summary>
        /// <param name="msbuildLocation">Path to the MSBuild executable or app host.</param>
        /// <param name="commandLineArgs">Command line arguments for the process.</param>
        /// <param name="nodeId">The node ID for this process.</param>
        /// <param name="environmentOverrides">
        /// Environment variables to set or remove in the child process.
        /// A non-null value sets or overrides that variable. A null value removes the variable
        /// from the child process environment - this is used to clear architecture-specific
        /// DOTNET_ROOT variants (e.g., DOTNET_ROOT_X64) that would otherwise take precedence
        /// over DOTNET_ROOT when launching an app host.
        /// </param>
        /// <returns>The started process.</returns>
        Process Start(string msbuildLocation, string commandLineArgs, int nodeId, IDictionary<string, string>? environmentOverrides = null);
    }
}
