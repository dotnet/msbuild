// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Server;

namespace Microsoft.Build.Execution
{
    public sealed class RarNode : INode
    {
        /// <summary>
        /// The amount of time to wait for the client to connect to the host.
        /// </summary>
        private const int ClientConnectTimeout = 60000;

        private Task<int> _rarTask;
        private Task<NodeEngineShutdownReason> _msBuildShutdown;

        public NodeEngineShutdownReason Run(bool nodeReuse, bool lowPriority, out Exception shutdownException)
        {
            shutdownException = null;
            using CancellationTokenSource cts = new CancellationTokenSource();
            string pipeName = CommunicationsUtilities.GetRarPipeName(nodeReuse, lowPriority);
            RarController controller = new RarController(pipeName);

            _rarTask = controller.StartAsync(cts.Token);

            Handshake handshake = NodeProviderOutOfProc.GetHandshake(nodeReuse, enableLowPriority: lowPriority, specialNode: true);
            _msBuildShutdown = RunShutdownCheckAsync(handshake, cts.Token);

            int index = Task.WaitAny(_msBuildShutdown, _rarTask);
            cts.Cancel();

            if(index == 0)
            {
                // We know that this task is completed so we can get Result without worring about waiting for it
                return _msBuildShutdown.Result;
            }
            else
            {
                // RAR task can only exit with connection error or timeout
                return NodeEngineShutdownReason.ConnectionFailed;
            }
        }

        public NodeEngineShutdownReason Run(out Exception shutdownException)
        {
            return Run(false, false, out shutdownException);
        }

        private async Task<NodeEngineShutdownReason> RunShutdownCheckAsync(Handshake handshake, CancellationToken cancellationToken = default)
        {
            string pipeName = NamedPipeUtil.GetPipeNameOrPath("MSBuild" + Process.GetCurrentProcess().Id);
            using NamedPipeServerStream serverStream = NamedPipeUtil.CreateNamedPipeServer(pipeName, maxNumberOfServerInstances: NamedPipeServerStream.MaxAllowedServerInstances);

            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                    return NodeEngineShutdownReason.Error;

                await serverStream.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                bool conected = NamedPipeUtil.ValidateHandshake(handshake, serverStream, ClientConnectTimeout);
                if (!conected)
                    continue;

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
                    // Finish this task
                    break;
                }
            }

            return NodeEngineShutdownReason.BuildComplete;
        }
    }
}
