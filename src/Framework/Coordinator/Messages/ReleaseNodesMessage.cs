// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Framework.Coordinator;

/// <summary>
///  Release a previously granted node allocation.
/// </summary>
internal sealed record ReleaseNodesMessage : ClientMessage
{
    public static ReleaseNodesMessage Instance { get; } = new();

    private ReleaseNodesMessage()
        : base(ClientMessageType.ReleaseNodes)
    {
    }
}
