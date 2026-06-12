// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Coordinator;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Coordinator.UnitTests;

public class Message_Tests
{
    [Fact]
    public void Handshake_RoundTrips()
    {
        Guid connectionId = Guid.NewGuid();
        ClientMessage message = WriteAndReadClientMessage(
            new ClientHandshakeMessage(connectionId, processId: 12345, capabilities: ["priority", "dynamic-grants"]));

        ClientHandshakeMessage result = message.ShouldBeOfType<ClientHandshakeMessage>();
        result.ConnectionId.ShouldBe(connectionId);
        result.Capabilities.ShouldBe(["priority", "dynamic-grants"]);
    }

    [Fact]
    public void Handshake_EmptyCapabilities_RoundTrips()
    {
        Guid connectionId = Guid.NewGuid();
        ClientMessage message = WriteAndReadClientMessage(new ClientHandshakeMessage(connectionId, processId: 12345, []));

        ClientHandshakeMessage result = message.ShouldBeOfType<ClientHandshakeMessage>();
        result.ConnectionId.ShouldBe(connectionId);
        result.Capabilities.ShouldBeEmpty();
    }

    [Fact]
    public void HandshakeResponse_RoundTrips()
    {
        ServerMessage message = WriteAndReadServerMessage(new ServerHandshakeMessage(capabilities: ["priority"]));

        ServerHandshakeMessage result = message.ShouldBeOfType<ServerHandshakeMessage>();
        result.Capabilities.ShouldBe(["priority"]);
    }

    [Fact]
    public void HandshakeResponse_EmptyCapabilities_RoundTrips()
    {
        ServerMessage message = WriteAndReadServerMessage(new ServerHandshakeMessage([]));

        ServerHandshakeMessage result = message.ShouldBeOfType<ServerHandshakeMessage>();
        result.Capabilities.ShouldBeEmpty();
    }

    [Fact]
    public void RequestNodes_RoundTrips()
    {
        ClientMessage message = WriteAndReadClientMessage(new RequestNodesMessage(requestedNodes: 16));
        message.ShouldBe(new RequestNodesMessage(requestedNodes: 16));
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
        ServerMessage message = WriteAndReadServerMessage(new NodeGrantMessage(grantedNodes: 4));
        message.ShouldBe(new NodeGrantMessage(4));
    }

    [Fact]
    public void Wait_RoundTrips()
    {
        ServerMessage message = WriteAndReadServerMessage(WaitMessage.Instance);
        message.ShouldBe(WaitMessage.Instance);
    }

    [Fact]
    public void Error_RoundTrips()
    {
        ServerMessage message = WriteAndReadServerMessage(new ErrorMessage("something went wrong"));
        message.ShouldBe(new ErrorMessage("something went wrong"));
    }

    [Fact]
    public void MultipleClientMessages_ReadSequentially()
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);

        writer.Write(new ClientHandshakeMessage(Guid.NewGuid(), processId: 12345, []));
        writer.Write(new RequestNodesMessage(requestedNodes: 8));
        writer.Write(HeartbeatMessage.Instance);
        writer.Write(ReleaseNodesMessage.Instance);

        stream.Position = 0;
        using BinaryReader reader = new(stream);

        reader.ReadClientMessage().ShouldBeOfType<ClientHandshakeMessage>();
        reader.ReadClientMessage().ShouldBe(new RequestNodesMessage(requestedNodes: 8));
        reader.ReadClientMessage().ShouldBe(HeartbeatMessage.Instance);
        reader.ReadClientMessage().ShouldBe(ReleaseNodesMessage.Instance);
    }

    [Fact]
    public void UnknownClientMessageType_Throws()
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        writer.Write((byte)99);
        writer.Flush();

        stream.Position = 0;
        using BinaryReader reader = new(stream);

        Should.Throw<InternalErrorException>(() => reader.ReadClientMessage());
    }

    [Fact]
    public void UnknownServerMessageType_Throws()
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        writer.Write((byte)99);
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

    private static ServerMessage WriteAndReadServerMessage(ServerMessage message)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        writer.Write(message);

        stream.Position = 0;
        using BinaryReader reader = new(stream);
        return reader.ReadServerMessage();
    }
}
