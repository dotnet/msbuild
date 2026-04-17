// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Coordinator;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Coordinator.UnitTests;

public class Protocol_Tests
{
    [Fact]
    public void GetPipeName_ContainsPrefix()
    {
        string pipeName = Protocol.GetPipeName();
        pipeName.ShouldStartWith(Protocol.PipeNamePrefix);
    }

    [Fact]
    public void GetPipeName_ContainsUserName()
    {
        string pipeName = Protocol.GetPipeName();
        pipeName.ShouldContain(Environment.UserName);
    }

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

        new RequestNodesMessage(8, 100).Write(writer);
        HeartbeatMessage.Instance.Write(writer);
        ReleaseNodesMessage.Instance.Write(writer);

        stream.Position = 0;
        using BinaryReader reader = new(stream);

        ClientMessage.Read(reader).ShouldBe(new RequestNodesMessage(8, 100));
        ClientMessage.Read(reader).ShouldBe(HeartbeatMessage.Instance);
        ClientMessage.Read(reader).ShouldBe(ReleaseNodesMessage.Instance);
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

        Should.Throw<InternalErrorException>(() => ClientMessage.Read(reader));
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

        Should.Throw<InternalErrorException>(() => ServerMessage.Read(reader));
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

        Should.Throw<InternalErrorException>(() => ClientMessage.Read(reader));
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

        Should.Throw<InternalErrorException>(() => ServerMessage.Read(reader));
    }

    private static ClientMessage WriteAndReadClientMessage(ClientMessage message)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        message.Write(writer);

        stream.Position = 0;
        using BinaryReader reader = new(stream);
        return ClientMessage.Read(reader);
    }

    private static ServerMessage WriteAndReadCoordinatorMessage(ServerMessage message)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        message.Write(writer);

        stream.Position = 0;
        using BinaryReader reader = new(stream);
        return ServerMessage.Read(reader);
    }
}
