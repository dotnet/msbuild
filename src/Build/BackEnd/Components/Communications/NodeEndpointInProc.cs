// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Build.Shared;

using BuildParameters = Microsoft.Build.Execution.BuildParameters;
using System.Globalization;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// This is an implementation of INodeEndpoint for in-proc nodes.  This endpoint can use either
    /// synchronous or asynchronous packet processing methods.  When synchronous processing methods are
    /// used, the SendData method will cause the OnDataReceived event on the receiving endpoint to be called
    /// on the same thread, blocking until the handler returns.  The asynchronous method more closely emulates
    /// the way other kinds of endpoints work, as the recipient processes the packet on a different thread
    /// than that from which the packet originated, but with the cost of the extra thread.
    /// </summary>
    internal class NodeEndpointInProc : INodeEndpoint
    {
        #region Private Data
        /// <summary>
        /// An object for the two inproc endpoints to synchronize on.
        /// </summary>
        private static Object s_locker = new Object();

        /// <summary>
        /// The current communication status of the node.
        /// </summary>
        private LinkStatus _status;

        /// <summary>
        /// The communications mode
        /// </summary>
        private EndpointMode _mode;

        /// <summary>
        /// The peer endpoint
        /// </summary>
        private NodeEndpointInProc _peerEndpoint;

        /// <summary>
        /// The build component host
        /// </summary>
        private IBuildComponentHost _componentHost;

        /// <summary>
        /// The packet factory used to route packets.
        /// </summary>
        private INodePacketFactory _packetFactory;

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
        /// The thread which runs the asynchronous packet pump
        /// </summary>
        private Thread _packetPump;

        /// <summary>
        /// Set to true if our peer is connected to us.
        /// </summary>
        private bool _peerConnected;

        /// <summary>
        /// The asynchronous packet queue.  
        /// </summary>
        /// <remarks>
        /// Operations on this queue must be synchronized since it is accessible by multiple threads.
        /// Use a lock on the packetQueue itself.
        /// </remarks>
        private ConcurrentQueue<INodePacket> _packetQueue;
        #endregion

        #region Constructors and Factories
        /// <summary>
        /// Instantiates a Node and initializes it to unconnected.
        /// </summary>
        /// <param name="commMode">The communications mode for this endpoint.</param>
        /// <param name="host">The component host.</param>
        private NodeEndpointInProc(EndpointMode commMode, IBuildComponentHost host)
        {
            ErrorUtilities.VerifyThrowArgumentNull(host, nameof(host));

            _status = LinkStatus.Inactive;
            _mode = commMode;
            _componentHost = host;

            if (commMode == EndpointMode.Asynchronous)
            {
                _asyncDataMonitor = new object();
            }
        }

        #endregion

        #region INodeEndpoint Events

        /// <summary>
        /// Raised when the link status has changed.
        /// </summary>
        public event LinkStatusChangedDelegate OnLinkStatusChanged;

        #endregion

        #region Public Types and Enums
        /// <summary>
        /// Represents the style of communications used by the in-proc endpoint.
        /// </summary>
        internal enum EndpointMode
        {
            /// <summary>
            /// The DataReceived event is raised on the same thread as that which called SendData.
            /// </summary>
            Synchronous,

            /// <summary>
            /// The DataReceived event is raised on a separate thread from that which called SendData
            /// </summary>
            Asynchronous
        }

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

        #region INodeEndpoint Methods
        /// <summary>
        /// Causes this endpoint to wait for the remote endpoint to connect
        /// </summary>
        /// <param name="factory">Unused</param>
        public void Listen(INodePacketFactory factory)
        {
            ErrorUtilities.VerifyThrowInternalNull(factory, nameof(factory));
            _packetFactory = factory;

            // Initialize our thread in async mode so we are ready when the Node-side endpoint "connects".
            if (_mode == EndpointMode.Asynchronous)
            {
                InitializeAsyncPacketThread();
            }

            _peerEndpoint.SetPeerNodeConnected();
        }

        /// <summary>
        /// Causes this node to connect to the matched endpoint.  
        /// </summary>
        /// <param name="factory">Unused</param>
        public void Connect(INodePacketFactory factory)
        {
            ErrorUtilities.VerifyThrowInternalNull(factory, nameof(factory));
            _packetFactory = factory;

            // Set up asynchronous packet pump, if necessary.
            if (_mode == EndpointMode.Asynchronous)
            {
                InitializeAsyncPacketThread();
            }

            // Notify the Build Manager-side endpoint that the connection is now active.
            _peerEndpoint.SetPeerNodeConnected();
        }

        /// <summary>
        /// Shuts down the link
        /// </summary>
        public void Disconnect()
        {
            InternalDisconnect();

            // Notify the remote endpoint that the link is dead
            _peerEndpoint.SetPeerNodeDisconnected();
        }

        /// <summary>
        /// Sends data to the peer endpoint.
        /// </summary>
        /// <param name="packet">The packet to send.</param>
        public void SendData(INodePacket packet)
        {
            ErrorUtilities.VerifyThrow(_status == LinkStatus.Active, "Cannot send when link status is not active. Current status {0}", _status);

            if (_mode == EndpointMode.Synchronous)
            {
                _peerEndpoint._packetFactory.RoutePacket(0, packet);
            }
            else
            {
                EnqueuePacket(packet);
            }
        }
        #endregion

        #region Internal Methods
        /// <summary>
        /// This method is used to create a matched pair of endpoints used by the Node Provider and
        /// the Node.  The inputs and outputs for each node are automatically configured.
        /// </summary>
        /// <param name="mode">The communications mode for the endpoints.</param>
        /// <param name="host">The component host.</param>
        /// <returns>A matched pair of endpoints.</returns>
        internal static EndpointPair CreateInProcEndpoints(EndpointMode mode, IBuildComponentHost host)
        {
            NodeEndpointInProc node = new NodeEndpointInProc(mode, host);
            NodeEndpointInProc manager = new NodeEndpointInProc(mode, host);

            // NOTE: This creates a circular reference which must be explicitly broken before these
            // objects can be reclaimed by the garbage collector.
            node._peerEndpoint = manager;
            manager._peerEndpoint = node;

            return new EndpointPair(node, manager);
        }
        #endregion

        #region Private Event Methods

        /// <summary>
        /// Invokes the OnLinkStatusChanged event in a thread-safe manner.
        /// </summary>
        /// <param name="newStatus">The new status of the endpoint link.</param>
        private void RaiseLinkStatusChanged(LinkStatus newStatus)
        {
            if (OnLinkStatusChanged != null)
            {
                LinkStatusChangedDelegate linkStatusDelegate = OnLinkStatusChanged;
                linkStatusDelegate(this, newStatus);
            }
        }

        #endregion 

        #region Private Methods

        /// <summary>
        /// This method is called by the other endpoint when it is ready to establish the connection.
        /// </summary>
        private void SetPeerNodeConnected()
        {
            lock (s_locker)
            {
                _peerConnected = true;
                if (_peerEndpoint._peerConnected)
                {
                    ChangeLinkStatus(LinkStatus.Active);
                    _peerEndpoint.ChangeLinkStatus(LinkStatus.Active);
                }
            }
        }

        /// <summary>
        /// This method is called by either side to notify this endpoint that the link is inactive.
        /// </summary>
        private void SetPeerNodeDisconnected()
        {
            _peerConnected = false;
            InternalDisconnect();
        }

        /// <summary>
        /// This does the actual work of changing the status and shutting down any threads we may have for
        /// disconnection.
        /// </summary>
        private void InternalDisconnect()
        {
            ErrorUtilities.VerifyThrow(_status == LinkStatus.Active, "Endpoint is not connected. Current status {0}", _status);

            ChangeLinkStatus(LinkStatus.Inactive);

            // Terminate our thread if we were in async mode
            if (_mode == EndpointMode.Asynchronous)
            {
                TerminateAsyncPacketThread();
            }
        }

        /// <summary>
        /// Updates the current link status if it has changed and notifies any registered delegates.
        /// </summary>
        /// <param name="newStatus">The status the node should now be in.</param>
        private void ChangeLinkStatus(LinkStatus newStatus)
        {
            ErrorUtilities.VerifyThrow(_status != newStatus, "Attempting to change status to existing status {0}.", _status);
            _status = newStatus;
            RaiseLinkStatusChanged(_status);
        }

        #region Asynchronous Mode Methods

        /// <summary>
        /// Adds a packet to the packet queue when asynchronous mode is enabled.
        /// </summary>
        /// <param name="packet">The packet to be transmitted.</param>
        private void EnqueuePacket(INodePacket packet)
        {
            ErrorUtilities.VerifyThrowArgumentNull(packet, nameof(packet));
            ErrorUtilities.VerifyThrow(_mode == EndpointMode.Asynchronous, "EndPoint mode is synchronous, should be asynchronous");
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
                ErrorUtilities.VerifyThrow(_packetPump == null, "packetPump != null");
                ErrorUtilities.VerifyThrow(_packetAvailable == null, "packetAvailable != null");
                ErrorUtilities.VerifyThrow(_terminatePacketPump == null, "terminatePacketPump != null");
                ErrorUtilities.VerifyThrow(_packetQueue == null, "packetQueue != null");

#if FEATURE_THREAD_CULTURE
                _packetPump = new Thread(PacketPumpProc);
#else
                //  In .NET Core, we need to set the current culture from inside the new thread
                CultureInfo culture = _componentHost.BuildParameters.Culture;
                CultureInfo uiCulture = _componentHost.BuildParameters.UICulture;
                _packetPump = new Thread(() =>
                {
                    CultureInfo.CurrentCulture = culture;
                    CultureInfo.CurrentUICulture = uiCulture;
                    PacketPumpProc();
                });
#endif
                _packetPump.Name = "InProc Endpoint Packet Pump";
                _packetAvailable = new AutoResetEvent(false);
                _terminatePacketPump = new AutoResetEvent(false);
                _packetQueue = new ConcurrentQueue<INodePacket>();
#if FEATURE_THREAD_CULTURE
                _packetPump.CurrentCulture = _componentHost.BuildParameters.Culture;
                _packetPump.CurrentUICulture = _componentHost.BuildParameters.UICulture;
#endif
                _packetPump.Start();
            }
        }

        /// <summary>
        /// Shuts down the packet pump thread and cleans up associated data.
        /// </summary>
        private void TerminateAsyncPacketThread()
        {
            lock (_asyncDataMonitor)
            {
                ErrorUtilities.VerifyThrow(_packetPump != null, "packetPump == null");
                ErrorUtilities.VerifyThrow(_packetAvailable != null, "packetAvailable == null");
                ErrorUtilities.VerifyThrow(_terminatePacketPump != null, "terminatePacketPump == null");
                ErrorUtilities.VerifyThrow(_packetQueue != null, "packetQueue == null");

                _terminatePacketPump.Set();
                if (!_packetPump.Join((int)new TimeSpan(0, 0, BuildParameters.EndpointShutdownTimeout).TotalMilliseconds))
                {
#if FEATURE_THREAD_ABORT
                    // We timed out.  Kill it.
                    _packetPump.Abort();
#endif
                }

                _packetPump = null;
                _packetAvailable.Dispose();
                _packetAvailable = null;
                _terminatePacketPump.Dispose();
                _terminatePacketPump = null;
                _packetQueue = null;
            }
        }

        /// <summary>
        /// This method handles the asynchronous message pump.  It waits for messages to show up on the queue
        /// and calls FireDataAvailable for each such packet.  It will terminate when the terminate event is
        /// set.
        /// </summary>
        private void PacketPumpProc()
        {
            try
            {
                // Ordering of the wait handles is important.  The first signalled wait handle in the array 
                // will be returned by WaitAny if multiple wait handles are signalled.  We prefer to have the
                // terminate event triggered so that we cannot get into a situation where packets are being
                // spammed to the endpoint and it never gets an opportunity to shutdown.
                WaitHandle[] handles = new WaitHandle[] { _terminatePacketPump, _packetAvailable };

                bool exitLoop = false;
                do
                {
                    int waitId = WaitHandle.WaitAny(handles);
                    switch (waitId)
                    {
                        case 0:
                            exitLoop = true;
                            break;
                        case 1:
                            {
                                INodePacket packet;
                                while (_packetQueue.TryDequeue(out packet))
                                {
                                    _peerEndpoint._packetFactory.RoutePacket(0, packet);
                                }
                            }

                            break;

                        default:
                            ErrorUtilities.ThrowInternalError("waitId {0} out of range.", waitId);
                            break;
                    }
                }
                while (!exitLoop);
            }
            catch (Exception e)
            {
                // Dump all engine exceptions to a temp file
                // so that we have something to go on in the
                // event of a failure
                ExceptionHandling.DumpExceptionToFile(e);
                throw;
            }
        }

        #endregion

        #endregion

        #region Structs
        /// <summary>
        /// Used to return a matched pair of endpoints for in-proc nodes to use with the Build Manager.
        /// </summary>
        internal struct EndpointPair
        {
            /// <summary>
            /// The endpoint destined for use by a node.
            /// </summary>
            internal readonly NodeEndpointInProc NodeEndpoint;

            /// <summary>
            /// The endpoint destined for use by the Build Manager
            /// </summary>
            internal readonly NodeEndpointInProc ManagerEndpoint;

            /// <summary>
            /// Creates an endpoint pair
            /// </summary>
            /// <param name="node">The node-side endpoint.</param>
            /// <param name="manager">The manager-side endpoint.</param>
            internal EndpointPair(NodeEndpointInProc node, NodeEndpointInProc manager)
            {
                NodeEndpoint = node;
                ManagerEndpoint = manager;
            }
        }
        #endregion
    }
}
