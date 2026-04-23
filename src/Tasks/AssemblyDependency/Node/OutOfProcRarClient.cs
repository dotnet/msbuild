// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks.AssemblyDependency
{
    /// <summary>
    /// Implements a client for sending the ResolveAssemblyReference task to an out-of-proc node.
    /// This is intended to be reused for all RAR tasks across a single build.
    /// It manages a pool of pipe clients to the RAR node, allowing it to be used concurrently from multiple RAR tasks.
    /// </summary>
    internal sealed class OutOfProcRarClient : IDisposable
    {
        // Create a single cached instance for this build.
        internal const string TaskObjectCacheKey = "OutOfProcRarClient";

        private readonly Queue<NodePipeClient> _availablePipeClients = new();
        private readonly LockType _poolLock = new();
        private readonly ServerNodeHandshake _handshake;
        private readonly string _pipeName;
        private volatile bool _disposed = false;

        private OutOfProcRarClient()
        {
            _handshake = new(HandshakeOptions.None);
            _pipeName = NamedPipeUtil.GetRarNodeEndpointPipeName(_handshake);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
                
            lock (_poolLock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                
                // Dispose all pipe clients in the pool
                CommunicationsUtilities.Trace($"Disposing RAR client pool with {_availablePipeClients.Count} pipe clients.");
                while (_availablePipeClients.Count > 0)
                {
                    NodePipeClient pipeClient = _availablePipeClients.Dequeue();
                    pipeClient?.Dispose();
                }
            }
        }

        internal static OutOfProcRarClient GetInstance(IBuildEngine10 buildEngine)
        {
            // We want to reuse the pipe client pool across all RAR invocations within a build, but release 
            // all pipe clients once build is complete. Using RegisteredTaskObjectLifetime.Build ensures 
            // that the RAR client is disposed between builds, freeing all pipe clients.
            OutOfProcRarClient rarClient = (OutOfProcRarClient)buildEngine.GetRegisteredTaskObject(TaskObjectCacheKey, RegisteredTaskObjectLifetime.Build);

            if (rarClient == null)
            {
                rarClient = new OutOfProcRarClient();
                buildEngine.RegisterTaskObject(TaskObjectCacheKey, rarClient, RegisteredTaskObjectLifetime.Build, allowEarlyCollection: false);
                CommunicationsUtilities.Trace("Initialized new RAR client.");
            }

            return rarClient;
        }

        private NodePipeClient AcquireConnection()
        {      
            lock (_poolLock)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(OutOfProcRarClient));
                }
                
                // Try to get an existing pipe client from the pool
                if (_availablePipeClients.Count > 0)
                {
                    return _availablePipeClients.Dequeue();
                }
            }
            
            // No available pipe client, create a new one outside the lock
            NodePipeClient newPipeClient = CreateNewConnection();
            CommunicationsUtilities.Trace("Created new pipe client for RAR pool.");
            return newPipeClient;
        }
        
        private void ReleaseConnection(NodePipeClient pipeClient)
        {
            lock (_poolLock)
            {
                if (_disposed)
                {
                    // If the client is already disposed, dispose the pipe client instead of returning it to the pool.
                    CommunicationsUtilities.Trace("RAR client already disposed, disposing pipe client instead of returning to pool.");
                    pipeClient?.Dispose();
                    return;
                }
                
                _availablePipeClients.Enqueue(pipeClient);
            }
        }
        
        private NodePipeClient CreateNewConnection()
        {
            var pipeClient = new NodePipeClient(_pipeName, _handshake);
            
            NodePacketFactory packetFactory = new();
            packetFactory.RegisterPacketHandler(NodePacketType.RarNodeBufferedLogEvents, static t => new RarNodeBufferedLogEvents(t), null);
            packetFactory.RegisterPacketHandler(NodePacketType.RarNodeExecuteResponse, static t => new RarNodeExecuteResponse(t), null);
            pipeClient.RegisterPacketFactory(packetFactory);
            
            return pipeClient;
        }

        internal bool Execute(ResolveAssemblyReference rarTask)
        {
            // Acquire a pipe client from the pool
            NodePipeClient? pipeClient = null;
            
            try
            {
                // CA2000: Disposal is handled - ReleaseConnection() either returns 
                // the pipe client to the pool for reuse or disposes it if this client was already disposed.
                // At the end of the build the entire pool will be disposed, which will dispose all pipe clients.
#pragma warning disable CA2000
                pipeClient = AcquireConnection();
#pragma warning restore CA2000
                
                // Connect if not already connected
                if (!pipeClient.IsConnected)
                {
                    // Don't set a timeout since the build manager already blocks until the server is running.
                    if (!pipeClient.ConnectToServer(0))
                    {
                        return false;
                    }
                }

                pipeClient.WritePacket(new RarNodeExecuteRequest(rarTask));

                INodePacket packet = pipeClient.ReadPacket();

                while (packet.Type != NodePacketType.RarNodeExecuteResponse)
                {
                    if (packet.Type == NodePacketType.RarNodeBufferedLogEvents)
                    {
                        RarNodeBufferedLogEvents logEvents = (RarNodeBufferedLogEvents)packet;
                        foreach (LogMessagePacketBase logMessagePacket in logEvents.EventQueue)
                        {
                            BuildEventArgs buildEvent = logMessagePacket.NodeBuildEvent?.Value!;
                            switch (logMessagePacket.EventType)
                            {
                                case LoggingEventType.BuildErrorEvent:
                                    rarTask.BuildEngine.LogErrorEvent((BuildErrorEventArgs)buildEvent);
                                    break;
                                case LoggingEventType.BuildWarningEvent:
                                    rarTask.BuildEngine.LogWarningEvent((BuildWarningEventArgs)buildEvent);
                                    break;
                                case LoggingEventType.BuildMessageEvent:
                                    rarTask.BuildEngine.LogMessageEvent((BuildMessageEventArgs)buildEvent);
                                    break;
                                default:
                                    ErrorUtilities.ThrowInternalError($"Received unexpected log event type {logMessagePacket.Type}");
                                    break;
                            }
                        }
                    }
                    else
                    {
                        ErrorUtilities.ThrowInternalError($"Received unexpected packet type {packet.Type}");
                    }

                    packet = pipeClient.ReadPacket();
                }

                RarNodeExecuteResponse response = (RarNodeExecuteResponse)packet;
                response.SetTaskOutputs(rarTask);

                return response.Success;
            }
            finally
            {
                // Return the pipe client to the pool
                if (pipeClient != null)
                {
                    ReleaseConnection(pipeClient);
                }
            }
        }
    }
}
