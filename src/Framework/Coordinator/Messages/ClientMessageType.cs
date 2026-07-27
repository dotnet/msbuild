// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Framework.Coordinator;

/// <summary>
///  Message types sent from an MSBuild client to the coordinator.
/// </summary>
internal enum ClientMessageType : byte
{
    /// <summary>
    ///  Handshake message. Payload: Guid connectionId, int pid, string[] capabilities.
    /// </summary>
    Handshake = 1,

    /// <summary>
    ///  Request a node grant. Payload: int requestedNodes.
    /// </summary>
    RequestNodes = 2,

    /// <summary>
    ///  Release a previously granted node allocation. No payload.
    /// </summary>
    ReleaseNodes = 3,

    /// <summary>
    ///  Heartbeat indicating the build is still active. No payload.
    /// </summary>
    Heartbeat = 4,

    /// <summary>
    ///  Join an existing node grant. Payload: Guid grantId, int requestedNodes.
    /// </summary>
    JoinGrant = 5,
}
