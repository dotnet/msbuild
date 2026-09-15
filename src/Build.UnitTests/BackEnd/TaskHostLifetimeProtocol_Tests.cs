// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

#nullable enable

namespace Microsoft.Build.UnitTests.BackEnd;

public sealed class TaskHostLifetimeProtocol_Tests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void CompletionActionRequiresNegotiatedSupport(byte version)
    {
        using MemoryStream stream = new();
        ITranslator writer = BinaryTranslator.GetWriteTranslator(stream);
        writer.NegotiatedPacketVersion = version;
        new NodeBuildComplete(true, NodeBuildCompleteAction.ReuseWithConnection).Translate(writer);
        stream.Position = 0;
        ITranslator reader = BinaryTranslator.GetReadTranslator(stream, InterningBinaryReader.CreateSharedBuffer());
        reader.NegotiatedPacketVersion = version;

        NodeBuildComplete packet = NodeBuildComplete.FactoryForDeserialization(reader);
        packet.PrepareForReuse.ShouldBeTrue();
        packet.Action.ShouldBe(version >= 6 ? NodeBuildCompleteAction.ReuseWithConnection : NodeBuildCompleteAction.Legacy);
        stream.Position.ShouldBe(stream.Length);
    }

    [Fact]
    public void OldParentCompletionIsNotAnOwnershipRequest()
    {
        using MemoryStream stream = new([1]);
        ITranslator reader = BinaryTranslator.GetReadTranslator(stream, InterningBinaryReader.CreateSharedBuffer());
        reader.NegotiatedPacketVersion = 5;

        NodeBuildComplete packet = NodeBuildComplete.FactoryForDeserialization(reader);
        packet.PrepareForReuse.ShouldBeTrue();
        packet.Action.ShouldBe(NodeBuildCompleteAction.Legacy);
    }

    [Theory]
    [InlineData(5, false)]
    [InlineData(6, true)]
    [InlineData(7, true)]
    public void Clr4CompletionUsesVersionedLifetimeActions(byte version, bool extended)
    {
        NodePacketTypeExtensions.TryCreateExtendedHeaderType(
            HandshakeOptions.TaskHost, NodePacketType.NodeBuildComplete, out byte rawType, version).ShouldBe(extended);
        NodePacketTypeExtensions.HasExtendedHeader(rawType).ShouldBe(extended);
        NodePacketTypeExtensions.TryCreateExtendedHeaderType(
            HandshakeOptions.TaskHost, NodePacketType.TaskHostConfiguration, out _, version).ShouldBeFalse(
                "CLR4 task configuration keeps its AppDomain-compatible format");
    }

    [Fact]
    public void ConsoleForwardingRequiresANewerProtocolThanOwnership()
    {
        NodePacketTypeExtensions.TaskHostOwnershipMinVersion.ShouldBe((byte)6);
        NodePacketTypeExtensions.ConsoleOutputForwardingMinVersion.ShouldBe((byte)7);
        NodePacketTypeExtensions.GetNegotiatedPacketVersion(6)
            .ShouldBeLessThan(NodePacketTypeExtensions.ConsoleOutputForwardingMinVersion);
        NodePacketTypeExtensions.GetNegotiatedPacketVersion(7)
            .ShouldBe(NodePacketTypeExtensions.ConsoleOutputForwardingMinVersion);
        ((byte)new TaskHostConsoleConfiguration().Type).ShouldBe((byte)0x26);
        ((byte)new ConsoleWritePacket(string.Empty, ConsoleOutput.Standard).Type).ShouldBe((byte)0x3E);
    }

    [Fact]
    public void ClosingAnIdleConnectionStopsItsSender()
    {
        using Process process = Process.GetCurrentProcess();
        using MemoryStream pipe = new();
        using ManualResetEventSlim terminated = new();
        NodePacketFactory factory = new();
        factory.RegisterPacketHandler(NodePacketType.NodeShutdown, NodeShutdown.FactoryForDeserialization, new ShutdownHandler());
        NodeProviderOutOfProcBase.NodeContext context = new(
            1, process, pipe, factory, _ => terminated.Set(), NodePacketTypeExtensions.PacketVersion,
            HandshakeOptions.TaskHost | HandshakeOptions.NET | HandshakeOptions.NodeReuse, connectionPersistsAcrossBuilds: true);

        context.BeginAsyncPacketRead();

        terminated.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
        context.WaitForSendCompletion(10_000).ShouldBeTrue("a closed idle connection must not retain its sender thread");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TerminalNotificationCannotBeLostByHandlerAttachment(bool attachBeforeFailure)
    {
        using Process process = Process.GetCurrentProcess();
        using ControlledReadStream pipe = new();
        using ManualResetEventSlim terminated = new();
        NodeProviderOutOfProcTaskHost provider = (NodeProviderOutOfProcTaskHost)NodeProviderOutOfProcTaskHost.CreateComponent(BuildComponentType.OutOfProcTaskHostNodeProvider);
        provider.InitializeComponent(new MockHost());
        NodeProviderOutOfProcBase.NodeContext context = new(
            1, process, pipe, provider, id => { provider.NodeContextTerminated(id); terminated.Set(); },
            NodePacketTypeExtensions.PacketVersion, connectionPersistsAcrossBuilds: true);
        TaskHostNodeKey key = new(HandshakeOptions.TaskHost | HandshakeOptions.NodeReuse, 1);
        RecordingHandler outer = new();
        RecordingHandler nested = new();
        provider.NodeContextCreated(context, key);

        try
        {
            if (attachBeforeFailure)
            {
                provider.TryAttachTaskHandler(context, outer).ShouldBeTrue();
                provider.TryAttachTaskHandler(context, nested).ShouldBeTrue();
                provider.DisconnectFromHost(context, new RecordingHandler());
            }

            provider.PacketReceived(1, new NodeShutdown(NodeShutdownReason.ConnectionFailed));

            provider.TryAttachTaskHandler(context, new RecordingHandler()).ShouldBeFalse(
                "a terminal notification published before attachment must prevent successful acquisition");
            outer.ShutdownCount.ShouldBe(attachBeforeFailure ? 1 : 0);
            nested.ShutdownCount.ShouldBe(attachBeforeFailure ? 1 : 0);
            provider.TaskHandlerRegistrationCount.ShouldBe(0);
        }
        finally
        {
            pipe.CompleteRead();
            terminated.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            context.WaitForSendCompletion(10_000).ShouldBeTrue();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReusedConnectionWaitsForCleanupOrFailure(bool failConnection)
    {
        using Process process = Process.GetCurrentProcess();
        using ControlledReadStream pipe = new();
        using ManualResetEventSlim terminated = new();
        NodeProviderOutOfProcTaskHost provider = (NodeProviderOutOfProcTaskHost)NodeProviderOutOfProcTaskHost.CreateComponent(BuildComponentType.OutOfProcTaskHostNodeProvider);
        provider.InitializeComponent(new MockHost());
        NodeProviderOutOfProcBase.NodeContext context = new(
            1, process, pipe, provider, id => { provider.NodeContextTerminated(id); terminated.Set(); },
            NodePacketTypeExtensions.PacketVersion, connectionPersistsAcrossBuilds: true);
        TaskHostNodeKey key = new(HandshakeOptions.TaskHost | HandshakeOptions.NodeReuse, 1);
        provider.NodeContextCreated(context, key);
        Task shutdown = Task.Run(() => provider.ShutdownConnectedNodes(enableReuse: true));

        try
        {
            pipe.PacketWritten.Wait(10_000).ShouldBeTrue();
            shutdown.Wait(100).ShouldBeFalse("sending the reset is not proof that disposal finished");

            if (failConnection)
            {
                pipe.CompleteRead();
            }
            else
            {
                provider.PacketReceived(1, new NodeBuildComplete(true, NodeBuildCompleteAction.ReuseWithConnection));
            }

            shutdown.Wait(10_000).ShouldBeTrue();
            provider.ConnectedNodes.ContainsKey(key).ShouldBe(!failConnection);
        }
        finally
        {
            pipe.CompleteRead();
            terminated.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            shutdown.Wait(10_000).ShouldBeTrue();
            context.WaitForSendCompletion(10_000).ShouldBeTrue();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RetiredConnectionCannotAffectItsReplacement(bool previouslyReused)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", null);
        ChangeWaves.ResetStateForTests();
        using Process process = Process.GetCurrentProcess();
        using ControlledReadStream pipe = new();
        using ControlledReadStream replacementPipe = new();
        using ManualResetEventSlim terminated = new();
        using ManualResetEventSlim replacementTerminated = new();
        NodeProviderOutOfProcTaskHost provider = (NodeProviderOutOfProcTaskHost)NodeProviderOutOfProcTaskHost.CreateComponent(BuildComponentType.OutOfProcTaskHostNodeProvider);
        provider.InitializeComponent(new MockHost());
        HandshakeOptions options = CommunicationsUtilities.GetHandshakeOptions(taskHost: true, TaskHostParameters.Empty, nodeReuse: previouslyReused);
        TaskHostNodeKey key = new(options, 1);
        NodeProviderOutOfProcBase.NodeContext context = new(
            1, process, pipe, provider, id => { provider.NodeContextTerminated(id); terminated.Set(); },
            NodePacketTypeExtensions.PacketVersion, options, previouslyReused);
        NodeProviderOutOfProcBase.NodeContext replacement = new(
            2, process, replacementPipe, provider, id => { provider.NodeContextTerminated(id); replacementTerminated.Set(); },
            NodePacketTypeExtensions.PacketVersion, options, previouslyReused);
        provider.NodeContextCreated(context, key);
        RecordingHandler retiringHandler = new();
        RecordingHandler replacementHandler = new();
        provider.TryAttachTaskHandler(context, retiringHandler).ShouldBeTrue();
        Task shutdown = Task.Run(() => provider.ShutdownConnectedNodes(enableReuse: false));
        bool replacementReadStarted = false;

        try
        {
            shutdown.Wait(10_000).ShouldBeTrue("retirement must not wait for the child's disposal");
            pipe.PacketWritten.Wait(10_000).ShouldBeTrue();
            context.WaitForSendCompletion(10_000).ShouldBeTrue();
            provider.ConnectedNodes.ShouldBeEmpty();
            provider.TryAttachTaskHandler(context, new RecordingHandler()).ShouldBeFalse();
            provider.TaskHandlerRegistrationCount.ShouldBe(1, "an aborted task must still receive its connection's terminal notification");
            retiringHandler.ShutdownCount.ShouldBe(0);

            provider.NodeContextCreated(replacement, key);
            replacementReadStarted = true;
            provider.TryAttachTaskHandler(replacement, replacementHandler).ShouldBeTrue();
            provider.PacketReceived(1, new NodeBuildComplete(true, NodeBuildCompleteAction.ReuseWithConnection));
            provider.PacketReceived(1, new NodeShutdown(NodeShutdownReason.Requested));
            pipe.CompleteRead();
            terminated.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            provider.NodeContextTerminated(1);
            retiringHandler.ShutdownCount.ShouldBe(1);
            replacementHandler.ShutdownCount.ShouldBe(0);
            provider.ConnectedNodes[key].ShouldBeSameAs(replacement);
        }
        finally
        {
            if (!replacementReadStarted)
            {
                replacement.BeginAsyncPacketRead();
            }
            pipe.CompleteRead();
            replacementPipe.CompleteRead();
            terminated.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            replacementTerminated.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            context.WaitForSendCompletion(10_000).ShouldBeTrue();
            replacement.WaitForSendCompletion(10_000).ShouldBeTrue();
        }
    }

    [Fact]
    public void OlderTaskHostStillWaitsForShutdown()
    {
        using Process process = Process.GetCurrentProcess();
        using ControlledReadStream pipe = new();
        using ManualResetEventSlim terminated = new();
        NodeProviderOutOfProcTaskHost provider = (NodeProviderOutOfProcTaskHost)NodeProviderOutOfProcTaskHost.CreateComponent(BuildComponentType.OutOfProcTaskHostNodeProvider);
        provider.InitializeComponent(new MockHost());
        HandshakeOptions options = CommunicationsUtilities.GetHandshakeOptions(taskHost: true, TaskHostParameters.Empty, nodeReuse: false);
        NodeProviderOutOfProcBase.NodeContext context = new(
            1, process, pipe, provider, id => { provider.NodeContextTerminated(id); terminated.Set(); }, 5, options);
        provider.NodeContextCreated(context, new TaskHostNodeKey(options, 1));
        Task shutdown = Task.Run(() => provider.ShutdownConnectedNodes(enableReuse: false));

        try
        {
            pipe.PacketWritten.Wait(10_000).ShouldBeTrue();
            shutdown.Wait(100).ShouldBeFalse();
        }
        finally
        {
            pipe.CompleteRead();
            terminated.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            shutdown.Wait(10_000).ShouldBeTrue();
            context.WaitForSendCompletion(10_000).ShouldBeTrue();
        }
    }

    private sealed class RecordingHandler : INodePacketHandler
    {
        public int ShutdownCount { get; private set; }
        public void PacketReceived(int node, INodePacket packet)
        {
            packet.Type.ShouldBe(NodePacketType.NodeShutdown);
            ShutdownCount++;
        }
    }

    private sealed class ControlledReadStream : MemoryStream
    {
        private readonly TaskCompletionSource<int> _read = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _packetWritten = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task PacketWritten => _packetWritten.Task;
        public void CompleteRead() => _read.TrySetResult(0);
        public override void Write(byte[] buffer, int offset, int count) => _packetWritten.TrySetResult(true);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _read.Task;
#if NET
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => new(_read.Task);
#endif
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            _ = _read.Task.ContinueWith(t => callback?.Invoke(t), TaskScheduler.Default);
            return _read.Task;
        }
        public override int EndRead(IAsyncResult asyncResult) => _read.Task.GetAwaiter().GetResult();
    }

    private sealed class ShutdownHandler : INodePacketHandler
    {
        public void PacketReceived(int node, INodePacket packet)
        {
            packet.Type.ShouldBe(NodePacketType.NodeShutdown);
        }
    }
}
