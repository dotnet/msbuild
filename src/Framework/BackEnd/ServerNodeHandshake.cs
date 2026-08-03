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

    public override byte? ExpectedVersionInFirstByte => null;

    internal ServerNodeHandshake(HandshakeOptions nodeType)
        : base(nodeType, includeSessionId: false, toolsDirectory: null)
    {
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
    /// The handshake key plus the current user. The pipe and mutex names derived from
    /// <see cref="ComputeHash"/> are machine-wide while the pipes are current-user-only, so without
    /// the user one account's server locks every other account on the machine out.
    /// </summary>
    public string GetKeyWithUserName() => $"{GetKey()} {EnvironmentUtilities.CurrentUserName}";

    /// <summary>
    /// Computes Handshake stable hash string representing whole state of handshake.
    /// </summary>
    public string ComputeHash() => _computedHash ??= HashKey(GetKeyWithUserName());

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
