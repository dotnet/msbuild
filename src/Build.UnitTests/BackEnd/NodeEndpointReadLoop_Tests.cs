// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared.Debugging;
using Shouldly;
using Xunit;

#nullable enable

namespace Microsoft.Build.UnitTests.BackEnd;

public sealed class NodeEndpointReadLoop_Tests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public void ReadLoop_DistinguishesFailureFromExpectedDisconnect(bool throwOnRead, bool expectedDisconnect)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        using MemoryStream stream = throwOnRead ? new FailingReadStream() : new MemoryStream();
        using AutoResetEvent packetAvailable = new(false);
        using AutoResetEvent terminate = new(false);
        TestEndpoint endpoint = new();

        if (expectedDisconnect)
        {
            endpoint.ClientWillDisconnect();
        }

        DebugUtils.ResetDebugDumpPathInRunningTests = true;
        _ = DebugUtils.DebugDumpPath;
        endpoint.RunReadLoop(stream, new ConcurrentQueue<INodePacket>(), packetAvailable, terminate);

        if (throwOnRead)
        {
            env.WithTransientTestState(new TransientTestFile(DebugUtils.DebugDumpPath, Path.GetFileName(DebugUtils.DumpFilePath)));
            DebugUtils.ResetDebugDumpPathInRunningTests = true;
        }

        endpoint.LinkStatus.ShouldBe(expectedDisconnect ? LinkStatus.Active : LinkStatus.Failed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(NodePacketTypeExtensions.PacketVersion)]
    public async Task LogBatch_PreservesSerializedFramesAndControlOrder(byte version)
    {
        INodePacket[] packets =
        [
            Log("first"), Log("second"),
            new NodeBuildComplete(false, NodeBuildCompleteAction.Shutdown),
            Log("third"), Log("fourth")
        ];
        byte[] expected = SerializeFrames(packets, version);
        using CountingWriteStream stream = new();

        await RunWritePump(stream, packets, version);

        stream.ToArray().ShouldBe(expected);
        stream.Writes.Count.ShouldBe(3);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LogBatch_FlushesOnQueueEmptyOrTermination(bool terminating)
    {
        INodePacket[] packets = [new RawPacket(17), new RawPacket(23)];
        using CountingWriteStream stream = new();

        await RunWritePump(stream, packets, terminating: terminating);

        stream.ToArray().ShouldBe(SerializeFrames(packets));
        stream.Writes.ShouldBe([40]);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    public async Task LogBatch_RespectsByteBudget(int adjustment, int expectedWrites)
    {
        int half = NodeEndpointOutOfProcBase.LogPacketBatchSize / 2;
        INodePacket[] packets = [new RawPacket(half), new RawPacket(half + adjustment)];
        using CountingWriteStream stream = new();

        await RunWritePump(stream, packets);

        stream.ToArray().ShouldBe(SerializeFrames(packets));
        stream.Writes.Count.ShouldBe(expectedWrites);
        stream.Writes.ShouldAllBe(size => size <= NodeEndpointOutOfProcBase.LogPacketBatchSize);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public async Task LogBatch_WritesLargePacketsInOrder(int adjustment)
    {
        int size = NodeEndpointOutOfProcBase.LogPacketBatchSize + adjustment;
        INodePacket[] packets = [new RawPacket(6), new RawPacket(size), new RawPacket(7)];
        using CountingWriteStream stream = new();

        await RunWritePump(stream, packets);

        stream.ToArray().ShouldBe(SerializeFrames(packets));
        stream.Writes.ShouldBe([6, size, 7]);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task LogBatch_SerializationFailurePreservesPrefixAndBothErrors(bool controlPacket, bool failWrite)
    {
        using CountingWriteStream stream = new() { FailWrite = failWrite };
        RawPacket prefix = new(17);
        RawPacket failure = new(6, controlPacket ? NodePacketType.NodeBuildComplete : NodePacketType.LogMessage, () =>
        {
            if (controlPacket)
            {
                stream.ToArray().ShouldBe(SerializeFrames([prefix]));
            }

            throw new InvalidOperationException("Injected packet serialization failure.");
        });

        string diagnostic = await RunWritePump(stream, [prefix, failure, new RawPacket(9)]);

        diagnostic.ShouldContain("Injected packet serialization failure.");
        stream.Writes.ShouldBe([17]);
        stream.ToArray().ShouldBe(failWrite ? Array.Empty<byte>() : SerializeFrames([prefix]));
        if (failWrite)
        {
            diagnostic.ShouldContain("Injected pipe write failure.");
        }
    }

    [Fact]
    public async Task LogBatch_WriteFailureFailsLinkWithoutRetry()
    {
        using CountingWriteStream stream = new() { FailWrite = true };

        string diagnostic = await RunWritePump(stream, [new RawPacket(17), new RawPacket(23)]);

        diagnostic.ShouldContain("Injected pipe write failure.");
        stream.Writes.ShouldBe([40]);
        stream.Length.ShouldBe(0);
    }

    private static LogMessagePacket Log(string message) =>
        new(new KeyValuePair<int, BuildEventArgs>(0, new BuildMessageEventArgs(message, "", "", MessageImportance.Low)));

    private static byte[] SerializeFrames(INodePacket[] packets, byte version = 0)
    {
        using MemoryStream outputStream = new();
        using MemoryStream packetStream = new();
        using ITranslator translator = BinaryTranslator.GetWriteTranslator(packetStream);
        translator.NegotiatedPacketVersion = version;
        foreach (INodePacket packet in packets)
        {
            packetStream.SetLength(0);
            packetStream.WriteByte((byte)packet.Type);
            translator.Writer.Write(0);
            packet.Translate(translator);
            int length = (int)packetStream.Position;
            packetStream.Position = 1;
            translator.Writer.Write(length - 5);
            outputStream.Write(packetStream.GetBuffer(), 0, length);
        }

        return outputStream.ToArray();
    }

    private async Task<string> RunWritePump(CountingWriteStream stream, INodePacket[] packets, byte version = 0, bool terminating = false)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        using MemoryStream packetStream = new();
        using BinaryWriter writer = new(packetStream);
        using AutoResetEvent available = new(!terminating);
        using AutoResetEvent terminate = new(terminating);
        TestEndpoint endpoint = new();
        // Initialize only serialization state, without opening a real named pipe.
        SetField("_packetStream", packetStream);
        SetField("_binaryWriter", writer);
        SetField("_negotiatedWriteVersion", version);
        stream.AfterWrite = () => terminate.Set();
        DebugUtils.ResetDebugDumpPathInRunningTests = true;
        _ = DebugUtils.DebugDumpPath;

        Task pump = Task.Run(() => endpoint.RunReadLoop(stream, new ConcurrentQueue<INodePacket>(packets), available, terminate));
        using CancellationTokenSource timeout = new();
        Task completed = await Task.WhenAny(pump, Task.Delay(TimeSpan.FromSeconds(30), timeout.Token));
        terminate.Set();
        await pump;
        timeout.Cancel();
        completed.ShouldBe(pump, "the queue-empty flush should not wait for another packet or termination");
        endpoint.LinkStatus.ShouldBe(LinkStatus.Failed);

        string diagnostic = "";
        if (DebugUtils.DumpFilePath is not null)
        {
            diagnostic = File.ReadAllText(DebugUtils.DumpFilePath);
            env.WithTransientTestState(new TransientTestFile(DebugUtils.DebugDumpPath, Path.GetFileName(DebugUtils.DumpFilePath), diagnostic));
            DebugUtils.ResetDebugDumpPathInRunningTests = true;
        }

        return diagnostic;

        void SetField(string name, object value) =>
            typeof(NodeEndpointOutOfProcBase).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(endpoint, value);
    }

    private sealed class RawPacket(int frameSize, NodePacketType type = NodePacketType.LogMessage, Action? beforeTranslate = null) : INodePacket
    {
        private readonly byte[] _payload = new byte[frameSize - 5];
        public NodePacketType Type => type;

        public void Translate(ITranslator translator)
        {
            translator.Writer.BaseStream.Position.ShouldBe(5);
            beforeTranslate?.Invoke();
            translator.Writer.Write(_payload);
        }
    }

    private sealed class CountingWriteStream : MemoryStream
    {
        private readonly TaskCompletionSource<int> _read = new();
        public List<int> Writes { get; } = [];
        public bool FailWrite { get; init; }
        public Action? AfterWrite { get; set; }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _read.Task;

        public override void Write(byte[] buffer, int offset, int count)
        {
            Writes.Add(count);
            if (FailWrite)
            {
                throw new IOException("Injected pipe write failure.");
            }

            base.Write(buffer, offset, count);
            AfterWrite?.Invoke();
        }

        protected override void Dispose(bool disposing)
        {
            _read.TrySetResult(0);
            base.Dispose(disposing);
        }
    }

    private sealed class TestEndpoint : NodeEndpointOutOfProcBase
    {
        public TestEndpoint() => ChangeLinkStatus(LinkStatus.Active);

        protected override Handshake GetHandshake() => throw new NotSupportedException();
    }

    private sealed class FailingReadStream : MemoryStream
    {
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => Task.FromException<int>(new IOException("Injected pipe read failure."));
    }
}
