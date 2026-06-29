// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Framework.Coordinator;

/// <summary>
///  Message types sent from the coordinator to an MSBuild client.
/// </summary>
internal enum ServerMessageType : byte
{
    /// <summary>
    ///  Handshake response. Payload: string[] capabilities.
    /// </summary>
    HandshakeResponse = 1,

    /// <summary>
    ///  A node grant. Payload: int grantedNodes.
    /// </summary>
    NodeGrant = 2,

    /// <summary>
    ///  Indicates the client should wait for a grant. No payload.
    /// </summary>
    Wait = 3,

    /// <summary>
    ///  An error occurred. Payload: string message.
    /// </summary>
    Error = 4,

    /// <summary>
    ///  A node grant with an associated grant token. Payload: Guid grantId, int grantedNodes.
    /// </summary>
    NodeGrantWithId = 5,
}
