// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CodeDom;
using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Execution
{
    public sealed class RarNode : INode
    {
        /// <summary>
        /// The amount of time to wait for the client to connect to the host.
        /// </summary>
        private const int ClientConnectTimeout = 60000;

        public NodeEngineShutdownReason Run(bool nodeReuse, bool lowPriority, out Exception shutdownException)
        {
            shutdownException = null;
            using CancellationTokenSource cts = new CancellationTokenSource();
            string pipeName = CommunicationsUtilities.GetRarPipeName(nodeReuse, lowPriority);
            IRarController controller = GetController(pipeName);

            Task<int> rarTask = controller.StartAsync(cts.Token);

            Handshake handshake = NodeProviderOutOfProc.GetHandshake(enableNodeReuse: nodeReuse,
                                                                     enableLowPriority: lowPriority, specialNode: true);
            Task<NodeEngineShutdownReason> msBuildShutdown = RunShutdownCheckAsync(handshake, cts.Token);

            // Wait for any of these task to finish:
            // - rarTask can timeout (default is 15 mins)
            // - msBuildShutdown ends when it recieves command to shutdown
            int index = Task.WaitAny(msBuildShutdown, rarTask);
            cts.Cancel();

            if (index == 0)
            {
                // We know that the task completed, so we can get Result without waiting for it.
                return msBuildShutdown.Result;
            }
            else
            {
                // RAR task can only exit with connection error or timeout
                return NodeEngineShutdownReason.ConnectionFailed;
            }
        }

        private static IRarController GetController(string pipeName)
        {
            const string rarControllerName = "Microsoft.Build.Tasks.ResolveAssemblyReferences.Server.RarController, Microsoft.Build.Tasks.Core";
            Type rarControllerType = Type.GetType(rarControllerName);

            Func<string, int?, int?, int, bool, NamedPipeServerStream> streamFactory = NamedPipeUtil.CreateNamedPipeServer;
            IRarController controller = (IRarController)Activator.CreateInstance(rarControllerType, pipeName, streamFactory, null);

            ErrorUtilities.VerifyThrow(controller != null, "Couldn't create instace of IRarController for '{0}' type", rarControllerName);
            return controller;
        }


        public NodeEngineShutdownReason Run(out Exception shutdownException)
        {
            return Run(false, false, out shutdownException);
        }

        private async Task<NodeEngineShutdownReason> RunShutdownCheckAsync(Handshake handshake, CancellationToken cancellationToken = default)
        {
            string pipeName = NamedPipeUtil.GetPipeNameOrPath("MSBuild" + Process.GetCurrentProcess().Id);

            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                    return NodeEngineShutdownReason.BuildComplete;

                using NamedPipeServerStream serverStream = NamedPipeUtil.CreateNamedPipeServer(pipeName, maxNumberOfServerInstances: NamedPipeServerStream.MaxAllowedServerInstances);
                await serverStream.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                bool connected = NamedPipeUtil.ValidateHandshake(handshake, serverStream, ClientConnectTimeout);
                if (!connected)
                    continue;

                // Header consits of:
                // 1 byte - Packet type
                // 4 bytes - packet length
                byte[] header = new byte[5];
                int bytesRead = await serverStream.ReadAsync(header, 0, header.Length).ConfigureAwait(false);
                if (bytesRead != header.Length)
                {
                    continue;
                }

                NodePacketType packetType = (NodePacketType)Enum.ToObject(typeof(NodePacketType), header[0]);
                // Packet type sent by Shutdown
                if (packetType == NodePacketType.NodeBuildComplete)
                {
                    // Body of NodeBuildComplete contains only one boolean (= 1 byte)
                    byte[] packetBody = new byte[sizeof(bool)];
                    await serverStream.ReadAsync(packetBody, 0, packetBody.Length, cancellationToken).ConfigureAwait(false);
                    bool nodeReuse = Convert.ToBoolean(packetBody[0]);

                    return nodeReuse ? NodeEngineShutdownReason.BuildCompleteReuse : NodeEngineShutdownReason.BuildComplete;
                }
            }
        }
    }
}
