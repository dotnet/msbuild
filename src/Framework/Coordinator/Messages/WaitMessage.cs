// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Framework.Coordinator;

/// <summary>
///  Indicates the client should wait for a grant.
/// </summary>
internal sealed record WaitMessage : ServerMessage
{
    public static WaitMessage Instance { get; } = new();

    private WaitMessage()
        : base(ServerMessageType.Wait)
    {
    }
}
