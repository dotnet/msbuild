// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CodeDom;
using System.Diagnostics;
using System.IO;
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

        /// <summary>
        /// Fully qualified name of RarController, used for providing <see cref="IRarController" /> instance to <see cref="RarNode" />
        /// </summary>
        private const string RarControllerName = "Microsoft.Build.Tasks.ResolveAssemblyReferences.Server.RarController, Microsoft.Build.Tasks.Core";


        /// <summary>
        /// Timeout for node shutdown
        /// </summary>
        private static readonly TimeSpan NodeShutdownTimeout = TimeSpan.FromHours(1);

        public NodeEngineShutdownReason Run(bool nodeReuse, bool lowPriority, out Exception shutdownException, CancellationToken cancellationToken = default)
        {
            shutdownException = null;
            using CancellationTokenSource cts = new CancellationTokenSource();
            string pipeName = CommunicationsUtilities.GetRarPipeName(nodeReuse, lowPriority);
            Handshake handshake = NodeProviderOutOfProc.GetHandshake(enableNodeReuse: nodeReuse,
                                                                     enableLowPriority: lowPriority, specialNode: true);

            IRarController controller = GetController(pipeName, handshake);

            Task<int> rarTask = controller.StartAsync(cts.Token);

            Task<NodeEngineShutdownReason> msBuildShutdown = RunShutdownCheckAsync(handshake, cts.Token);

            // Timeout for node, limits lifetime of node to 1 hour
            cts.CancelAfter(NodeShutdownTimeout);
            int index;
            try
            {
                // Wait for any of these tasks to finish:
                // - rarTask can timeout (default is 15 minutes)
                // - msBuildShutdown ends when it receives command to shutdown
                // - node lifetime expires
                index = Task.WaitAny(new Task[] { msBuildShutdown, rarTask }, cts.Token);
            }
            catch (OperationCanceledException e)
            {
                shutdownException = e;
                return NodeEngineShutdownReason.Error;
            }

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

        private static IRarController GetController(string pipeName, Handshake handshake)
        {
            Type rarControllerType = Type.GetType(RarControllerName);

            Func<string, int?, int?, int, bool, NamedPipeServerStream> streamFactory = NamedPipeUtil.CreateNamedPipeServer;
            Func<Handshake, NamedPipeServerStream, int, bool> validateCallback = NamedPipeUtil.ValidateHandshake;
            IRarController controller = Activator.CreateInstance(rarControllerType, pipeName, handshake, streamFactory, validateCallback, null) as IRarController;

            ErrorUtilities.VerifyThrow(controller != null, ResourceUtilities.GetResourceString("RarControllerReflectionError"), RarControllerName);
            return controller;
        }

        public NodeEngineShutdownReason Run(out Exception shutdownException)
        {
            return Run(false, false, out shutdownException);
        }

        private async Task<NodeEngineShutdownReason> RunShutdownCheckAsync(Handshake handshake, CancellationToken cancellationToken = default)
        {
            string pipeName = NamedPipeUtil.GetPipeNameOrPath("MSBuild" + Process.GetCurrentProcess().Id);

            static async Task<int> ReadAsync(Stream stream, byte[] buffer, int bytesToRead)
            {
                int totalBytesRead = 0;
                while (totalBytesRead < bytesToRead)
                {
                    int bytesRead = await stream.ReadAsync(buffer, totalBytesRead, bytesToRead - totalBytesRead);
                    if (bytesRead == 0)
                    {
                        return totalBytesRead;
                    }
                    totalBytesRead += bytesRead;
                }
                return totalBytesRead;
            }

            // Most common path in this while loop in long run will be over the continue statement.
            // This is happening because the MSBuild when starting new nodes is trying in some cases to reuse nodes (see nodeReuse switch).
            // It is done by listing the MSBuild processes and then connecting to them and validating the handshake.
            // In most cases for this loop it will fail, which will lead to hitting the continue statement.
            // If we get over that, the MSBuild should send NodeBuildComplete packet, which will indicate that the engine is requesting to shutdown this node.
            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                    return NodeEngineShutdownReason.BuildComplete;

                using NamedPipeServerStream serverStream = NamedPipeUtil.CreateNamedPipeServer(pipeName, maxNumberOfServerInstances: NamedPipeServerStream.MaxAllowedServerInstances);
                await serverStream.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                bool connected = NamedPipeUtil.ValidateHandshake(handshake, serverStream, ClientConnectTimeout);
                if (!connected)
                    continue;

                // Header consists of:
                // 1 byte - Packet type
                // 4 bytes - packet length
                byte[] header = new byte[5];
                int bytesRead = await ReadAsync(serverStream, header, header.Length).ConfigureAwait(false);
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
