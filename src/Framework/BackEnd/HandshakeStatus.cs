// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Internal;

/// <summary>
///  Status codes for the handshake process.
///  It aggregates return values across several functions so we use an aggregate instead of a separate class for each method.
/// </summary>
internal enum HandshakeStatus
{
    /// <summary>
    ///  The handshake operation completed successfully.
    /// </summary>
    Success = 0,

    /// <summary>
    ///  The other node returned a different value than expected.
    ///  This can happen either by attempting to connect to a wrong node type 
    ///  (e.g., transient TaskHost trying to connect to a long-running TaskHost)
    ///  or by trying to connect to a node that has a different MSBuild version.
    /// </summary>
    VersionMismatch = 1,

    /// <summary>
    ///  The handshake was aborted due to connection from an old MSBuild version.
    ///  Occurs in TryReadInt when detecting legacy MSBuild.exe connections.
    /// </summary>
    OldMSBuild = 2,

    /// <summary>
    ///  The handshake operation timed out before completion.
    /// </summary>
    Timeout = 3,

    /// <summary>
    ///  The stream ended unexpectedly during the handshake operation.
    ///  Indicates an incomplete or corrupted handshake sequence.
    /// </summary>
    UnexpectedEndOfStream = 4,

    /// <summary>
    ///  The endianness (byte order) of the communicating nodes does not match.
    ///  Indicates an architecture compatibility issue.
    /// </summary>
    EndiannessMismatch = 5,

    /// <summary>
    ///  The handshake status is undefined or uninitialized.
    /// </summary>
    Undefined,
}
