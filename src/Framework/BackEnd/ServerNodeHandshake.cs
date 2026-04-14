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
        FrameworkCommunicationsUtilities.AvoidEndOfHandshakeSignal(_handshakeComponents.Options),
        FrameworkCommunicationsUtilities.AvoidEndOfHandshakeSignal(_handshakeComponents.Salt),
        FrameworkCommunicationsUtilities.AvoidEndOfHandshakeSignal(_handshakeComponents.FileVersionMajor),
        FrameworkCommunicationsUtilities.AvoidEndOfHandshakeSignal(_handshakeComponents.FileVersionMinor),
        FrameworkCommunicationsUtilities.AvoidEndOfHandshakeSignal(_handshakeComponents.FileVersionBuild),
        FrameworkCommunicationsUtilities.AvoidEndOfHandshakeSignal(_handshakeComponents.FileVersionPrivate));

    public override string GetKey() => $"{_handshakeComponents.Options} {_handshakeComponents.Salt} {_handshakeComponents.FileVersionMajor} {_handshakeComponents.FileVersionMinor} {_handshakeComponents.FileVersionBuild} {_handshakeComponents.FileVersionPrivate}"
        .ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Computes Handshake stable hash string representing whole state of handshake.
    /// </summary>
    public string ComputeHash()
    {
        if (_computedHash == null)
        {
            var input = GetKey();
            byte[] utf8 = Encoding.UTF8.GetBytes(input);
#if NET
            Span<byte> bytes = stackalloc byte[SHA256.HashSizeInBytes];
            SHA256.HashData(utf8, bytes);
#else
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(utf8);
#endif
            _computedHash = Convert.ToBase64String(bytes)
                .Replace("/", "_")
                .Replace("=", string.Empty);
        }

        return _computedHash;
    }
}
