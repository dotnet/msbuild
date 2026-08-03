// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Framework.Coordinator;

/// <summary>
///  Heartbeat indicating the build is still active.
/// </summary>
internal sealed record HeartbeatMessage : ClientMessage
{
    public static HeartbeatMessage Instance { get; } = new();

    private HeartbeatMessage()
        : base(ClientMessageType.Heartbeat)
    {
    }
}
