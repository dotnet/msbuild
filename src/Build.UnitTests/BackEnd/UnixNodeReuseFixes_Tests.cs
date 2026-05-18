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
        [UnixOnlyFact]
        public void Handshake_OnUnix_SessionIdIsZero()
        {
            var handshake = new Handshake(HandshakeOptions.NodeReuse);

            // Use the structured accessor rather than parsing the key string
            handshake.RetrieveHandshakeComponents().SessionId.ShouldBe(0,
                "Unix handshake SessionId should be 0 to enable cross-terminal node reuse");
        }

        [UnixOnlyFact]
        public void Handshake_OnUnix_KeyIsDeterministic()
        {
            // Two handshakes with same options produce identical keys on Unix
            // because SessionId is always 0 (not terminal-specific).
            // Note: this runs in a single process, so it validates determinism
            // rather than cross-terminal behavior (which requires integration testing).
            var h1 = new Handshake(HandshakeOptions.NodeReuse);
            var h2 = new Handshake(HandshakeOptions.NodeReuse);

            h1.GetKey().ShouldBe(h2.GetKey());
        }
    }
}

#endif
