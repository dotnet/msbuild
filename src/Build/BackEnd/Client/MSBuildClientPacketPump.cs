// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers.Binary;
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
        /// The factory method for creating packets based on packet type.
        /// </summary>
        private readonly Func<NodePacketType, ITranslator, INodePacket> _packetFactoryMethod;

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

        public MSBuildClientPacketPump(Stream stream, Func<NodePacketType, ITranslator, INodePacket> packetFactoryMethod)
        {
            ErrorUtilities.VerifyThrowArgumentNull(stream);
            ErrorUtilities.VerifyThrowArgumentNull(packetFactoryMethod);

            _stream = stream;
            _shutdownTokenSource = new CancellationTokenSource();
            _receivedPacketsChannel = Channel.CreateUnbounded<INodePacket>(s_channelOptions);
            _packetFactoryMethod = packetFactoryMethod;

            _readBufferMemoryStream = new MemoryStream();
            _binaryReadTranslator = BinaryTranslator.GetReadTranslator(_readBufferMemoryStream, InterningBinaryReader.CreateSharedBuffer());
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
                        INodePacket packet = _packetFactoryMethod(packetType, _binaryReadTranslator);
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
