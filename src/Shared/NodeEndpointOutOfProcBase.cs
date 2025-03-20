// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
#if TASKHOST
using Microsoft.Build.Shared.Concurrent;
#else
using System.Collections.Concurrent;
using System.Threading.Tasks;
#endif
using System.IO;
using System.Threading;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// This is an implementation of INodeEndpoint for the out-of-proc nodes.  It acts only as a client.
    /// </summary>
    [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "It is expected to keep the stream open for the process lifetime")]
    internal abstract class NodeEndpointOutOfProcBase : INodeEndpoint
    {
        #region Private Data

        /// <summary>
        /// The current communication status of the node.
        /// </summary>
        private LinkStatus _status;

        /// <summary>
        /// The pipe client used by the nodes.
        /// </summary>
        private NodePipeServer _pipeServer;

        // The following private data fields are used only when the endpoint is in ASYNCHRONOUS mode.

        /// <summary>
        /// Object used as a lock source for the async data
        /// </summary>
        private object _asyncDataMonitor;

        /// <summary>
        /// Set when a packet is available in the packet queue
        /// </summary>
        private AutoResetEvent _packetAvailable;

        /// <summary>
        /// Set when the asynchronous packet pump should terminate
        /// </summary>
        private AutoResetEvent _terminatePacketPump;

        /// <summary>
        /// True if this side is gracefully disconnecting.
        /// In such case we have sent last packet to client side and we expect
        /// client will soon broke pipe connection - unless server do it first.
        /// </summary>
        private bool _isClientDisconnecting;

        /// <summary>
        /// The thread which runs the asynchronous packet pump
        /// </summary>
        private Thread _packetPump;

        /// <summary>
        /// The factory used to create and route packets.
        /// </summary>
        private INodePacketFactory _packetFactory;

        /// <summary>
        /// The asynchronous packet queue.
        /// </summary>
        /// <remarks>
        /// Operations on this queue must be synchronized since it is accessible by multiple threads.
        /// Use a lock on the packetQueue itself.
        /// </remarks>
        private ConcurrentQueue<INodePacket> _packetQueue;

        #endregion

        #region INodeEndpoint Events

        /// <summary>
        /// Raised when the link status has changed.
        /// </summary>
        public event LinkStatusChangedDelegate OnLinkStatusChanged;

        #endregion

        #region INodeEndpoint Properties

        /// <summary>
        /// Returns the link status of this node.
        /// </summary>
        public LinkStatus LinkStatus
        {
            get { return _status; }
        }

        #endregion

        #region Properties

        #endregion

        #region INodeEndpoint Methods

        /// <summary>
        /// Causes this endpoint to wait for the remote endpoint to connect
        /// </summary>
        /// <param name="factory">The factory used to create packets.</param>
        public void Listen(INodePacketFactory factory)
        {
            ErrorUtilities.VerifyThrow(_status == LinkStatus.Inactive, "Link not inactive.  Status is {0}", _status);
            ErrorUtilities.VerifyThrowArgumentNull(factory, nameof(factory));
            _packetFactory = factory;
            _pipeServer.RegisterPacketFactory(factory);

            InitializeAsyncPacketThread();
        }

        /// <summary>
        /// Causes this node to connect to the matched endpoint.
        /// </summary>
        /// <param name="factory">The factory used to create packets.</param>
        public void Connect(INodePacketFactory factory)
        {
            ErrorUtilities.ThrowInternalError("Connect() not valid on the out of proc endpoint.");
        }

        /// <summary>
        /// Shuts down the link
        /// </summary>
        public void Disconnect()
        {
            InternalDisconnect();
        }

        /// <summary>
        /// Sends data to the peer endpoint.
        /// </summary>
        /// <param name="packet">The packet to send.</param>
        public void SendData(INodePacket packet)
        {
            // PERF: Set up a priority system so logging packets are sent only when all other packet types have been sent.
            if (_status == LinkStatus.Active)
            {
                EnqueuePacket(packet);
            }
        }

        /// <summary>
        /// Called when we are about to send last packet to finalize graceful disconnection with client.
        /// </summary>
        public void ClientWillDisconnect()
        {
            _isClientDisconnecting = true;
        }

        #endregion

        #region Construction

        /// <summary>
        /// Instantiates an endpoint to act as a client
        /// </summary>
        internal void InternalConstruct(string pipeName = null)
        {
            _status = LinkStatus.Inactive;
            _asyncDataMonitor = new object();

            pipeName ??= NamedPipeUtil.GetPlatformSpecificPipeName();
            _pipeServer = new NodePipeServer(pipeName, GetHandshake());
        }

        #endregion

        /// <summary>
        /// Returns the host handshake for this node endpoint
        /// </summary>
        protected abstract Handshake GetHandshake();

        /// <summary>
        /// Updates the current link status if it has changed and notifies any registered delegates.
        /// </summary>
        /// <param name="newStatus">The status the node should now be in.</param>
        protected void ChangeLinkStatus(LinkStatus newStatus)
        {
            ErrorUtilities.VerifyThrow(_status != newStatus, "Attempting to change status to existing status {0}.", _status);
            CommunicationsUtilities.Trace("Changing link status from {0} to {1}", _status.ToString(), newStatus.ToString());
            _status = newStatus;
            RaiseLinkStatusChanged(_status);
        }

        /// <summary>
        /// Invokes the OnLinkStatusChanged event in a thread-safe manner.
        /// </summary>
        /// <param name="newStatus">The new status of the endpoint link.</param>
        private void RaiseLinkStatusChanged(LinkStatus newStatus)
        {
            OnLinkStatusChanged?.Invoke(this, newStatus);
        }

        #region Private Methods

        /// <summary>
        /// This does the actual work of changing the status and shutting down any threads we may have for
        /// disconnection.
        /// </summary>
        private void InternalDisconnect()
        {
            ErrorUtilities.VerifyThrow(_packetPump.ManagedThreadId != Thread.CurrentThread.ManagedThreadId, "Can't join on the same thread.");
            _terminatePacketPump.Set();
            _packetPump.Join();
#if TASKHOST
            _terminatePacketPump.Close();
#else
            _terminatePacketPump.Dispose();
#endif
            _pipeServer.Dispose();
            _packetPump = null;
            ChangeLinkStatus(LinkStatus.Inactive);
        }

        #region Asynchronous Mode Methods

        /// <summary>
        /// Adds a packet to the packet queue when asynchronous mode is enabled.
        /// </summary>
        /// <param name="packet">The packet to be transmitted.</param>
        private void EnqueuePacket(INodePacket packet)
        {
            ErrorUtilities.VerifyThrowArgumentNull(packet, nameof(packet));
            ErrorUtilities.VerifyThrow(_packetQueue != null, "packetQueue is null");
            ErrorUtilities.VerifyThrow(_packetAvailable != null, "packetAvailable is null");
            _packetQueue.Enqueue(packet);
            _packetAvailable.Set();
        }

        /// <summary>
        /// Initializes the packet pump thread and the supporting events as well as the packet queue.
        /// </summary>
        private void InitializeAsyncPacketThread()
        {
            lock (_asyncDataMonitor)
            {
                _isClientDisconnecting = false;
                _packetPump = new Thread(PacketPumpProc);
                _packetPump.IsBackground = true;
                _packetPump.Name = "OutOfProc Endpoint Packet Pump";
                _packetAvailable = new AutoResetEvent(false);
                _terminatePacketPump = new AutoResetEvent(false);
                _packetQueue = new ConcurrentQueue<INodePacket>();
                _packetPump.Start();
            }
        }

        /// <summary>
        /// This method handles the asynchronous message pump.  It waits for messages to show up on the queue
        /// and calls FireDataAvailable for each such packet.  It will terminate when the terminate event is
        /// set.
        /// </summary>
        private void PacketPumpProc()
        {
            NodePipeServer localPipeServer = _pipeServer;

            AutoResetEvent localPacketAvailable = _packetAvailable;
            AutoResetEvent localTerminatePacketPump = _terminatePacketPump;
            ConcurrentQueue<INodePacket> localPacketQueue = _packetQueue;

            ChangeLinkStatus(localPipeServer.WaitForConnection());
            if (_status != LinkStatus.Active)
            {
                return;
            }

            RunReadLoop(localPipeServer, localPacketQueue, localPacketAvailable, localTerminatePacketPump);

            CommunicationsUtilities.Trace("Ending read loop");
            localPipeServer.Disconnect();
        }

        private void RunReadLoop(NodePipeServer localPipeServer,
            ConcurrentQueue<INodePacket> localPacketQueue, AutoResetEvent localPacketAvailable, AutoResetEvent localTerminatePacketPump)
        {
            // Ordering of the wait handles is important.  The first signalled wait handle in the array
            // will be returned by WaitAny if multiple wait handles are signalled.  We prefer to have the
            // terminate event triggered so that we cannot get into a situation where packets are being
            // spammed to the endpoint and it never gets an opportunity to shutdown.
            CommunicationsUtilities.Trace("Entering read loop.");
#if TASKHOST
            Func<INodePacket> readPacketFunc = localPipeServer.ReadPacket;
            IAsyncResult result = readPacketFunc.BeginInvoke(null, null);
#else
            Task<INodePacket> readTask = localPipeServer.ReadPacketAsync();
#endif

            // Ordering is important.  We want packetAvailable to supercede terminate otherwise we will not properly wait for all
            // packets to be sent by other threads which are shutting down, such as the logging thread.
            WaitHandle[] handles = new WaitHandle[]
            {
#if NET451_OR_GREATER || NETCOREAPP
                ((IAsyncResult)readTask).AsyncWaitHandle,
#else
                result.AsyncWaitHandle,
#endif
                localPacketAvailable,
                localTerminatePacketPump,
            };

            bool exitLoop = false;
            do
            {
                int waitId = WaitHandle.WaitAny(handles);
                switch (waitId)
                {
                    case 0:
                        {
                            INodePacket packet = null;

                            try
                            {
#if TASKHOST
                                packet = readPacketFunc.EndInvoke(result);
#else
                                packet = readTask.GetAwaiter().GetResult();
#endif
                                if (packet.Type == NodePacketType.NodeShutdown)
                                {
                                    if (_isClientDisconnecting)
                                    {
                                        // Lost communications.  Abort (but allow node reuse).
                                        // Do not change link status to failed as this could make node think connection has failed
                                        // and recycle node, while this is perfectly expected and handled race condition
                                        // (both client and node is about to close pipe and client can be faster).
                                        CommunicationsUtilities.Trace("Parent disconnected gracefully.");
                                        ChangeLinkStatus(LinkStatus.Inactive);
                                    }
                                    else
                                    {
                                        CommunicationsUtilities.Trace("Parent disconnected abruptly.");
                                        ChangeLinkStatus(LinkStatus.Failed);
                                    }
                                }
                                else
                                {
                                    _packetFactory.RoutePacket(0, packet);
                                }
                            }
                            catch (Exception e)
                            {
                                if (packet == null)
                                {
                                    CommunicationsUtilities.Trace("Exception while reading packet from server:  {0}", e);
                                }
                                else
                                {
                                    CommunicationsUtilities.Trace("Exception while deserializing or handling packet {0}: {1}", packet.Type, e);
                                }

                                ExceptionHandling.DumpExceptionToFile(e);
                                ChangeLinkStatus(LinkStatus.Failed);
                            }

                            exitLoop = _status != LinkStatus.Active;
                            if (!exitLoop)
                            {
#if TASKHOST
                                result = readPacketFunc.BeginInvoke(null, null);
                                handles[0] = result.AsyncWaitHandle;
#else
                                readTask = localPipeServer.ReadPacketAsync();
                                handles[0] = ((IAsyncResult)readTask).AsyncWaitHandle;
#endif
                            }
                        }

                        break;

                    case 1:
                    case 2:
                        try
                        {
                            // Write out all the queued packets.
                            while (localPacketQueue.TryDequeue(out INodePacket packet))
                            {
                                localPipeServer.WritePacket(packet);
                            }
                        }
                        catch (Exception e)
                        {
                            // Error while deserializing or handling packet.  Abort.
                            CommunicationsUtilities.Trace("Exception while serializing packets: {0}", e);
                            ExceptionHandling.DumpExceptionToFile(e);
                            ChangeLinkStatus(LinkStatus.Failed);
                            exitLoop = true;
                            break;
                        }

                        if (waitId == 2)
                        {
                            CommunicationsUtilities.Trace("Disconnecting voluntarily");
                            ChangeLinkStatus(LinkStatus.Failed);
                            exitLoop = true;
                        }

                        break;

                    default:
                        ErrorUtilities.ThrowInternalError("waitId {0} out of range.", waitId);
                        break;
                }
            }
            while (!exitLoop);
        }

        #endregion

        #endregion
    }
}
