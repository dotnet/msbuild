// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd.Client
{
    internal sealed class MSBuildClientPacketPump : IAsyncDisposable
    {
        private static readonly UnboundedChannelOptions s_channelOptions = new()
        {
            AllowSynchronousContinuations = true,
            SingleReader = true,
            SingleWriter = true,
        };

        /// <summary>
        /// The queue of packets we have received but which have not yet been processed.
        /// Only one reader is allowed to read from this channel at a time.
        /// </summary>
        public ChannelReader<INodePacket> ReceivedPackets => _receivedPacketsChannel.Reader;

        private readonly CancellationTokenSource _shutdownTokenSource;

        private readonly Channel<INodePacket> _receivedPacketsChannel;

        /// <summary>
        /// Mapping of packet types to deserialization methods.
        /// </summary>
        private readonly Dictionary<NodePacketType, NodePacketFactoryMethod> _packetDeserializationMethods;

        /// <summary>
        /// The memory stream for a read buffer.
        /// </summary>
        private readonly MemoryStream _readBufferMemoryStream;

        /// <summary>
        /// The task which runs the asynchronous packet pump.
        /// </summary>
        private Task? _packetPumpTask;

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
        private volatile bool _isServerDisconnecting;

        public MSBuildClientPacketPump(Stream stream)
        {
            ErrorUtilities.VerifyThrowArgumentNull(stream);

            _stream = stream;
            _shutdownTokenSource = new CancellationTokenSource();
            _receivedPacketsChannel = Channel.CreateUnbounded<INodePacket>(s_channelOptions);
            _packetDeserializationMethods = new Dictionary<NodePacketType, NodePacketFactoryMethod>();

            _readBufferMemoryStream = new MemoryStream();
            _binaryReadTranslator = BinaryTranslator.GetReadTranslator(_readBufferMemoryStream, InterningBinaryReader.CreateSharedBuffer());
        }

        /// <summary>
        /// Registers a packet handler.
        /// </summary>
        /// <param name="packetType">The packet type for which the handler should be registered.</param>
        /// <param name="factory">The factory used to create packets.</param>
        public void RegisterPacketHandler(NodePacketType packetType, NodePacketFactoryMethod factory) =>
            _packetDeserializationMethods.Add(packetType, factory);

        /// <summary>
        /// Deserializes a packet.
        /// </summary>
        /// <param name="packetType">The packet type.</param>
        /// <param name="translator">The translator to use as a source for packet data.</param>
        private INodePacket DeserializePacket(NodePacketType packetType, ITranslator translator)
        {
            ErrorUtilities.VerifyThrow(
                _packetDeserializationMethods.TryGetValue(packetType, out NodePacketFactoryMethod? factory),
                $"No packet handler for type {packetType}");

            return factory(translator);
        }

        #region Packet Pump

        /// <summary>
        /// Initializes the packet pump task.
        /// </summary>
        public void Start() => _packetPumpTask = Task.Run(RunReadLoopAsync);

        private async Task RunReadLoopAsync()
        {
            CommunicationsUtilities.Trace("Entering read loop.");
            CancellationToken shutdownToken = _shutdownTokenSource.Token;
            ChannelWriter<INodePacket> packetWriter = _receivedPacketsChannel.Writer;

            try
            {
                byte[] headerByte = new byte[5];
                while (true)
                {
                    // Client recieved a packet header. Read the rest of it.
#if NET
                    int headerBytesRead = await _stream.ReadAsync(headerByte, shutdownToken).ConfigureAwait(false);
#else
                    int headerBytesRead = await _stream.ReadAsync(headerByte, 0, headerByte.Length, shutdownToken).ConfigureAwait(false);
#endif

                    if ((headerBytesRead != headerByte.Length) && !shutdownToken.IsCancellationRequested)
                    {
                        // Incomplete read. Abort.
                        if (headerBytesRead == 0)
                        {
                            if (_isServerDisconnecting)
                            {
                                break;
                            }

                            ErrorUtilities.ThrowInternalError("Server disconnected abruptly");
                        }
                        else
                        {
                            ErrorUtilities.ThrowInternalError($"Incomplete header read.  {headerBytesRead} of {headerByte.Length} bytes read");
                        }
                    }

                    NodePacketType packetType = (NodePacketType)headerByte[0];

                    int packetLength = BinaryPrimitives.ReadInt32LittleEndian(headerByte.AsSpan(1));
                    int packetBytesRead = 0;

                    _readBufferMemoryStream.Position = 0;
                    _readBufferMemoryStream.SetLength(packetLength);
                    byte[] packetData = _readBufferMemoryStream.GetBuffer();

                    while (packetBytesRead < packetLength)
                    {
#if NET
                        int bytesRead = await _stream.ReadAsync(packetData.AsMemory(packetBytesRead, packetLength - packetBytesRead), shutdownToken).ConfigureAwait(false);
#else
                        int bytesRead = await _stream.ReadAsync(packetData, packetBytesRead, packetLength - packetBytesRead, shutdownToken).ConfigureAwait(false);
#endif
                        if (bytesRead == 0)
                        {
                            // Incomplete read.  Abort.
                            ErrorUtilities.ThrowInternalError($"Incomplete packet read. {packetBytesRead} of {packetLength} bytes read");
                        }

                        packetBytesRead += bytesRead;
                    }

                    try
                    {
                        INodePacket packet = DeserializePacket(packetType, _binaryReadTranslator);
                        await packetWriter.WriteAsync(packet, shutdownToken).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Error while deserializing or handling packet. Logging additional info.
                        CommunicationsUtilities.Trace($"Packet factory failed to receive package. Exception while deserializing packet {packetType}.");
                        throw;
                    }

                    if (packetType == NodePacketType.ServerNodeBuildResult)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == shutdownToken)
            {
                CommunicationsUtilities.Trace("Packet pump shutdown requested.");
            }
            catch (Exception ex)
            {
                CommunicationsUtilities.Trace($"Exception occurred in the packet pump: {ex}");
                packetWriter.Complete(ex);
            }

            CommunicationsUtilities.Trace("Ending read loop.");
            packetWriter.TryComplete();
        }
        #endregion

        /// <summary>
        /// Stops the packet pump loop, and waits for it to finish.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            _shutdownTokenSource.Cancel();
            if (_packetPumpTask is not null)
            {
                await _packetPumpTask.ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Signalize that from now on we expect server will break connected named pipe.
        /// </summary>
        public void ServerWillDisconnect()
        {
            _isServerDisconnecting = true;
        }
    }
}
