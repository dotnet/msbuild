// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Internal;

internal sealed class ServerNodeHandshake : Handshake
{
    /// <summary>
    /// Caching computed hash.
    /// </summary>
    private string? _computedHash = null;

    /// <summary>
    /// Distinguishes one transient server from every other server, or <see langword="null"/> for the
    /// resident server.
    /// </summary>
    /// <remarks>
    /// This participates in <see cref="ComputeHash"/>, and therefore in the pipe and mutex names, but
    /// deliberately not in <see cref="RetrieveHandshakeComponents"/>: the handshake itself answers
    /// "are we compatible", while the hash answers "which server am I talking to". A transient server
    /// exists to serve exactly one build and is then torn down, so it must not be reachable by any
    /// other client - otherwise that client can order it to shut down mid-build, or find itself
    /// connected to one that is already exiting.
    /// </remarks>
    private readonly string? _instanceId;

    public override byte? ExpectedVersionInFirstByte => null;

    internal ServerNodeHandshake(HandshakeOptions nodeType, string? instanceId = null)
        : base(nodeType, includeSessionId: false, toolsDirectory: null)
    {
        _instanceId = instanceId;
    }

    public override HandshakeComponents RetrieveHandshakeComponents() => new(
        CommunicationsUtilities.AvoidEndOfHandshakeSignal(_handshakeComponents.Options),
        CommunicationsUtilities.AvoidEndOfHandshakeSignal(_handshakeComponents.Salt),
        CommunicationsUtilities.AvoidEndOfHandshakeSignal(_handshakeComponents.FileVersionMajor),
        CommunicationsUtilities.AvoidEndOfHandshakeSignal(_handshakeComponents.FileVersionMinor),
        CommunicationsUtilities.AvoidEndOfHandshakeSignal(_handshakeComponents.FileVersionBuild),
        CommunicationsUtilities.AvoidEndOfHandshakeSignal(_handshakeComponents.FileVersionPrivate));

    public override string GetKey() => $"{_handshakeComponents.Options} {_handshakeComponents.Salt} {_handshakeComponents.FileVersionMajor} {_handshakeComponents.FileVersionMinor} {_handshakeComponents.FileVersionBuild} {_handshakeComponents.FileVersionPrivate}"
        .ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// The handshake key plus the current user, and the instance id when this is a transient server.
    /// The pipe and mutex names derived from <see cref="ComputeHash"/> are machine-wide while the
    /// pipes are current-user-only, so without the user one account's server locks every other
    /// account on the machine out.
    /// </summary>
    public string GetKeyWithEndpointIdentity() => _instanceId is null
        ? $"{GetKey()} {EnvironmentUtilities.CurrentUserName}"
        : $"{GetKey()} {EnvironmentUtilities.CurrentUserName} {_instanceId}";

    /// <summary>
    /// Computes Handshake stable hash string representing whole state of handshake.
    /// </summary>
    public string ComputeHash() => _computedHash ??= HashKey(GetKeyWithEndpointIdentity());

    public static string HashKey(string key)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(key);
#if NET
        Span<byte> bytes = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(utf8, bytes);
#else
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(utf8);
#endif

        return Convert.ToBase64String(bytes)
            .Replace("/", "_")
            .Replace("=", string.Empty);
    }
}
