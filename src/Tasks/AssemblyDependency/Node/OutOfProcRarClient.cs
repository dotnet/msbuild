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
        private readonly NodePipeClient _pipeClient;

        private OutOfProcRarClient()
        {
            ServerNodeHandshake handshake = new(HandshakeOptions.None);
            _pipeClient = new NodePipeClient(NamedPipeUtil.GetRarNodeEndpointPipeName(handshake), handshake);

            NodePacketFactory packetFactory = new();
            packetFactory.RegisterPacketHandler(NodePacketType.RarNodeExecuteResponse, RarNodeExecuteResponse.FactoryForDeserialization, null);
            _pipeClient.RegisterPacketFactory(packetFactory);
        }

        public void Dispose() => _pipeClient.Dispose();

        internal static OutOfProcRarClient GetInstance(IBuildEngine10 buildEngine)
        {
            // Create a single cached instance for this build.
            const string OutOfProcRarClientKey = "OutOfProcRarClient";

            // We want to reuse the pipe client across all RAR invocations within a build, but release the connection once
            // the MSBuild node is idle. Using RegisteredTaskObjectLifetime.Build ensures that the RAR client is disposed between
            // builds, freeing the server to run other requests.
            OutOfProcRarClient rarClient = (OutOfProcRarClient)buildEngine.GetRegisteredTaskObject(OutOfProcRarClientKey, RegisteredTaskObjectLifetime.Build);

            if (rarClient == null)
            {
                rarClient = new OutOfProcRarClient();
                buildEngine.RegisterTaskObject(OutOfProcRarClientKey, rarClient, RegisteredTaskObjectLifetime.Build, allowEarlyCollection: false);
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
                _pipeClient.ConnectToServer(0);
            }

            // TODO: Use RAR task to create the request packet.
            _pipeClient.WritePacket(new RarNodeExecuteRequest());

            // TODO: Use response packet to set RAR task outputs.
            _ = (RarNodeExecuteResponse)_pipeClient.ReadPacket();

            return true;
        }
    }
}
