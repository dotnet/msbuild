// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental
{
    /// <summary>
    /// This class represents an implementation of INode for out-of-proc server nodes aka MSBuild server
    /// </summary>
    public sealed partial class OutOfProcServerNode
    {
        internal static string GetPipeName(ServerNodeHandshake handshake)
            => NamedPipeUtil.GetPlatformSpecificPipeName($"MSBuildServer-{handshake.ComputeHash()}");

        internal static string GetRunningServerMutexName(ServerNodeHandshake handshake)
            => $@"Global\msbuild-server-running-{handshake.ComputeHash()}";

        internal static string GetBusyServerMutexName(ServerNodeHandshake handshake)
            => $@"Global\msbuild-server-busy-{handshake.ComputeHash()}";
    }
}
