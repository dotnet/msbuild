// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;

namespace Microsoft.Build.Framework.Coordinator;

/// <summary>
///  Request a node grant from the coordinator.
/// </summary>
internal sealed record RequestNodesMessage : ClientMessage
{
    public override ClientMessageType MessageType => ClientMessageType.RequestNodes;

    public int RequestedNodes { get; }

    public int ProcessId { get; }

    public RequestNodesMessage(int requestedNodes, int processId)
    {
        RequestedNodes = requestedNodes;
        ProcessId = processId;
    }

    protected override void WritePayload(BinaryWriter writer)
    {
        writer.Write(RequestedNodes);
        writer.Write(ProcessId);
    }
}
