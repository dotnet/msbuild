// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;
using Microsoft.Build.Internal;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    public sealed class IpcStreamFragment_Tests
    {
        private sealed class FragmentedStream : Stream
        {
            private readonly byte[] _buffer;
            private int _position;
            private readonly int _fragmentSize;

            public FragmentedStream(byte[] buffer, int fragmentSize)
            {
                _buffer = buffer;
                _fragmentSize = fragmentSize;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _buffer.Length;
            public override long Position { get => _position; set => throw new NotSupportedException(); }

            public override void Flush() { }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_position >= _buffer.Length)
                {
                    return 0; // EOF
                }

                int toRead = Math.Min(count, _fragmentSize);
                toRead = Math.Min(toRead, _buffer.Length - _position);

                Array.Copy(_buffer, _position, buffer, offset, toRead);
                _position += toRead;

                return toRead;
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return Task.FromResult(Read(buffer, offset, count));
            }

#if FEATURE_APM
            public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
            {
                var result = new StreamAsyncResult(state);
                result.BytesRead = Read(buffer, offset, count);
                result.IsCompleted = true;
                callback?.Invoke(result);
                return result;
            }

            public override int EndRead(IAsyncResult asyncResult)
            {
                return ((StreamAsyncResult)asyncResult).BytesRead;
            }

            private sealed class StreamAsyncResult : IAsyncResult
            {
                public StreamAsyncResult(object state) => AsyncState = state;
                public int BytesRead { get; set; }
                public object AsyncState { get; }
                public WaitHandle AsyncWaitHandle => throw new NotSupportedException();
                public bool CompletedSynchronously => true;
                public bool IsCompleted { get; set; }
            }
#endif

#if NETCOREAPP
            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                if (_position >= _buffer.Length)
                {
                    return new ValueTask<int>(0);
                }

                int toRead = Math.Min(buffer.Length, _fragmentSize);
                toRead = Math.Min(toRead, _buffer.Length - _position);

                _buffer.AsMemory(_position, toRead).CopyTo(buffer);
                _position += toRead;

                return new ValueTask<int>(toRead);
            }
#endif

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        private sealed class MockPacketFactory : INodePacketFactory
        {
            public int RoutePacketCalls { get; private set; }
            public INodePacket LastPacket { get; private set; }

            public void RoutePacket(int nodeId, INodePacket packet)
            {
                RoutePacketCalls++;
                LastPacket = packet;
            }

            public void DeserializeAndRoutePacket(int nodeId, NodePacketType packetType, ITranslator translator)
            {
                RoutePacketCalls++;
            }

            public void RegisterPacketHandler(NodePacketType packetType, NodePacketFactoryMethod factory, INodePacketHandler handler)
            {
            }

            public void UnregisterPacketHandler(NodePacketType packetType)
            {
            }

            public INodePacket DeserializePacket(NodePacketType packetType, ITranslator translator)
            {
                return null;
            }
        }

        [Fact]
        public async Task RunPacketReadLoopAsync_HandlesFragmentedHeader()
        {
            // Prepare a mock packet stream.
            byte packetType = (byte)NodePacketType.NodeShutdown;
            byte[] packetBytes = new byte[] { packetType, 0, 0, 0, 0 }; // 5 byte header, 0 length body

            // Fragment size of 2 will force a partial read of the 5-byte header (2 bytes, then 2 bytes, then 1 byte)
            var fragmentedStream = new FragmentedStream(packetBytes, fragmentSize: 2);
            var factory = new MockPacketFactory();

            var context = new NodeProviderOutOfProcBase.NodeContext(
                nodeId: 1,
                process: null,
                nodePipe: fragmentedStream,
                factory: factory,
                terminateDelegate: (nodeId) => { },
                negotiatedVersion: 0,
                handshakeOptions: HandshakeOptions.None);

#if !FEATURE_APM
            await context.RunPacketReadLoopAsync();
#else
            context.BeginAsyncPacketRead();
            await Task.Yield(); // Just let it synchronously finish inside BeginAsyncPacketRead
#endif
            
            if (factory.LastPacket is NodeShutdown shutdownPacket)
            {
                // The NodeShutdown packet should NOT be a ConnectionFailed reason with an exception.
                shutdownPacket.Reason.ShouldNotBe(NodeShutdownReason.ConnectionFailed);
            }
        }
    }
}
