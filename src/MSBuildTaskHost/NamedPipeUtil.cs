// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.Build.Internal;

namespace Microsoft.Build.Shared
{
    internal static class NamedPipeUtil
    {
        internal static string GetPlatformSpecificPipeName(int? processId = null)
        {
            if (processId is null)
            {
                processId = EnvironmentUtilities.CurrentProcessId;
            }

            string pipeName = $"MSBuild{processId}";

            return GetPlatformSpecificPipeName(pipeName);
        }

        internal static string GetPlatformSpecificPipeName(string pipeName)
        {
            if (NativeMethodsShared.IsUnixLike)
            {
                // We should never get here. This would be a net35 task host running on unix.
                ErrorUtilities.ThrowInternalError("Task host used on unix in retrieving the pipe name.");
                return string.Empty;
            }
            else
            {
                return pipeName;
            }
        }

        internal static string GetRarNodePipeName(ServerNodeHandshake handshake)
            => GetPlatformSpecificPipeName($"MSBuildRarNode-{handshake.ComputeHash()}");

        internal static string GetRarNodeEndpointPipeName(ServerNodeHandshake handshake)
            => GetPlatformSpecificPipeName($"MSBuildRarNodeEndpoint-{handshake.ComputeHash()}");
    }
}
