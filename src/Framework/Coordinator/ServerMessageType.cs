// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Framework.Coordinator;

/// <summary>
///  Message types sent from the coordinator to an MSBuild client.
/// </summary>
internal enum ServerMessageType : byte
{
    /// <summary>
    ///  A node grant. Payload: int granted Nodes.
    /// </summary>
    NodeGrant = 128,

    /// <summary>
    ///  Indicates the client should wait for a grant. No payload.
    /// </summary>
    Wait = 129,

    /// <summary>
    ///  An error occurred. Payload: string message.
    /// </summary>
    Error = 130,
}
