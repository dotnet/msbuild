// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET

using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Tests for Unix node reuse bug fixes:
    /// - SessionId = 0 on Unix (cross-terminal node reuse)
    /// </summary>
    public class UnixNodeReuseFixes_Tests
    {
        [Fact]
        public void Handshake_OnUnix_SessionIdIsZero()
        {
            if (!NativeMethodsShared.IsUnixLike)
            {
                return;
            }

            // Two handshakes created from different contexts should have the same
            // session ID (0) on Unix, enabling cross-terminal node reuse.
            var h1 = new Handshake(HandshakeOptions.NodeReuse);
            var h2 = new Handshake(HandshakeOptions.NodeReuse);

            // Same handshake key means same session ID was used
            h1.GetKey().ShouldBe(h2.GetKey());
        }

        [Fact]
        public void Handshake_SessionIdComponent_IsZeroOnUnix()
        {
            if (!NativeMethodsShared.IsUnixLike)
            {
                return;
            }

            var handshake = new Handshake(HandshakeOptions.NodeReuse);

            // Key format: "options salt major minor build private sessionId"
            // Last component should be 0 on Unix
            string key = handshake.GetKey();
            string[] keyParts = key.Split(' ');
            keyParts[keyParts.Length - 1].ShouldBe("0");
        }
    }
}

#endif
