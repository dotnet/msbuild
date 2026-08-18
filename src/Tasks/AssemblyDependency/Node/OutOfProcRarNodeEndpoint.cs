// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks.AssemblyDependency
{
    /// <summary>
    /// Implements a single instance of a pipe server which executes the ResolveAssemblyReference task.
    /// </summary>
    internal sealed class OutOfProcRarNodeEndpoint : IDisposable
    {
        private readonly int _endpointId;
        private readonly NodePipeServer _pipeServer;
        private readonly RarNodeBuildEngine _buildEngine = new();

        internal OutOfProcRarNodeEndpoint(int endpointId, SharedConfig config)
        {
            _endpointId = endpointId;
            _pipeServer = new NodePipeServer(config.PipeName, config.Handshake, config.MaxNumberOfServerInstances);
            _pipeServer.RegisterPacketFactory(config.PacketFactory);
        }

        public void Dispose() => _pipeServer.Dispose();

        internal static SharedConfig CreateConfig(int maxNumberOfServerInstances)
        {
            ServerNodeHandshake handshake = new(HandshakeOptions.None);
            NodePacketFactory packetFactory = new();
            packetFactory.RegisterPacketHandler(NodePacketType.RarNodeExecuteRequest, static t => new RarNodeExecuteRequest(t), null);

            return new SharedConfig(
                pipeName: NamedPipeUtil.GetRarNodeEndpointPipeName(handshake),
                handshake,
                packetFactory,
                maxNumberOfServerInstances);
        }

        internal async Task RunAsync(CancellationToken cancellationToken = default)
        {
            CommunicationsUtilities.Trace("({0}) Starting RAR endpoint.", _endpointId);

            try
            {
                await RunInternalAsync(cancellationToken).ConfigureAwait(false);
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
                if (!_pipeServer.IsConnected)
                {
                    LinkStatus linkStatus = await _pipeServer.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                    if (linkStatus != LinkStatus.Active)
                    {
                        continue;
                    }
                }

                try
                {
                    INodePacket packet = await _pipeServer.ReadPacketAsync(cancellationToken).ConfigureAwait(false);
                    NodePacketType packetType = packet.Type;

                    CommunicationsUtilities.Trace("({0}) Received request.", _endpointId);

                    switch (packet.Type)
                    {
                        case NodePacketType.RarNodeEndpointConfiguration:
                            // TODO: Pass in client state such as immutable directories, environment variables, ect.
                            break;
                        case NodePacketType.RarNodeExecuteRequest:
                            CommunicationsUtilities.Trace("({0}) Executing RAR...", _endpointId);

                            RarNodeExecuteRequest request = (RarNodeExecuteRequest)packet;
                            ResolveAssemblyReference rarTask = new();
                            request.SetTaskInputs(rarTask, _buildEngine);

                            bool success = rarTask.Execute();

                            await _pipeServer.WritePacketAsync(new RarNodeExecuteResponse(rarTask, success), cancellationToken).ConfigureAwait(false);

                            CommunicationsUtilities.Trace("({0}) Completed RAR request.", _endpointId);
                            break;
                        case NodePacketType.NodeShutdown:
                            // Although the client has already disconnected, it is still necessary to Disconnect() so the
                            // pipe can transition into PipeState.Disconnected, which is treated as an intentional pipe break.
                            // Otherwise, all future operations on the pipe will throw an exception.
                            CommunicationsUtilities.Trace("({0}) RAR client disconnected.", _endpointId);
                            _pipeServer.Disconnect();
                            break;
                        default:
                            ErrorUtilities.ThrowInternalError($"Received unexpected packet type {packetType}");
                            break;
                    }
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    CommunicationsUtilities.Trace("({0}) Exception while executing RAR request: {1}", _endpointId, e);
                }
            }

            _pipeServer.Disconnect();
        }

        /// <summary>
        /// Configuration to reuse for all endpoints in a given RAR node process.
        /// </summary>
        internal readonly struct SharedConfig(
            string pipeName,
            ServerNodeHandshake handshake,
            NodePacketFactory packetFactory,
            int maxNumberOfServerInstances)
        {
            public string PipeName { get; } = pipeName;

            public ServerNodeHandshake Handshake { get; } = handshake;

            public NodePacketFactory PacketFactory { get; } = packetFactory;

            public int MaxNumberOfServerInstances { get; } = maxNumberOfServerInstances;
        }
    }
}
