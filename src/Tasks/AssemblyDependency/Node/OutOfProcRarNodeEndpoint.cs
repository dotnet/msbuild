// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks.AssemblyDependency
{
    internal class OutOfProcRarNodeEndpoint : IDisposable
    {
        private readonly int _endpointId;

        private readonly NodePipeServer _pipeServer;

        internal OutOfProcRarNodeEndpoint(int endpointId, ServerNodeHandshake handshake, int maxNumberOfServerInstances)
        {
            _endpointId = endpointId;
            _pipeServer = new NodePipeServer(NamedPipeUtil.GetRarNodeEndpointPipeName(handshake), handshake, maxNumberOfServerInstances);

            NodePacketFactory packetFactory = new();
            packetFactory.RegisterPacketHandler(NodePacketType.RarNodeExecuteRequest, RarNodeExecuteRequest.FactoryForDeserialization, null);
            _pipeServer.RegisterPacketFactory(packetFactory);
        }

        public void Dispose() => _pipeServer.Dispose();

        internal void Run(CancellationToken cancellationToken = default)
        {
            CommunicationsUtilities.Trace("({0}) Starting RAR endpoint.", _endpointId);

            try
            {
                RunInternal(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Swallow cancellation excpetions for now. We're using this as a simple way to gracefully shutdown the
                // endpoint, instead of having to implement separate Start / Stop methods and deferring to the caller.
                // Can reevaluate if we need more granular control over cancellation vs shutdown.
            }
        }

        private void RunInternal(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                LinkStatus linkStatus = _pipeServer.WaitForConnection();

                if (linkStatus != LinkStatus.Active)
                {
                    // We either timed out or failed to connect to a client.
                    // Just continue running since the RAR endpoint isn't tied to a specific client.
                    continue;
                }

                CommunicationsUtilities.Trace("({0}) Connected to RAR client.", _endpointId);

                try
                {
                    RarNodeExecuteRequest request = (RarNodeExecuteRequest)_pipeServer.ReadPacket();

                    // TODO: Use request packet to set inputs on the RAR task.
                    ResolveAssemblyReference rarTask = new();

                    // TODO: bool success = rarTask.ExecuteInProcess();
                    // TODO: Use RAR task outputs to create response packet.
                    _pipeServer.WritePacket(new RarNodeExecuteResponse());

                    CommunicationsUtilities.Trace("({0}) Completed RAR request.", _endpointId);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    CommunicationsUtilities.Trace("({0}) Exception while executing RAR request: {1}", _endpointId, e);
                }

                _pipeServer.Disconnect();
            }
        }
    }
}
