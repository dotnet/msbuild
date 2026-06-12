// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.Build.Framework.Coordinator;

/// <summary>
///  Request a node grant from the coordinator.
/// </summary>
internal sealed record RequestNodesMessage : ClientMessage
{
    public override ClientMessageType MessageType => ClientMessageType.RequestNodes;

    /// <summary>
    ///  A unique identifier for this connection, used to distinguish clients
    ///  even if OS process IDs are recycled.
    /// </summary>
    public Guid ConnectionId { get; }

    public int RequestedNodes { get; }

    public int ProcessId { get; }

    public RequestNodesMessage(Guid connectionId, int requestedNodes, int processId)
    {
        ConnectionId = connectionId;
        RequestedNodes = requestedNodes;
        ProcessId = processId;
    }

    protected override void WritePayload(BinaryWriter writer)
    {
        writer.WriteGuid(ConnectionId);
        writer.Write(RequestedNodes);
        writer.Write(ProcessId);
    }

    internal static RequestNodesMessage ReadPayload(BinaryReader reader)
        => new(
            connectionId: reader.ReadGuid(),
            requestedNodes: reader.ReadInt32(),
            processId: reader.ReadInt32());
}
