// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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
            _pipeName = NamedPipeUtil.GetPlatformSpecificPipeName($"MSBuildRarNode-{_handshake.ComputeHash()}");
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
            // Determine if the node is running by checking if the expected named pipe exists.
            if (NativeMethodsShared.IsWindows)
            {
                const string NamedPipeRoot = @"\\.\pipe\";

                // File.Exists() will crash the pipe server, as the underlying Windows APIs have undefined behavior
                // when used with pipe objects. Enumerating the pipe directory avoids this issue.
                IEnumerable<string> pipeNames = FileSystems.Default.EnumerateFiles(NamedPipeRoot);

                return pipeNames.Contains(Path.Combine(NamedPipeRoot, _pipeName));
            }
            else
            {
                // On Unix, named pipes are implemented via sockets, and the pipe name is simply the file path.
                return FileSystems.Default.FileExists(_pipeName);
            }
        }

        private void LaunchNode()
        {
            string msbuildLocation = BuildEnvironmentHelper.Instance.CurrentMSBuildExePath;
            string commandLineArgs = string.Join(" ", ["/nologo", "/nodemode:3"]);
            _ = _nodeLauncher.Start(msbuildLocation, commandLineArgs, nodeId: 0);
        }

        private void ValidateConnection()
        {
            // TODO: Move to RAR client once it is merged. This exists just to validate that the handshake implementation is functional,
            // TODO: otherwise this may add latency to the build start.
            using NamedPipeClientStream pipeClient = CommunicationsUtilities.CreateSecurePipeClient(_pipeName);

            try
            {
                CommunicationsUtilities.ConnectToPipeStream(pipeClient, _pipeName, _handshake);
            }
            catch (Exception ex)
            {
                CommunicationsUtilities.Trace("Failed to connect to RAR node: {0}", ex);
            }
        }
    }
}
