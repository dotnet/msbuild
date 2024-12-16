// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Telemetry;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd.Client
{
    public sealed class RarNodeLauncher
    {
        public static void LaunchServer()
        {
            CommunicationsUtilities.Trace("Checking for RAR server...");
            RarNodeHandshake handshake = new(HandshakeOptions.None);

            if (IsPipeExists(handshake))
            {
                CommunicationsUtilities.Trace($"Existing RAR server found.");
                return;
            }

            string serverLaunchMutexName = $@"Global\msbuild-rar-server-launch-{handshake.ComputeHash()}";

            // Then, check if another MSBuild process has started the server launch.
            // This accounts for any delay between the server starting and opening its mutex.
            using Mutex serverLaunchMutex = new(
                initiallyOwned: true,
                name: serverLaunchMutexName,
                createdNew: out bool createdNew);

            if (!createdNew)
            {
                CommunicationsUtilities.Trace("Another process launching the RAR server.");
                return;
            }

            CommunicationsUtilities.Trace("Starting Server...");

            string msbuildLocation = BuildEnvironmentHelper.Instance.CurrentMSBuildExePath;
            string commandLineArgs = string.Join(" ", ["/nologo", "/nodemode:3"]);

            NodeLauncher nodeLauncher = new();
            Process msbuildProcess = nodeLauncher.Start(msbuildLocation, commandLineArgs, nodeId: 0);

            CommunicationsUtilities.Trace("RAR Server started with PID: {0}", msbuildProcess.Id);
        }

        private static bool IsPipeExists(RarNodeHandshake handshake)
        {
            string[] pipes = Directory.GetFiles(@"\\.\pipe\");

            return pipes.Contains(Path.Combine(@"\\.\pipe\", $"msbuild-rar-{handshake.ComputeHash()}"));
        }

        private static bool IsServerRunning(ServerNodeHandshake handshake)
        {
            string serverRunningMutexName = $@"Global\msbuild-rar-server-running-{handshake.ComputeHash()}";

            // First, check if the server has created the mutex.
            // Use a mutex to avoid using a timeout or checking a max pipe instance exception.
            bool isRunning = Mutex.TryOpenExisting(serverRunningMutexName, out Mutex? mutex);
            mutex?.Dispose();

            return isRunning;
        }
    }
}