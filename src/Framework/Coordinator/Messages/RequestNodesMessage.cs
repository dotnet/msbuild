// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;

namespace Microsoft.Build.Framework.Coordinator;

/// <summary>
///  Request a node grant from the coordinator.
/// </summary>
internal sealed record RequestNodesMessage : ClientMessage
{
    public int RequestedNodes { get; }

    public RequestNodesMessage(int requestedNodes)
        : base(ClientMessageType.RequestNodes)
    {
        RequestedNodes = requestedNodes;
    }

    protected override void WritePayload(BinaryWriter writer)
    {
        writer.Write(RequestedNodes);
    }

    internal static RequestNodesMessage ReadPayload(BinaryReader reader)
    {
        int requestedNodes = reader.ReadInt32();

        return new(requestedNodes: requestedNodes);
    }
}
