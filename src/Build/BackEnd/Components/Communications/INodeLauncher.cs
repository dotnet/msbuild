// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Build.Internal;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    ///  Represents the configuration data needed to launch a node process.
    /// </summary>
    /// <param name="MSBuildLocation">
    ///  The path to the MSBuild binary to launch (e.g., MSBuild.exe, MSBuild.dll or MSBuildTaskHost.exe).
    ///  If <see langword="null"/> is passed, <see cref="Execution.BuildParameters.NodeExeLocation"/> will
    ///  be used as the default MSBuild location.
    /// </param>
    /// <param name="CommandLineArgs">The command line arguments to pass to the executable.</param>
    /// <param name="Handshake">The handshake data used to establish communication with the node process.</param>
    /// <param name="UsingDotNetExe">
    ///  <see langword="true"/> if the dotnet.exe should be used to launch the MSBuild assembly;
    ///  <see langword="false"/> if the MSBuild executable should be launched directly.
    /// </param>
    internal readonly record struct NodeLaunchData(
        string? MSBuildLocation,
        string CommandLineArgs,
        Handshake Handshake,
        bool UsingDotNetExe = false)
    {
    }

    internal interface INodeLauncher
    {
        Process Start(string msbuildLocation, string commandLineArgs, int nodeId);
    }
}
