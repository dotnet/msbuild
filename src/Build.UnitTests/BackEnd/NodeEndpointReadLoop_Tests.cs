// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.IO;
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
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public void ReadLoop_DistinguishesFailureFromExpectedDisconnect(bool throwOnRead, bool expectedDisconnect)
    {
        using TestEnvironment env = TestEnvironment.Create(output);
        using MemoryStream stream = throwOnRead ? new FailingReadStream() : new MemoryStream();
        using AutoResetEvent packetAvailable = new(false);
        using AutoResetEvent terminate = new(false);
        TestEndpoint endpoint = new();

        if (expectedDisconnect)
        {
            endpoint.ClientWillDisconnect();
        }

        // Keep the injected failure out of other test processes' crash-log checks.
        TransientTestFolder debugPath = env.CreateFolder();
        var transientDebugPath = env.SetEnvironmentVariable("MSBUILDDEBUGPATH", debugPath.Path);

        try
        {
            FrameworkDebugUtils.SetDebugPath();
            DebugUtils.ResetDebugDumpPathInRunningTests = true;
            _ = DebugUtils.DebugDumpPath;
            endpoint.RunReadLoop(stream, new ConcurrentQueue<INodePacket>(), packetAvailable, terminate);

            string[] failureLogs = Directory.GetFiles(debugPath.Path, "MSBuild_*.failure.txt", SearchOption.AllDirectories);
            if (throwOnRead)
            {
                failureLogs.ShouldHaveSingleItem();
                failureLogs[0].ShouldBe(DebugUtils.DumpFilePath);
                File.ReadAllText(failureLogs[0]).ShouldContain("Injected pipe read failure.");
            }
            else
            {
                failureLogs.ShouldBeEmpty();
            }

            endpoint.LinkStatus.ShouldBe(expectedDisconnect ? LinkStatus.Active : LinkStatus.Failed);
        }
        finally
        {
            transientDebugPath.Revert();
            FrameworkDebugUtils.SetDebugPath();
            DebugUtils.ResetDebugDumpPathInRunningTests = true;
            _ = DebugUtils.DebugDumpPath;
        }
    }

    /// <summary>
    /// A parent that has sent its final <see cref="NodeBuildComplete"/> may close the pipe before the node
    /// answers (ShutdownAllNodes does exactly that). The node's reply then fails with "Pipe is broken";
    /// that is the expected end of the link, not a failure worth a crash dump. A parent that keeps the
    /// connection (<see cref="NodeBuildCompleteAction.ReuseWithConnection"/>) or has not finished the
    /// build still gets the dump.
    /// </summary>
    [Theory]
    [InlineData((int)NodeBuildCompleteAction.Legacy, false, false)]
    [InlineData((int)NodeBuildCompleteAction.Legacy, true, false)]
    [InlineData((int)NodeBuildCompleteAction.Shutdown, false, false)]
    [InlineData((int)NodeBuildCompleteAction.ReuseWithConnection, true, true)]
    public void WriteFailureAfterParentEndsLink_IsNotDumped(int action, bool prepareForReuse, bool expectDump)
    {
        WriteFailureAfterPacket(new NodeBuildComplete(prepareForReuse, (NodeBuildCompleteAction)action), expectDump);
    }

    [Fact]
    public void WriteFailureMidBuild_IsDumped()
    {
        WriteFailureAfterPacket(packetFromParent: null, expectDump: true);
    }

    private void WriteFailureAfterPacket(NodeBuildComplete? packetFromParent, bool expectDump)
    {
        using TestEnvironment env = TestEnvironment.Create(output);
        using AutoResetEvent packetAvailable = new(false);
        using AutoResetEvent terminate = new(false);
        ConcurrentQueue<INodePacket> queue = new();
        TestEndpoint endpoint = new();

        // The node answers the parent's packet the way OutOfProcNode.HandleShutdown does: it queues NodeShutdown.
        NodePacketFactory factory = new();
        factory.RegisterPacketHandler(NodePacketType.NodeBuildComplete, NodeBuildComplete.FactoryForDeserialization, new ReplyingHandler(queue, packetAvailable));
        endpoint.InitializeForTesting(factory);

        using ParentClosedPipeStream stream = new(packetFromParent is null ? [] : Serialize(packetFromParent));
        if (packetFromParent is null)
        {
            queue.Enqueue(new NodeShutdown(NodeShutdownReason.Requested));
            packetAvailable.Set();
        }

        // Other tests move the process-wide dump path around (and delete what they created), so own it here.
        var debugPath = env.SetEnvironmentVariable("MSBUILDDEBUGPATH", env.CreateFolder().Path);
        try
        {
            FrameworkDebugUtils.SetDebugPath();
            DebugUtils.ResetDebugDumpPathInRunningTests = true;
            string dumpDirectory = DebugUtils.DebugDumpPath;

            endpoint.RunReadLoop(stream, queue, packetAvailable, terminate);

            stream.WriteAttempted.ShouldBeTrue();
            endpoint.LinkStatus.ShouldBe(LinkStatus.Failed);
            string[] dumps = Directory.GetFiles(dumpDirectory, "MSBuild_pid-*.failure.txt");
            if (expectDump)
            {
                File.ReadAllText(dumps.ShouldHaveSingleItem()).ShouldContain("Pipe is broken.");
            }
            else
            {
                dumps.ShouldBeEmpty();
            }
        }
        finally
        {
            debugPath.Revert();
            FrameworkDebugUtils.SetDebugPath();
            DebugUtils.ResetDebugDumpPathInRunningTests = true;
            _ = DebugUtils.DebugDumpPath;
        }
    }

    private static byte[] Serialize(INodePacket packet)
    {
        // Frame the packet like NodeContext does for a current parent: extended header, then the version,
        // so version-dependent fields such as NodeBuildComplete.Action are on the wire.
        using MemoryStream body = new();
        NodePacketTypeExtensions.WriteVersion(body, NodePacketTypeExtensions.PacketVersion);
        ITranslator translator = BinaryTranslator.GetWriteTranslator(body);
        translator.NegotiatedPacketVersion = NodePacketTypeExtensions.PacketVersion;
        packet.Translate(translator);

        byte[] framed = new byte[5 + body.Length];
        framed[0] = (byte)((byte)packet.Type | 0x40);
        NodePacketTypeExtensions.HasExtendedHeader(framed[0]).ShouldBeTrue();
        BitConverter.GetBytes((int)body.Length).CopyTo(framed, 1);
        body.ToArray().CopyTo(framed, 5);
        return framed;
    }

    private sealed class ReplyingHandler(ConcurrentQueue<INodePacket> queue, AutoResetEvent packetAvailable) : INodePacketHandler
    {
        public void PacketReceived(int node, INodePacket packet)
        {
            queue.Enqueue(new NodeShutdown(NodeShutdownReason.Requested));
            packetAvailable.Set();
        }
    }

    /// <summary>
    /// Delivers the parent's bytes, then models a parent that has closed its end: reads stay pending (the node has
    /// not observed EOF yet) and writes fail the way a closed named pipe fails them.
    /// </summary>
    private sealed class ParentClosedPipeStream(byte[] fromParent) : MemoryStream(fromParent)
    {
        private readonly TaskCompletionSource<int> _neverCompletes = new();

        public bool WriteAttempted { get; private set; }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => Position < Length ? base.ReadAsync(buffer, offset, count, cancellationToken) : _neverCompletes.Task;

        public override void Write(byte[] buffer, int offset, int count)
        {
            WriteAttempted = true;
            throw new IOException("Pipe is broken.");
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
