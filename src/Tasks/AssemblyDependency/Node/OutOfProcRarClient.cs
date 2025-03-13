// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks.AssemblyDependency
{
    internal class OutOfProcRarClient
    {
        private readonly NodePipeClient _pipeClient;

        public OutOfProcRarClient()
        {
            ServerNodeHandshake handshake = new(HandshakeOptions.None);
            _pipeClient = new NodePipeClient(NamedPipeUtil.GetRarNodeEndpointPipeName(handshake), handshake);

            NodePacketFactory packetFactory = new();
            packetFactory.RegisterPacketHandler(NodePacketType.RarNodeExecuteResponse, RarNodeExecuteRequest.FactoryForDeserialization, null);
            _pipeClient.RegisterPacketFactory(packetFactory);
        }

        public bool Execute(ResolveAssemblyReference rarTask)
        {
            // Don't set a timeout since the build manager currently blocks until the server is running.
            _pipeClient.ConnectToServer(0);

            // TODO: Use RAR task to create the request packet.
            _pipeClient.WritePacket(new RarNodeExecuteRequest());

            // TODO: Use response packet to set RAR task outputs.
            _ = (RarNodeExecuteResponse)_pipeClient.ReadPacket();

            throw new NotImplementedException("RAR node communication succeeded, but task execution is unimplemented.");
        }
    }
}
