// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Experimental;
using Microsoft.Build.Internal;
using Microsoft.Build.Server;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Tests that the names MSBuild server derives from <see cref="ServerNodeHandshake"/> are scoped
    /// to the current user. Named pipes and named mutexes live in machine-wide namespaces, but the
    /// server pipes are created current-user-only, so user-agnostic names would let one user's server
    /// lock every other user on the machine out of the feature.
    /// </summary>
    public class ServerNodeHandshake_Tests
    {
        [Fact]
        public void GetKeyWithUserName_IncludesTheKeyAndTheCurrentUser()
        {
            ServerNodeHandshake handshake = new(HandshakeOptions.None);

            handshake.GetKeyWithUserName().ShouldContain(handshake.GetKey());
            handshake.GetKeyWithUserName().ShouldContain(Environment.UserName);
        }

        [Fact]
        public void GetKey_ContainsOnlyNumericComponents()
        {
            // Only the derived names carry the user. The handshake exchanged over the wire must not,
            // or it would stop matching other MSBuild versions. The key is purely the numeric
            // handshake components, so no user string can have leaked into it.
            ServerNodeHandshake handshake = new(HandshakeOptions.None);

            handshake.GetKey().ShouldNotBeEmpty();
            handshake.GetKey().ToCharArray().ShouldAllBe(c => char.IsDigit(c) || c == ' ' || c == '-');
        }

        [Fact]
        public void ComputeHash_HashesTheUserScopedKey()
        {
            ServerNodeHandshake handshake = new(HandshakeOptions.None);

            // Hashing through the production HashKey pins which input is hashed without restating
            // the algorithm, so this fails if ComputeHash ever drops back to the bare key.
            handshake.ComputeHash().ShouldNotBe(ServerNodeHandshake.HashKey(handshake.GetKey()));
        }

        [Fact]
        public void ComputeHash_DependsOnTheWholeKey()
        {
            // Guards against the hash ignoring part of its input, which would silently drop the user
            // scoping that GetKeyWithUserName adds.
            ServerNodeHandshake first = new(HandshakeOptions.None);
            ServerNodeHandshake second = new(HandshakeOptions.NodeReuse);

            first.GetKeyWithUserName().ShouldNotBe(second.GetKeyWithUserName());
            first.ComputeHash().ShouldNotBe(second.ComputeHash());
        }

        [Fact]
        public void ComputeHash_IsStableAcrossInstances()
        {
            // A client and the server it launches are separate processes that must agree on the
            // derived names, so the hash has to depend only on the handshake and the user.
            ServerNodeHandshake first = new(HandshakeOptions.None);
            ServerNodeHandshake second = new(HandshakeOptions.None);

            first.ComputeHash().ShouldBe(second.ComputeHash());
        }

        [Fact]
        public void ComputeHash_UsesOnlyCharactersLegalInPipeAndMutexNames()
        {
            // The hash is embedded in '/tmp/...' pipe paths and 'Global\...' mutex names, so path and
            // namespace separators must not survive into it.
            string hash = new ServerNodeHandshake(HandshakeOptions.None).ComputeHash();

            hash.ShouldNotBeEmpty();
            hash.ShouldNotContain("/");
            hash.ShouldNotContain("\\");
            hash.ShouldNotContain("=");
        }

        [Fact]
        public void ServerNames_AreAllDerivedFromTheUserScopedHash()
        {
            ServerNodeHandshake handshake = new(HandshakeOptions.None);
            string hash = handshake.ComputeHash();

            // Every machine-wide name the server uses must carry the user-scoped hash.
            OutOfProcServerNode.GetPipeName(handshake).ShouldContain(hash);
            OutOfProcServerNode.GetRunningServerMutexName(handshake).ShouldContain(hash);
            OutOfProcServerNode.GetBusyServerMutexName(handshake).ShouldContain(hash);
        }
    }
}
