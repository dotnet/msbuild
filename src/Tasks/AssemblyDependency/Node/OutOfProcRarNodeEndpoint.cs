// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Internal;

namespace Microsoft.Build.Tasks.AssemblyDependency
{
    /// <summary>
    /// Implements a single instance of a pipe server which executes the ResolveAssemblyReference task.
    /// </summary>
    internal sealed class OutOfProcRarNodeEndpoint : IDisposable
    {
        private readonly int _endpointId;

        private readonly NodePipeServer _pipeServer;

        internal OutOfProcRarNodeEndpoint(
            int endpointId,
            string pipeName,
            ServerNodeHandshake handshake,
            int maxNumberOfServerInstances,
            NodePacketFactory packetFactory)
        {
            _endpointId = endpointId;
            _pipeServer = new NodePipeServer(pipeName, handshake, maxNumberOfServerInstances);
            _pipeServer.RegisterPacketFactory(packetFactory);
        }

        public void Dispose() => _pipeServer.Dispose();

        internal async Task RunAsync(CancellationToken cancellationToken = default)
        {
            CommunicationsUtilities.Trace("({0}) Starting RAR endpoint.", _endpointId);

            try
            {
                await RunInternalAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Swallow cancellation excpetions for now. We're using this as a simple way to gracefully shutdown the
                // endpoint, instead of having to implement separate Start / Stop methods and deferring to the caller.
                // Can reevaluate if we need more granular control over cancellation vs shutdown.
                CommunicationsUtilities.Trace("({0}) RAR endpoint stopped due to cancellation.", _endpointId);
            }
        }

        private async Task RunInternalAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                while (!_pipeServer.IsConnected)
                {
                    _ = _pipeServer.WaitForConnection();
                }

                CommunicationsUtilities.Trace("({0}) Received RAR request.", _endpointId);

                try
                {
                    INodePacket packet = await _pipeServer.ReadPacketAsync(cancellationToken);

                    if (packet.Type == NodePacketType.NodeShutdown)
                    {
                        // Although the client has already disconnected, it is still necessary to Diconnect() so the
                        // pipe can transition into PipeState.Disonnected, which is treated as an intentional pipe break.
                        // Otherwise, all future operations on the pipe will throw an exception.
                        CommunicationsUtilities.Trace("({0}) RAR client disconnected.", _endpointId);
                        _pipeServer.Disconnect();
                        continue;
                    }

                    RarNodeExecuteRequest request = (RarNodeExecuteRequest)packet;

                    // TODO: Use request packet to set inputs on the RAR task.
                    ResolveAssemblyReference rarTask = new();

                    // TODO: bool success = rarTask.ExecuteInProcess();
                    // TODO: Use RAR task outputs to create response packet.
                    await _pipeServer.WritePacketAsync(new RarNodeExecuteResponse(), cancellationToken);

                    CommunicationsUtilities.Trace("({0}) Completed RAR request.", _endpointId);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    CommunicationsUtilities.Trace("({0}) Exception while executing RAR request: {1}", _endpointId, e);
                }
            }

            _pipeServer.Disconnect();
        }
    }
}
