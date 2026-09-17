// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Internal;

/// <summary>
///  An aggregate class for passing around results of a handshake and adjacent information.
///  ErrorMessage is to propagate error messages where necessary
/// </summary> 
internal class HandshakeResult
{
    /// <summary>
    ///  Gets the status code indicating the result of the handshake operation.
    /// </summary>
    public HandshakeStatus Status { get; }

    /// <summary>
    ///  Handshake in MSBuild is performed as passing integers back and forth.
    ///  This field holds the value returned from a successful handshake step.
    /// </summary>
    public int Value { get; }

    /// <summary>
    ///  Gets the error message when a handshake operation fails.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    ///  The negotiated packet version with the child node.
    ///  It's needed to ensure both sides of the communication can read/write data in pipe.
    /// </summary>
    public byte NegotiatedPacketVersion { get; }

    /// <summary>
    ///  Initializes a new instance of the <see cref="HandshakeResult"/> class.
    /// </summary>
    /// <param name="status">The status of the handshake operation.</param>
    /// <param name="value">The value returned from the handshake.</param>
    /// <param name="errorMessage">The error message if the handshake failed.</param>
    /// <param name="negotiatedPacketVersion">The packet version from the child node.</param>
    private HandshakeResult(HandshakeStatus status, int value, string? errorMessage, byte negotiatedPacketVersion = 1)
    {
        Status = status;
        Value = value;
        ErrorMessage = errorMessage;
        NegotiatedPacketVersion = negotiatedPacketVersion;
    }

    /// <summary>
    ///  Creates a successful handshake result with the specified value.
    /// </summary>
    /// <param name="value">The value returned from the handshake operation.</param>
    /// <param name="negotiatedPacketVersion">The packet version received from the child node.</param>
    /// <returns>
    ///  A new <see cref="HandshakeResult"/> instance representing a successful operation.
    /// </returns>
    public static HandshakeResult Success(int value = 0, byte negotiatedPacketVersion = 1)
        => new(HandshakeStatus.Success, value, errorMessage: null, negotiatedPacketVersion);

    /// <summary>
    ///  Creates a failed handshake result with the specified status and error message.
    /// </summary>
    /// <param name="status">The error status code for the failure.</param>
    /// <param name="errorMessage">A description of the error that occurred.</param>
    /// <returns>
    ///  A new <see cref="HandshakeResult"/> instance representing a failed operation.
    /// </returns>
    public static HandshakeResult Failure(HandshakeStatus status, string errorMessage)
        => new(status, 0, errorMessage);
}
