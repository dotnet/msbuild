// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
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

        /// <summary>
        /// Returns a pipe name that encodes both the handshake hash and the process ID.
        /// Format: MSBuild-{hash}-{pid}
        /// This allows discovery of compatible nodes by listing pipes matching the hash prefix,
        /// eliminating trial-and-error probing of all dotnet processes.
        /// </summary>
        internal static string GetHashBasedPipeName(string handshakeHash, int? processId = null)
        {
            processId ??= EnvironmentUtilities.CurrentProcessId;
            string pipeName = $"MSBuild-{handshakeHash}-{processId}";
            return GetPlatformSpecificPipeName(pipeName);
        }

        /// <summary>
        /// Finds pipe files matching a handshake hash and extracts their PIDs.
        /// Only works on Unix where pipes are files in /tmp.
        /// </summary>
        internal static IList<int> FindNodesByHandshakeHash(string handshakeHash)
        {
            var pids = new List<int>();
            // GetPlatformSpecificPipeName returns full paths like /tmp/MSBuild-{hash}-{pid}
            // on Unix, and .NET does NOT add CoreFxPipe_ prefix for absolute paths.
            string prefix = $"MSBuild-{handshakeHash}-";
            string? pipeDir = NativeMethodsShared.IsUnixLike ? "/tmp" : null;

            if (pipeDir == null)
            {
                // On Windows, named pipes aren't files — fall back to legacy discovery.
                return pids;
            }

            try
            {
                foreach (string file in System.IO.Directory.EnumerateFiles(pipeDir, $"MSBuild-{handshakeHash}-*"))
                {
                    string fileName = Path.GetFileName(file);
                    if (fileName.StartsWith(prefix) && int.TryParse(fileName.Substring(prefix.Length), out int pid))
                    {
                        pids.Add(pid);
                    }
                }
            }
            catch
            {
                // Directory enumeration can fail (e.g. permissions); return empty
                // so the caller falls through to launching new nodes.
            }

            return pids;
        }

        internal static string GetPlatformSpecificPipeName(string pipeName)
        {
            if (NativeMethodsShared.IsUnixLike)
            {
                // If we're on a Unix machine then named pipes are implemented using Unix Domain Sockets.
                // Most Unix systems have a maximum path length limit for Unix Domain Sockets, with
                // Mac having a particularly short one. Mac also has a generated temp directory that
                // can be quite long, leaving very little room for the actual pipe name. Fortunately,
                // '/tmp' is mandated by POSIX to always be a valid temp directory, so we can use that
                // instead.
#if !CLR2COMPATIBILITY
                return Path.Combine("/tmp", pipeName);
#else
                // We should never get here. This would be a net35 task host running on unix.
                ErrorUtilities.ThrowInternalError("Task host used on unix in retrieving the pipe name.");
                return string.Empty;
#endif
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
