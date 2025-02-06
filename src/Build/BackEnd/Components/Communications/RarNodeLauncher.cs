// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd
{
    internal sealed class RarNodeLauncher
    {
        /// <summary>
        /// Creates a new MSBuild process with the RAR nodemode.
        /// </summary>
        public static void Start()
        {
            // SYNC: src\Tasks\AssemblyDependency\Service\OutOfProcRarNode.cs
            ServerNodeHandshake handshake = new(HandshakeOptions.None);
            string pipeName = $"MSBuildRarNode-{handshake.ComputeHash()}";

            if (!IsRarNodeRunning(pipeName))
            {
                CommunicationsUtilities.Trace("Launching RAR node...");

                try
                {
                    LaunchNode();
                }
                catch (NodeFailedToLaunchException ex)
                {
                    CommunicationsUtilities.Trace("Failed to launch RAR node: {0}.", ex);
                    return;
                }
            }

            ValidateConnection(pipeName, handshake);
        }

        private static bool IsRarNodeRunning(string pipeName)
        {
            // If the node is running, we will find it in the list of named pipes.
            // TODO: Adapt for non-Windows platforms.
            const string NamedPipeRoot = @"\\.\pipe\";
            string[] pipeNames = Directory.GetFiles(NamedPipeRoot);
            return pipeNames.Contains(Path.Combine(NamedPipeRoot, pipeName));
        }

        private static void LaunchNode()
        {
            string msbuildLocation = BuildEnvironmentHelper.Instance.CurrentMSBuildExePath;
            string commandLineArgs = string.Join(" ", ["/nologo", "/nodemode:3"]);
            NodeLauncher nodeLauncher = new();
            _ = nodeLauncher.Start(msbuildLocation, commandLineArgs, nodeId: 0);
        }

        private static void ValidateConnection(string pipeName, Handshake handshake)
        {
            // TODO: Move to RAR client once it is merged. This is just to validate that the handshake implementation is functional.
            using NamedPipeClientStream pipeClient = CommunicationsUtilities.CreateSecurePipeClient(pipeName);

            if (!CommunicationsUtilities.ConnectToPipeStream(pipeClient, pipeName, handshake))
            {
                CommunicationsUtilities.Trace("Failed to connect to RAR node.");
            }
        }
    }
}