// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using Microsoft.Build.TaskHost.Collections;
using Microsoft.Build.TaskHost.Utilities;

#nullable disable

namespace Microsoft.Build.TaskHost.BackEnd
{
    /// <summary>
    /// This is an implementation of INodeEndpoint for the out-of-proc nodes.  It acts only as a client.
    /// </summary>
    [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "It is expected to keep the stream open for the process lifetime")]
    internal sealed class NodeEndpointOutOfProcTaskHost : INodeEndpoint
    {
        #region Private Data

        /// <summary>
        /// The size of the buffers to use for named pipes
        /// </summary>
        private const int PipeBufferSize = 131072;

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

        /// <summary>
        /// Per-node shared read buffer.
        /// </summary>
        private BinaryReaderFactory _sharedReadBuffer;

        /// <summary>
        /// A way to cache a byte array when writing out packets
        /// </summary>
        private MemoryStream _packetStream;

        /// <summary>
        /// A binary writer to help write into <see cref="_packetStream"/>
        /// </summary>
        private BinaryWriter _binaryWriter;

        /// <summary>
        /// Represents the version of the parent packet associated with the node instantiation.
        /// </summary>
        private byte _parentPacketVersion;

        #endregion

        public NodeEndpointOutOfProcTaskHost(byte parentPacketVersion)
        {
            _status = LinkStatus.Inactive;
            _asyncDataMonitor = new object();
            _sharedReadBuffer = InterningBinaryReader.CreateSharedBuffer();

            _packetStream = new MemoryStream();
            _binaryWriter = new BinaryWriter(_packetStream);
            _parentPacketVersion = parentPacketVersion;

            string pipeName = $"MSBuild{EnvironmentUtilities.CurrentProcessId}";

            SecurityIdentifier identifier = WindowsIdentity.GetCurrent().Owner;
            PipeSecurity security = new PipeSecurity();

            // Restrict access to just this account.  We set the owner specifically here, and on the
            // pipe client side they will check the owner against this one - they must have identical
            // SIDs or the client will reject this server.  This is used to avoid attacks where a
            // hacked server creates a less restricted pipe in an attempt to lure us into using it and
            // then sending build requests to the real pipe client (which is the MSBuild Build Manager.)
            PipeAccessRule rule = new PipeAccessRule(identifier, PipeAccessRights.ReadWrite, AccessControlType.Allow);
            security.AddAccessRule(rule);
            security.SetOwner(identifier);

            _pipeServer = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1, // Only allow one connection at a time.
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.WriteThrough,
                PipeBufferSize, // Default input buffer
                PipeBufferSize,  // Default output buffer
                security,
                HandleInheritability.None);
        }

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

        /// <summary>
        /// Called when we are about to send last packet to finalize graceful disconnection with client.
        /// </summary>
        public void ClientWillDisconnect()
        {
            _isClientDisconnecting = true;
        }

        #endregion

        /// <summary>
        /// Updates the current link status if it has changed and notifies any registered delegates.
        /// </summary>
        /// <param name="newStatus">The status the node should now be in.</param>
        private void ChangeLinkStatus(LinkStatus newStatus)
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
            _terminatePacketPump.Close();
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
                    IAsyncResult resultForConnection = localPipeServer.BeginWaitForConnection(null, null);
                    CommunicationsUtilities.Trace("Waiting for connection {0} ms...", waitTimeRemaining);
                    bool connected = resultForConnection.AsyncWaitHandle.WaitOne(waitTimeRemaining, false);
                    if (!connected)
                    {
                        CommunicationsUtilities.Trace("Connection timed out waiting a host to contact us.  Exiting comm thread.");
                        ChangeLinkStatus(LinkStatus.ConnectionFailed);
                        return;
                    }

                    CommunicationsUtilities.Trace("Parent started connecting. Reading handshake from parent");
                    localPipeServer.EndWaitForConnection(resultForConnection);

                    // The handshake protocol is a series of int exchanges.  The host sends us a each component, and we
                    // verify it. Afterwards, the host sends an "End of Handshake" signal, to which we respond in kind.
                    // Once the handshake is complete, both sides can be assured the other is ready to accept data.
                    Handshake handshake = new(CommunicationsUtilities.GetHandshakeOptions());
                    try
                    {
                        HandshakeComponents handshakeComponents = handshake.RetrieveHandshakeComponents();

                        int index = 0;
                        foreach (var component in handshakeComponents.EnumerateComponents())
                        {

                            if (!_pipeServer.TryReadIntForHandshake(
                                byteToAccept: index == 0 ? (byte?)CommunicationsUtilities.HandshakeVersion : null, /* this will disconnect a < 16.8 host; it expects leading 00 or F5 or 06. 0x00 is a wildcard */
                              out HandshakeResult result))
                            {
                                CommunicationsUtilities.Trace($"Handshake failed with error: {result.ErrorMessage}");
                            }

                            if (!IsHandshakePartValid(component, result.Value, index))
                            {
                                CommunicationsUtilities.Trace(
                                        "Handshake failed. Received {0} from host  for {1} but expected {2}. Probably the host is a different MSBuild build.",
                                        result.Value,
                                        component.Key,
                                        component.Value);
                                _pipeServer.WriteIntForHandshake(index + 1);
                                gotValidConnection = false;
                                break;
                            }

                            index++;
                        }

                        if (gotValidConnection)
                        {
                            // To ensure that our handshake and theirs have the same number of bytes, receive and send a magic number indicating EOS.
                            if (_pipeServer.TryReadEndOfHandshakeSignal(false, out HandshakeResult _))
                            {
                                // Send supported PacketVersion after EndOfHandshakeSignal
                                // Based on this parent node decides how to communicate with the child.
                                if (_parentPacketVersion >= 2)
                                {
                                    _pipeServer.WriteIntForHandshake(Handshake.PacketVersionFromChildMarker);  // Marker: PacketVersion follows
                                    _pipeServer.WriteIntForHandshake(NodePacketTypeExtensions.PacketVersion);
                                    CommunicationsUtilities.Trace("Sent PacketVersion: {0}", NodePacketTypeExtensions.PacketVersion);
                                }

                                CommunicationsUtilities.Trace("Successfully connected to parent.");
                                _pipeServer.WriteEndOfHandshakeSignal();

                                // We will only talk to a host that was started by the same user as us.  Even though the pipe access is set to only allow this user, we want to ensure they
                                // haven't attempted to change those permissions out from under us.  This ensures that the only way they can truly gain access is to be impersonating the
                                // user we were started by.
                                WindowsIdentity currentIdentity = WindowsIdentity.GetCurrent();
                                WindowsIdentity clientIdentity = null;
                                localPipeServer.RunAsClient(delegate () { clientIdentity = WindowsIdentity.GetCurrent(true); });

                                if (clientIdentity == null || !String.Equals(clientIdentity.Name, currentIdentity.Name, StringComparison.OrdinalIgnoreCase))
                                {
                                    CommunicationsUtilities.Trace("Handshake failed. Host user is {0} but we were created by {1}.", (clientIdentity == null) ? "<unknown>" : clientIdentity.Name, currentIdentity.Name);
                                    gotValidConnection = false;
                                    continue;
                                }
                            }
                        }
                    }
                    catch (IOException e)
                    {
                        // We will get here when:
                        // 1. The host (OOP main node) connects to us, it immediately checks for user privileges
                        //    and if they don't match it disconnects immediately leaving us still trying to read the blank handshake
                        // 2. The host is too old sending us bits we automatically reject in the handshake
                        // 3. We expected to read the EndOfHandshake signal, but we received something else
                        CommunicationsUtilities.Trace("Client connection failed but we will wait for another connection. Exception: {0}", e.Message);

                        gotValidConnection = false;
                    }
                    catch (InvalidOperationException)
                    {
                        gotValidConnection = false;
                    }

                    if (!gotValidConnection)
                    {
                        if (localPipeServer.IsConnected)
                        {
                            localPipeServer.Disconnect();
                        }
                        continue;
                    }

                    ChangeLinkStatus(LinkStatus.Active);
                }
                catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
                {
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

        /// <summary>
        /// Method to verify that the handshake part received from the host matches the expected values.
        /// </summary>
        private bool IsHandshakePartValid(KeyValuePair<string, int> component, int handshakePart, int index)
        {
            if (handshakePart == component.Value)
            {
                return true;
            }

            CommunicationsUtilities.Trace(
                "Handshake failed. Received {0} from host for {1} but expected {2}. Probably the host is a different MSBuild build.",
                handshakePart,
                component.Key,
                component.Value);

            return false;
        }

        private void RunReadLoop(
            BufferedReadStream localReadPipe,
            NamedPipeServerStream localWritePipe,
            ConcurrentQueue<INodePacket> localPacketQueue,
            AutoResetEvent localPacketAvailable,
            AutoResetEvent localTerminatePacketPump)
        {
            // Ordering of the wait handles is important.  The first signaled wait handle in the array
            // will be returned by WaitAny if multiple wait handles are signaled.  We prefer to have the
            // terminate event triggered so that we cannot get into a situation where packets are being
            // spammed to the endpoint and it never gets an opportunity to shutdown.
            CommunicationsUtilities.Trace("Entering read loop.");
            byte[] headerByte = new byte[5];
            ITranslator writeTranslator = null;
            IAsyncResult result = localReadPipe.BeginRead(headerByte, 0, headerByte.Length, null, null);

            // Ordering is important.  We want packetAvailable to supercede terminate otherwise we will not properly wait for all
            // packets to be sent by other threads which are shutting down, such as the logging thread.
            WaitHandle[] handles = new WaitHandle[]
            {
                result.AsyncWaitHandle,
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
                            int bytesRead = 0;
                            try
                            {
                                bytesRead = localReadPipe.EndRead(result);
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
                                    if (_isClientDisconnecting)
                                    {
                                        CommunicationsUtilities.Trace("Parent disconnected gracefully.");
                                        // Do not change link status to failed as this could make node think connection has failed
                                        // and recycle node, while this is perfectly expected and handled race condition
                                        // (both client and node is about to close pipe and client can be faster).
                                    }
                                    else
                                    {
                                        CommunicationsUtilities.Trace("Parent disconnected abruptly.");
                                        ChangeLinkStatus(LinkStatus.Failed);
                                    }
                                }
                                else
                                {
                                    CommunicationsUtilities.Trace("Incomplete header read from server.  {0} of {1} bytes read", bytesRead, headerByte.Length);
                                    ChangeLinkStatus(LinkStatus.Failed);
                                }

                                exitLoop = true;
                                break;
                            }

                            // Check if this packet has an extended header that includes a version part.
                            byte rawType = headerByte[0];
                            bool hasExtendedHeader = NodePacketTypeExtensions.HasExtendedHeader(rawType);
                            NodePacketType packetType = hasExtendedHeader ? NodePacketTypeExtensions.GetNodePacketType(rawType) : (NodePacketType)rawType;

                            byte parentVersion = 0;
                            if (hasExtendedHeader)
                            {
                                parentVersion = NodePacketTypeExtensions.ReadVersion(localReadPipe);
                            }

                            try
                            {
                                ITranslator readTranslator = BinaryTranslator.GetReadTranslator(localReadPipe, _sharedReadBuffer);

                                // parent sends a packet version that is already negotiated during handshake.
                                readTranslator.NegotiatedPacketVersion = parentVersion;
                                _packetFactory.DeserializeAndRoutePacket(0, packetType, readTranslator);
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

                            result = localReadPipe.BeginRead(headerByte, 0, headerByte.Length, null, null);

                            handles[0] = result.AsyncWaitHandle;
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
                                var packetStream = _packetStream;
                                packetStream.SetLength(0);

                                // Re-use writeTranslator; we clear _packetStream but never replace it.
                                // If _packetStream is ever reassigned, set writeTranslator = null first.
                                writeTranslator ??= BinaryTranslator.GetWriteTranslator(packetStream);

                                packetStream.WriteByte((byte)packet.Type);

                                // Pad for packet length
                                _binaryWriter.Write(0);

                                // Reset the position in the write buffer.
                                packet.Translate(writeTranslator);

                                int packetStreamLength = (int)packetStream.Position;

                                // Now write in the actual packet length
                                packetStream.Position = 1;
                                _binaryWriter.Write(packetStreamLength - 5);

                                localWritePipe.Write(packetStream.GetBuffer(), 0, packetStreamLength);
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
