// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BackEnd;

/// <summary>
/// The connection status of a link between the NodeEndpoint on the host and the NodeEndpoint
/// on the peer.
/// </summary>
internal enum LinkStatus
{
    /// <summary>
    /// The connection has never been started.
    /// </summary>
    Inactive,

    /// <summary>
    /// The connection is active, the most recent data has been successfully sent, and the
    /// node is responding to pings.
    /// </summary>
    Active,

    /// <summary>
    /// The connection has failed and been terminated.
    /// </summary>
    Failed,

    /// <summary>
    /// The connection could not be made/timed out.
    /// </summary>
    ConnectionFailed,
}
