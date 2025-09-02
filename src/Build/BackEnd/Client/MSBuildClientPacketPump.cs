// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
#if !FEATURE_APM
using System.Threading.Tasks;
#endif

namespace Microsoft.Build.BackEnd.Client
{
    internal sealed class MSBuildClientPacketPump : INodePacketHandler, INodePacketFactory, IDisposable
    {
        /// <summary>
        /// The queue of packets we have received but which have not yet been processed.
        /// </summary>
        public ConcurrentQueue<INodePacket> ReceivedPacketsQueue { get; }

        /// <summary>
        /// Set when packet pump receive packets and put them to <see cref="ReceivedPacketsQueue"/>.
        /// </summary>
        public AutoResetEvent PacketReceivedEvent { get; }

        /// <summary>
        /// Set when the packet pump terminates.
        /// </summary>
        public ManualResetEvent PacketPumpCompleted { get; }

        /// <summary>
        /// Exception appeared when the packet pump unexpectedly terminates (due to connection problems or because of deserialization issues).
        /// </summary>
        public Exception? PacketPumpException { get; set; }

        /// <summary>
        /// Set when packet pump should shutdown.
        /// </summary>
        private readonly ManualResetEvent _packetPumpShutdownEvent;

        /// <summary>
        /// The packet factory.
        /// </summary>
        private readonly NodePacketFactory _packetFactory;

        /// <summary>
        /// The memory stream for a read buffer.
        /// </summary>
        private readonly MemoryStream _readBufferMemoryStream;

        /// <summary>
        /// The thread which runs the asynchronous packet pump
        /// </summary>
        private Thread? _packetPumpThread;

        /// <summary>
        /// The stream from where to read packets.
        /// </summary>
        private readonly Stream _stream;

        /// <summary>
        /// The binary translator for reading packets.
        /// </summary>
        private readonly ITranslator _binaryReadTranslator;

        /// <summary>
        /// True if this side is gracefully disconnecting.
        /// In such case we have sent last packet to server side and we expect
        /// it will soon broke pipe connection - unless client do it first.
        /// </summary>
        private bool _isServerDisconnecting;

        public MSBuildClientPacketPump(Stream stream)
        {
            ErrorUtilities.VerifyThrowArgumentNull(stream, nameof(stream));

            _stream = stream;
            _isServerDisconnecting = false;
            _packetFactory = new NodePacketFactory();

            ReceivedPacketsQueue = new ConcurrentQueue<INodePacket>();
            PacketReceivedEvent = new AutoResetEvent(false);
            PacketPumpCompleted = new ManualResetEvent(false);
            _packetPumpShutdownEvent = new ManualResetEvent(false);

            _readBufferMemoryStream = new MemoryStream();
            _binaryReadTranslator = BinaryTranslator.GetReadTranslator(_readBufferMemoryStream, InterningBinaryReader.CreateSharedBuffer());
        }

        #region INodePacketFactory Members

        /// <summary>
        /// Registers a packet handler.
        /// </summary>
        /// <param name="packetType">The packet type for which the handler should be registered.</param>
        /// <param name="factory">The factory used to create packets.</param>
        /// <param name="handler">The handler for the packets.</param>
        public void RegisterPacketHandler(NodePacketType packetType, NodePacketFactoryMethod factory, INodePacketHandler handler)
        {
            _packetFactory.RegisterPacketHandler(packetType, factory, handler);
        }

        /// <summary>
        /// Unregisters a packet handler.
        /// </summary>
        /// <param name="packetType">The type of packet for which the handler should be unregistered.</param>
        public void UnregisterPacketHandler(NodePacketType packetType)
        {
            _packetFactory.UnregisterPacketHandler(packetType);
        }

        /// <summary>
        /// Deserializes and routes a packer to the appropriate handler.
        /// </summary>
        /// <param name="nodeId">The node from which the packet was received.</param>
        /// <param name="packetType">The packet type.</param>
        /// <param name="translator">The translator to use as a source for packet data.</param>
        public void DeserializeAndRoutePacket(int nodeId, NodePacketType packetType, ITranslator translator)
        {
            _packetFactory.DeserializeAndRoutePacket(nodeId, packetType, translator);
        }

        /// <summary>
        /// Routes a packet to the appropriate handler.
        /// </summary>
        /// <param name="nodeId">The node id from which the packet was received.</param>
        /// <param name="packet">The packet to route.</param>
        public void RoutePacket(int nodeId, INodePacket packet)
        {
            _packetFactory.RoutePacket(nodeId, packet);
        }

        #endregion

        #region INodePacketHandler Members

        /// <summary>
        /// Called when a packet has been received.
        /// </summary>
        /// <param name="node">The node from which the packet was received.</param>
        /// <param name="packet">The packet.</param>
        public void PacketReceived(int node, INodePacket packet)
        {
            ReceivedPacketsQueue.Enqueue(packet);
            PacketReceivedEvent.Set();
        }

        #endregion

        #region Packet Pump
        /// <summary>
        /// Initializes the packet pump thread.
        /// </summary>
        public void Start()
        {
            _packetPumpThread = new Thread(PacketPumpProc)
            {
                IsBackground = true,
                Name = "MSBuild Client Packet Pump"
            };
            _packetPumpThread.Start();
        }

        /// <summary>
        /// Stops the packet pump thread.
        /// </summary>
        public void Stop()
        {
            _packetPumpShutdownEvent.Set();
            _packetPumpThread?.Join();
        }

        /// <summary>
        /// This method handles the packet pump reading. It will terminate when the terminate event is
        /// set.
        /// </summary>
        /// <remarks>
        /// Instead of throwing an exception, puts it in <see cref="PacketPumpException"/> and raises event <see cref="PacketPumpCompleted"/>.
        /// </remarks>
        private void PacketPumpProc()
        {
            RunReadLoop(_stream, _packetPumpShutdownEvent);
        }

        private void RunReadLoop(Stream localStream, ManualResetEvent localPacketPumpShutdownEvent)
        {
            CommunicationsUtilities.Trace("Entering read loop.");

            try
            {
                byte[] headerByte = new byte[5];
#if FEATURE_APM
                IAsyncResult result = localStream.BeginRead(headerByte, 0, headerByte.Length, null, null);
#else
                Task<int> readTask = CommunicationsUtilities.ReadAsync(localStream, headerByte, headerByte.Length);
#endif

                bool continueReading = true;
                do
                {
                    // Ordering of the wait handles is important. The first signalled wait handle in the array
                    // will be returned by WaitAny if multiple wait handles are signalled. We prefer to have the
                    // terminate event triggered so that we cannot get into a situation where packets are being
                    // spammed to the client and it never gets an opportunity to shutdown.
                    WaitHandle[] handles = new WaitHandle[] {
                    localPacketPumpShutdownEvent,
#if FEATURE_APM
                    result.AsyncWaitHandle
#else
                    ((IAsyncResult)readTask).AsyncWaitHandle
#endif
                    };
                    int waitId = WaitHandle.WaitAny(handles);
                    switch (waitId)
                    {
                        case 0:
                            // Fulfill the request for shutdown of the message pump.
                            CommunicationsUtilities.Trace("Shutdown message pump thread.");
                            continueReading = false;
                            break;

                        case 1:
                            {
                                // Client recieved a packet header. Read the rest of it.
                                int headerBytesRead = 0;
#if FEATURE_APM
                                headerBytesRead = localStream.EndRead(result);
#else
                                headerBytesRead = readTask.Result;
#endif

                                if ((headerBytesRead != headerByte.Length) && !localPacketPumpShutdownEvent.WaitOne(0))
                                {
                                    // Incomplete read. Abort.
                                    if (headerBytesRead == 0)
                                    {
                                        if (_isServerDisconnecting)
                                        {
                                            continueReading = false;
                                            break;
                                        }

                                        ErrorUtilities.ThrowInternalError("Server disconnected abruptly");
                                    }
                                    else
                                    {
                                        ErrorUtilities.ThrowInternalError("Incomplete header read.  {0} of {1} bytes read", headerBytesRead, headerByte.Length);
                                    }
                                }

                                NodePacketType packetType = (NodePacketType)Enum.ToObject(typeof(NodePacketType), headerByte[0]);

                                int packetLength = BinaryPrimitives.ReadInt32LittleEndian(new Span<byte>(headerByte, 1, 4));
                                int packetBytesRead = 0;

                                _readBufferMemoryStream.Position = 0;
                                _readBufferMemoryStream.SetLength(packetLength);
                                byte[] packetData = _readBufferMemoryStream.GetBuffer();

                                while (packetBytesRead < packetLength)
                                {
                                    int bytesRead = localStream.Read(packetData, packetBytesRead, packetLength - packetBytesRead);
                                    if (bytesRead == 0)
                                    {
                                        // Incomplete read.  Abort.
                                        ErrorUtilities.ThrowInternalError("Incomplete packet read. {0} of {1} bytes read", packetBytesRead, packetLength);
                                    }

                                    packetBytesRead += bytesRead;
                                }

                                try
                                {
                                    _packetFactory.DeserializeAndRoutePacket(0, packetType, _binaryReadTranslator);
                                }
                                catch
                                {
                                    // Error while deserializing or handling packet. Logging additional info.
                                    CommunicationsUtilities.Trace("Packet factory failed to receive package. Exception while deserializing packet {0}.", packetType);
                                    throw;
                                }

                                if (packetType == NodePacketType.ServerNodeBuildResult)
                                {
                                    continueReading = false;
                                }
                                else
                                {
                                    // Start reading the next package header.
#if FEATURE_APM
                                    result = localStream.BeginRead(headerByte, 0, headerByte.Length, null, null);
#else
                                    readTask = CommunicationsUtilities.ReadAsync(localStream, headerByte, headerByte.Length);
#endif
                                }
                            }
                            break;

                        default:
                            ErrorUtilities.ThrowInternalError("WaitId {0} out of range.", waitId);
                            break;
                    }
                }
                while (continueReading);
            }
            catch (Exception ex)
            {
                CommunicationsUtilities.Trace("Exception occurred in the packet pump: {0}", ex);
                PacketPumpException = ex;
            }

            CommunicationsUtilities.Trace("Ending read loop.");
            PacketPumpCompleted.Set();
        }
        #endregion

        public void Dispose() => Stop();

        /// <summary>
        /// Signalize that from now on we expect server will break connected named pipe.
        /// </summary>
        public void ServerWillDisconnect()
        {
            _isServerDisconnecting = true;
        }
    }
}
