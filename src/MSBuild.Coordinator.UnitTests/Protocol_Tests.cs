// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Coordinator;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Coordinator.UnitTests;

public class Protocol_Tests
{
    [Fact]
    public void RequestNodes_RoundTrips()
    {
        ClientMessage message = WriteAndReadClientMessage(new RequestNodesMessage(requestedNodes: 16, processId: 12345));
        message.ShouldBe(new RequestNodesMessage(16, 12345));
    }

    [Fact]
    public void ReleaseNodes_RoundTrips()
    {
        ClientMessage message = WriteAndReadClientMessage(ReleaseNodesMessage.Instance);
        message.ShouldBe(ReleaseNodesMessage.Instance);
    }

    [Fact]
    public void Heartbeat_RoundTrips()
    {
        ClientMessage message = WriteAndReadClientMessage(HeartbeatMessage.Instance);
        message.ShouldBe(HeartbeatMessage.Instance);
    }

    [Fact]
    public void NodeGrant_RoundTrips()
    {
        ServerMessage message = WriteAndReadCoordinatorMessage(new NodeGrantMessage(grantedNodes: 4));
        message.ShouldBe(new NodeGrantMessage(4));
    }

    [Fact]
    public void Wait_RoundTrips()
    {
        ServerMessage message = WriteAndReadCoordinatorMessage(WaitMessage.Instance);
        message.ShouldBe(WaitMessage.Instance);
    }

    [Fact]
    public void Error_RoundTrips()
    {
        ServerMessage message = WriteAndReadCoordinatorMessage(new ErrorMessage("something went wrong"));
        message.ShouldBe(new ErrorMessage("something went wrong"));
    }

    [Fact]
    public void MultipleClientMessages_ReadSequentially()
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);

        writer.Write(new RequestNodesMessage(8, 100));
        writer.Write(HeartbeatMessage.Instance);
        writer.Write(ReleaseNodesMessage.Instance);

        stream.Position = 0;
        using BinaryReader reader = new(stream);

        reader.ReadClientMessage().ShouldBe(new RequestNodesMessage(8, 100));
        reader.ReadClientMessage().ShouldBe(HeartbeatMessage.Instance);
        reader.ReadClientMessage().ShouldBe(ReleaseNodesMessage.Instance);
    }

    [Fact]
    public void UnknownClientMessageType_Throws()
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        writer.Write(Protocol.Version);
        writer.Write((byte)99);
        writer.Flush();

        stream.Position = 0;
        using BinaryReader reader = new(stream);

        Should.Throw<InternalErrorException>(() => reader.ReadClientMessage());
    }

    [Fact]
    public void UnknownCoordinatorMessageType_Throws()
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        writer.Write(Protocol.Version);
        writer.Write((byte)99);
        writer.Flush();

        stream.Position = 0;
        using BinaryReader reader = new(stream);

        Should.Throw<InternalErrorException>(() => reader.ReadServerMessage());
    }

    [Fact]
    public void ClientMessage_WrongVersion_Throws()
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        writer.Write((byte)255);
        writer.Write((byte)ClientMessageType.Heartbeat);
        writer.Flush();

        stream.Position = 0;
        using BinaryReader reader = new(stream);

        Should.Throw<InternalErrorException>(() => reader.ReadClientMessage());
    }

    [Fact]
    public void CoordinatorMessage_WrongVersion_Throws()
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        writer.Write((byte)255);
        writer.Write((byte)ServerMessageType.NodeGrant);
        writer.Write(4);
        writer.Flush();

        stream.Position = 0;
        using BinaryReader reader = new(stream);

        Should.Throw<InternalErrorException>(() => reader.ReadServerMessage());
    }

    private static ClientMessage WriteAndReadClientMessage(ClientMessage message)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        writer.Write(message);

        stream.Position = 0;
        using BinaryReader reader = new(stream);
        return reader.ReadClientMessage();
    }

    private static ServerMessage WriteAndReadCoordinatorMessage(ServerMessage message)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        writer.Write(message);

        stream.Position = 0;
        using BinaryReader reader = new(stream);
        return reader.ReadServerMessage();
    }
}
