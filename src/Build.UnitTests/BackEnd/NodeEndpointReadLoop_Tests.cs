// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
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

    [Theory]
    [InlineData("RequestedShutdown", false)]
    [InlineData("RequestedShutdown", true)]
    [InlineData("ConnectedWriteFailure", false)]
    [InlineData("ErrorShutdown", false)]
    [InlineData("RequestedShutdownWithError", false)]
    [InlineData("ConnectionFailedShutdown", false)]
    [InlineData("BuildComplete", false)]
    [InlineData("SerializationFailure", false)]
    public void WriteLoop_OnlyAcceptsPeerDisconnectForCleanShutdown(string packetKind, bool terminating)
    {
        using TestEnvironment env = TestEnvironment.Create(output);
        using FailingWriteStream stream = new(disconnect: packetKind != "ConnectedWriteFailure");
        using MemoryStream writeBuffer = new();
        using BinaryWriter writer = new(writeBuffer);
        using AutoResetEvent packetAvailable = new(!terminating);
        using AutoResetEvent terminate = new(terminating);
        TestEndpoint endpoint = new();
        typeof(NodeEndpointOutOfProcBase).GetField("_packetStream", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(endpoint, writeBuffer);
        typeof(NodeEndpointOutOfProcBase).GetField("_binaryWriter", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(endpoint, writer);
        ConcurrentQueue<INodePacket> packets = new();
        packets.Enqueue(packetKind switch
        {
            "RequestedShutdown" => new NodeShutdown(NodeShutdownReason.Requested),
            "ConnectedWriteFailure" => new NodeShutdown(NodeShutdownReason.Requested),
            "ErrorShutdown" => new NodeShutdown(NodeShutdownReason.Error),
            "RequestedShutdownWithError" => new NodeShutdown(NodeShutdownReason.Requested, new InvalidOperationException("Node failed.")),
            "ConnectionFailedShutdown" => new NodeShutdown(NodeShutdownReason.ConnectionFailed),
            "BuildComplete" => new NodeBuildComplete(false),
            "SerializationFailure" => new FailingShutdownPacket(),
            _ => throw new ArgumentOutOfRangeException(nameof(packetKind)),
        });

        DebugUtils.ResetDebugDumpPathInRunningTests = true;
        _ = DebugUtils.DebugDumpPath;
        endpoint.RunReadLoop(stream, packets, packetAvailable, terminate);

        bool expectedDisconnect = packetKind == "RequestedShutdown";
        if (expectedDisconnect)
        {
            File.Exists(DebugUtils.DumpFilePath).ShouldBeFalse();
        }
        else
        {
            File.ReadAllText(DebugUtils.DumpFilePath).ShouldContain(
                packetKind == "SerializationFailure" ? "Injected serialization failure." : "Injected pipe write failure.");
            env.WithTransientTestState(new TransientTestFile(DebugUtils.DebugDumpPath, Path.GetFileName(DebugUtils.DumpFilePath)));
        }

        DebugUtils.ResetDebugDumpPathInRunningTests = true;
        endpoint.LinkStatus.ShouldBe(expectedDisconnect ? LinkStatus.Active : LinkStatus.Failed);
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

    private sealed class FailingWriteStream : PipeStream
    {
        private readonly bool _disconnect;
        private readonly TaskCompletionSource<int> _read = new();

        public FailingWriteStream(bool disconnect) : base(PipeDirection.InOut, 0)
        {
            _disconnect = disconnect;
            IsConnected = true;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => _read.Task;

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_disconnect)
            {
                IsConnected = false;
            }
            throw new IOException("Injected pipe write failure.");
        }

        protected override void Dispose(bool disposing)
        {
            _read.TrySetResult(0);
            base.Dispose(disposing);
        }
    }

    private sealed class FailingShutdownPacket() : NodeShutdown(NodeShutdownReason.Requested), ITranslatable
    {
        void ITranslatable.Translate(ITranslator translator)
            => throw new IOException("Injected serialization failure.");
    }
}
