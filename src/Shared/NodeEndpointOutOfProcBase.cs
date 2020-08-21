// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
#if CLR2COMPATIBILITY
using Microsoft.Build.Shared.Concurrent;
#else
using System.Collections.Concurrent;
#endif
using System.IO;
using System.IO.Pipes;
using System.Threading;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
#if FEATURE_SECURITY_PERMISSIONS || FEATURE_PIPE_SECURITY
using System.Security.AccessControl;
#endif
using System.Security.Principal;
#if !FEATURE_APM
using System.Threading.Tasks;
#endif
#if FEATURE_SECURITY_PERMISSIONS
using System.Security.Permissions;
#endif

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// This is an implementation of INodeEndpoint for the out-of-proc nodes.  It acts only as a client.
    /// </summary>
    internal abstract class NodeEndpointOutOfProcBase : INodeEndpoint
    {
        #region Private Data
        /// <summary>
        /// The amount of time to wait for the client to connect to the host.
        /// </summary>
        private const int ClientConnectTimeout = 60000;

        /// <summary>
        /// The size of the buffers to use for named pipes
        /// </summary>
        private const int PipeBufferSize = 131072;

        /// <summary>
        /// Flag indicating if we should debug communications or not.
        /// </summary>
        private bool _debugCommunications = false;

        /// <summary>
        /// The current communication status of the node.
        /// </summary>
        private LinkStatus _status;

        /// <summary>
        /// The pipe client used by the nodes.
        /// </summary>
        private NamedPipeServerStream _pipeServer;

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

        /// <summary>
        /// Per-node shared read buffer.
        /// </summary>
        private SharedReadBuffer _sharedReadBuffer;

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

#endregion

#region Construction

        /// <summary>
        /// Instantiates an endpoint to act as a client
        /// </summary>
        /// <param name="pipeName">The name of the pipe to which we should connect.</param>
        internal void InternalConstruct(string pipeName)
        {
            ErrorUtilities.VerifyThrowArgumentLength(pipeName, nameof(pipeName));

            _debugCommunications = (Environment.GetEnvironmentVariable("MSBUILDDEBUGCOMM") == "1");

            _status = LinkStatus.Inactive;
            _asyncDataMonitor = new object();
            _sharedReadBuffer = InterningBinaryReader.CreateSharedBuffer();
            _pipeServer = NamedPipeUtil.CreateNamedPipeServer(pipeName, PipeBufferSize, PipeBufferSize);
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
#if CLR2COMPATIBILITY
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
            NamedPipeServerStream localPipeServer = _pipeServer;

            AutoResetEvent localPacketAvailable = _packetAvailable;
            AutoResetEvent localTerminatePacketPump = _terminatePacketPump;
            ConcurrentQueue<INodePacket> localPacketQueue = _packetQueue;

            DateTime originalWaitStartTime = DateTime.UtcNow;
            bool gotValidConnection = false;
            while (!gotValidConnection)
            {
                gotValidConnection = true;
                DateTime restartWaitTime = DateTime.UtcNow;

                // We only wait to wait the difference between now and the last original start time, in case we have multiple hosts attempting
                // to attach.  This prevents each attempt from resetting the timer.
                TimeSpan usedWaitTime = restartWaitTime - originalWaitStartTime;
                int waitTimeRemaining = Math.Max(0, CommunicationsUtilities.NodeConnectionTimeout - (int)usedWaitTime.TotalMilliseconds);

                try
                {
                    // Wait for a connection
#if FEATURE_APM
                    IAsyncResult resultForConnection = localPipeServer.BeginWaitForConnection(null, null);
                    CommunicationsUtilities.Trace("Waiting for connection {0} ms...", waitTimeRemaining);
                    bool connected = resultForConnection.AsyncWaitHandle.WaitOne(waitTimeRemaining, false);
#else
                    Task connectionTask = localPipeServer.WaitForConnectionAsync();
                    CommunicationsUtilities.Trace("Waiting for connection {0} ms...", waitTimeRemaining);
                    bool connected = connectionTask.Wait(waitTimeRemaining);
#endif
                    if (!connected)
                    {
                        CommunicationsUtilities.Trace("Connection timed out waiting a host to contact us.  Exiting comm thread.");
                        ChangeLinkStatus(LinkStatus.ConnectionFailed);
                        return;
                    }

                    CommunicationsUtilities.Trace("Parent started connecting. Reading handshake from parent");
#if FEATURE_APM
                    localPipeServer.EndWaitForConnection(resultForConnection);
#endif


                    Handshake handshake = GetHandshake();
                    gotValidConnection = NamedPipeUtil.ValidateHandshake(handshake, _pipeServer, ClientConnectTimeout);
                    if (!gotValidConnection)
                    {
                        return;
                    }

                    ChangeLinkStatus(LinkStatus.Active);
                }
                catch (Exception e)
                {
                    if (ExceptionHandling.IsCriticalException(e))
                    {
                        throw;
                    }

                    CommunicationsUtilities.Trace("Client connection failed.  Exiting comm thread. {0}", e);
                    if (localPipeServer.IsConnected)
                    {
                        localPipeServer.Disconnect();
                    }

                    ExceptionHandling.DumpExceptionToFile(e);
                    ChangeLinkStatus(LinkStatus.Failed);
                    return;
                }
            }

            RunReadLoop(
                new BufferedReadStream(_pipeServer),
                _pipeServer,
                localPacketQueue, localPacketAvailable, localTerminatePacketPump);

            CommunicationsUtilities.Trace("Ending read loop");

            try
            {
                if (localPipeServer.IsConnected)
                {
                    localPipeServer.WaitForPipeDrain();
                    localPipeServer.Disconnect();
                }
            }
            catch (Exception)
            {
                // We don't really care if Disconnect somehow fails, but it gives us a chance to do the right thing.
            }
        }

        private void RunReadLoop(Stream localReadPipe, Stream localWritePipe,
            ConcurrentQueue<INodePacket> localPacketQueue, AutoResetEvent localPacketAvailable, AutoResetEvent localTerminatePacketPump)
        {
            // Ordering of the wait handles is important.  The first signalled wait handle in the array 
            // will be returned by WaitAny if multiple wait handles are signalled.  We prefer to have the
            // terminate event triggered so that we cannot get into a situation where packets are being
            // spammed to the endpoint and it never gets an opportunity to shutdown.
            CommunicationsUtilities.Trace("Entering read loop.");
            byte[] headerByte = new byte[5];
#if FEATURE_APM
            IAsyncResult result = localReadPipe.BeginRead(headerByte, 0, headerByte.Length, null, null);
#else
            Task<int> readTask = CommunicationsUtilities.ReadAsync(localReadPipe, headerByte, headerByte.Length);
#endif

            bool exitLoop = false;
            do
            {
                // Ordering is important.  We want packetAvailable to supercede terminate otherwise we will not properly wait for all
                // packets to be sent by other threads which are shutting down, such as the logging thread.
                WaitHandle[] handles = new WaitHandle[] {
#if FEATURE_APM
                    result.AsyncWaitHandle,
#else
                    ((IAsyncResult)readTask).AsyncWaitHandle,
#endif
                    localPacketAvailable, localTerminatePacketPump };

                int waitId = WaitHandle.WaitAny(handles);
                switch (waitId)
                {
                    case 0:
                        {
                            int bytesRead = 0;
                            try
                            {
#if FEATURE_APM
                                bytesRead = localReadPipe.EndRead(result);
#else
                                bytesRead = readTask.Result;
#endif
                            }
                            catch (Exception e)
                            {
                                // Lost communications.  Abort (but allow node reuse)
                                CommunicationsUtilities.Trace("Exception reading from server.  {0}", e);
                                ExceptionHandling.DumpExceptionToFile(e);
                                ChangeLinkStatus(LinkStatus.Inactive);
                                exitLoop = true;
                                break;
                            }

                            if (bytesRead != headerByte.Length)
                            {
                                // Incomplete read.  Abort.
                                if (bytesRead == 0)
                                {
                                    CommunicationsUtilities.Trace("Parent disconnected abruptly");
                                }
                                else
                                {
                                    CommunicationsUtilities.Trace("Incomplete header read from server.  {0} of {1} bytes read", bytesRead, headerByte.Length);
                                }

                                ChangeLinkStatus(LinkStatus.Failed);
                                exitLoop = true;
                                break;
                            }

                            NodePacketType packetType = (NodePacketType)Enum.ToObject(typeof(NodePacketType), headerByte[0]);

                            try
                            {
                                _packetFactory.DeserializeAndRoutePacket(0, packetType, BinaryTranslator.GetReadTranslator(localReadPipe, _sharedReadBuffer));
                            }
                            catch (Exception e)
                            {
                                // Error while deserializing or handling packet.  Abort.
                                CommunicationsUtilities.Trace("Exception while deserializing packet {0}: {1}", packetType, e);
                                ExceptionHandling.DumpExceptionToFile(e);
                                ChangeLinkStatus(LinkStatus.Failed);
                                exitLoop = true;
                                break;
                            }

#if FEATURE_APM
                            result = localReadPipe.BeginRead(headerByte, 0, headerByte.Length, null, null);
#else
                            readTask = CommunicationsUtilities.ReadAsync(localReadPipe, headerByte, headerByte.Length);
#endif
                        }

                        break;

                    case 1:
                    case 2:
                        try
                        {
                            // Write out all the queued packets.
                            INodePacket packet;
                            while (localPacketQueue.TryDequeue(out packet))
                            {
                                MemoryStream packetStream = new MemoryStream();
                                ITranslator writeTranslator = BinaryTranslator.GetWriteTranslator(packetStream);

                                packetStream.WriteByte((byte)packet.Type);

                                // Pad for packet length
                                packetStream.Write(BitConverter.GetBytes((int)0), 0, 4);

                                // Reset the position in the write buffer.
                                packet.Translate(writeTranslator);

                                // Now write in the actual packet length
                                packetStream.Position = 1;
                                packetStream.Write(BitConverter.GetBytes((int)packetStream.Length - 5), 0, 4);

                                localWritePipe.Write(packetStream.GetBuffer(), 0, (int)packetStream.Length);
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
