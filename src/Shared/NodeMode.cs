// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// Enumeration of the various node modes that MSBuild.exe can run in.
    /// </summary>
    public enum NodeMode
    {
        /// <summary>
        /// Normal out-of-process node.
        /// </summary>
        OutOfProcNode = 1,

        /// <summary>
        /// Out-of-process task host node.
        /// </summary>
        OutOfProcTaskHostNode = 2,

        /// <summary>
        /// Out-of-process RAR (ResolveAssemblyReference) service node.
        /// </summary>
        OutOfProcRarNode = 3,

        /// <summary>
        /// Out-of-process server node.
        /// </summary>
        OutOfProcServerNode = 8,
    }

    /// <summary>
    /// Helper methods for the NodeMode enum.
    /// </summary>
    public static class NodeModeHelper
    {
        /// <summary>
        /// Converts a NodeMode value to a command line argument string.
        /// </summary>
        /// <param name="nodeMode">The node mode to convert</param>
        /// <returns>The command line argument string (e.g., "/nodemode:1")</returns>
        public static string ToCommandLineArgument(NodeMode nodeMode) => $"/nodemode:{(int)nodeMode}";
    }
}
