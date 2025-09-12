// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks.AssemblyDependency
{
    /// <summary>
    /// Implements a client for sending the ResolveAssemblyReference task to an out-of-proc node.
    /// This is intended to be reused for all RAR tasks across a single build.
    /// </summary>
    internal sealed class OutOfProcRarClient : IDisposable
    {
        // Create a single cached instance for this build.
        internal const string TaskObjectCacheKey = "OutOfProcRarClient";

        private readonly NodePipeClient _pipeClient;

        private OutOfProcRarClient()
        {
            ServerNodeHandshake handshake = new(HandshakeOptions.None);
            _pipeClient = new NodePipeClient(NamedPipeUtil.GetRarNodeEndpointPipeName(handshake), handshake);

            NodePacketFactory packetFactory = new();
            packetFactory.RegisterPacketHandler(NodePacketType.RarNodeExecuteResponse, static t => new RarNodeExecuteResponse(t), null);
            _pipeClient.RegisterPacketFactory(packetFactory);
        }

        public void Dispose() => _pipeClient.Dispose();

        internal static OutOfProcRarClient GetInstance(IBuildEngine10 buildEngine)
        {
            // We want to reuse the pipe client across all RAR invocations within a build, but release the connection once
            // the MSBuild node is idle. Using RegisteredTaskObjectLifetime.Build ensures that the RAR client is disposed between
            // builds, freeing the server to run other requests.
            OutOfProcRarClient rarClient = (OutOfProcRarClient)buildEngine.GetRegisteredTaskObject(TaskObjectCacheKey, RegisteredTaskObjectLifetime.Build);

            if (rarClient == null)
            {
                rarClient = new OutOfProcRarClient();
                buildEngine.RegisterTaskObject(TaskObjectCacheKey, rarClient, RegisteredTaskObjectLifetime.Build, allowEarlyCollection: false);
                CommunicationsUtilities.Trace("Initialized new RAR client.");
            }

            return rarClient;
        }

        internal bool Execute(ResolveAssemblyReference rarTask)
        {
            // This should only be true at the start of a build.
            if (!_pipeClient.IsConnected)
            {
                // Don't set a timeout since the build manager already blocks until the server is running.
                if (!_pipeClient.ConnectToServer(0))
                {
                    return false;
                }
            }

            _pipeClient.WritePacket(new RarNodeExecuteRequest(rarTask));

            INodePacket packet = _pipeClient.ReadPacket();

            while (packet.Type != NodePacketType.RarNodeExecuteResponse)
            {
                if (packet.Type == NodePacketType.RarNodeBufferedLogEvents)
                {
                    // TODO: Stub for replaying logs to the real build engine.
                }
                else
                {
                    ErrorUtilities.ThrowInternalError($"Received unexpected packet type {packet.Type}");
                }

                packet = _pipeClient.ReadPacket();
            }

            RarNodeExecuteResponse response = (RarNodeExecuteResponse)packet;
            response.SetTaskOutputs(rarTask);

            return response.Success;
        }
    }
}
