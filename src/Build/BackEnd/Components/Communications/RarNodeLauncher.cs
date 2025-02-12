// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

namespace Microsoft.Build.BackEnd
{
    internal sealed class RarNodeLauncher
    {
        private readonly INodeLauncher _nodeLauncher;

        private readonly ServerNodeHandshake _handshake;

        private readonly string _pipeName;

        internal RarNodeLauncher(INodeLauncher nodeLauncher)
        {
            _nodeLauncher = nodeLauncher;
            _handshake = new ServerNodeHandshake(HandshakeOptions.None);

            // SYNC: src\Tasks\AssemblyDependency\Service\OutOfProcRarNode.cs
            _pipeName = $"MSBuildRarNode-{_handshake.ComputeHash()}";
        }

        /// <summary>
        /// Creates a new MSBuild process with the RAR nodemode.
        /// </summary>
        public void Start()
        {
            if (!IsRarNodeRunning())
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

            ValidateConnection();
        }

        private bool IsRarNodeRunning()
        {
            // If the node is running, we will find it in the list of named pipes.
            // TODO: Adapt for non-Windows platforms.
            const string NamedPipeRoot = @"\\.\pipe\";
            IEnumerable<string> pipeNames = FileSystems.Default.EnumerateFiles(NamedPipeRoot);

            return pipeNames.Contains(Path.Combine(NamedPipeRoot, _pipeName));
        }

        private void LaunchNode()
        {
            string msbuildLocation = BuildEnvironmentHelper.Instance.CurrentMSBuildExePath;
            string commandLineArgs = string.Join(" ", ["/nologo", "/nodemode:3"]);
            _ = _nodeLauncher.Start(msbuildLocation, commandLineArgs, nodeId: 0);
        }

        private void ValidateConnection()
        {
            // TODO: Move to RAR client once it is merged. This is just to validate that the handshake implementation is functional.
            using NamedPipeClientStream pipeClient = CommunicationsUtilities.CreateSecurePipeClient(_pipeName);

            if (!CommunicationsUtilities.ConnectToPipeStream(pipeClient, _pipeName, _handshake))
            {
                CommunicationsUtilities.Trace("Failed to connect to RAR node.");
            }
        }
    }
}