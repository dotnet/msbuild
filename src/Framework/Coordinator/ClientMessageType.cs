// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Framework.Coordinator;

/// <summary>
///  Message types sent from an MSBuild client to the coordinator.
/// </summary>
internal enum ClientMessageType : byte
{
    /// <summary>
    ///  Request a node grant. Payload: int requestedNodes, int pid.
    /// </summary>
    RequestNodes = 1,

    /// <summary>
    ///  Release a previously granted node allocation. No payload.
    /// </summary>
    ReleaseNodes = 2,

    /// <summary>
    ///  Heartbeat indicating the build is still active. No payload.
    /// </summary>
    Heartbeat = 3,
}
