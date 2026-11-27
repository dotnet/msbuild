// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.BackEnd;

/// <summary>
/// Reasons why the node shut down.
/// </summary>
internal enum NodeShutdownReason
{
    /// <summary>
    /// The node shut down because it was requested to shut down.
    /// </summary>
    Requested,

    /// <summary>
    /// The node shut down because of an error.
    /// </summary>
    Error,

    /// <summary>
    /// The node shut down because the connection failed.
    /// </summary>
    ConnectionFailed,
}
