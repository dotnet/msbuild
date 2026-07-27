// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Build.Experimental;
using Microsoft.Build.Internal;
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
        public void ComputeHash_IncludesCurrentUser()
        {
            ServerNodeHandshake handshake = new(HandshakeOptions.None);

            // The user name must participate in the hash, otherwise every user on the machine
            // computes the same pipe and mutex names.
            handshake.GetKeyWithUserName().ShouldBe($"{handshake.GetKey()} {Environment.UserName}");
            handshake.ComputeHash().ShouldBe(Hash(handshake.GetKeyWithUserName()));
            handshake.ComputeHash().ShouldNotBe(Hash(handshake.GetKey()));
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
        public void ComputeHash_DoesNotChangeHandshakeSentOverTheWire()
        {
            // The user discriminator is a naming concern only. Adding it to the wire handshake would
            // be a protocol change, so the exchanged components must stay derived from the key alone.
            ServerNodeHandshake handshake = new(HandshakeOptions.None);

            HandshakeComponents components = handshake.RetrieveHandshakeComponents();

            components.Options.ShouldBe(CommunicationsUtilities.AvoidEndOfHandshakeSignal(components.Options));
            handshake.GetKey().ShouldNotContain(Environment.UserName, Case.Insensitive);
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

        private static string Hash(string input)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(input);
#if NET
            byte[] bytes = SHA256.HashData(utf8);
#else
            using SHA256 sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(utf8);
#endif

            return Convert.ToBase64String(bytes)
                .Replace("/", "_")
                .Replace("=", string.Empty);
        }
    }
}
