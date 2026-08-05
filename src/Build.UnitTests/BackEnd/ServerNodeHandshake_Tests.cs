// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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

        [Fact]
        public void ComputeHash_IsUnaffectedByAnAbsentInstanceId()
        {
            // The resident server passes no instance id, so its endpoint must be exactly what it was
            // before transient servers became addressable separately.
            ServerNodeHandshake withoutArgument = new(HandshakeOptions.None);
            ServerNodeHandshake withExplicitNull = new(HandshakeOptions.None, instanceId: null);

            withExplicitNull.ComputeHash().ShouldBe(withoutArgument.ComputeHash());
        }

        [Fact]
        public void ComputeHash_SeparatesTransientServersFromEachOtherAndFromTheResident()
        {
            // A transient server serves one build and is then torn down, so no other client may be
            // able to reach it: otherwise it can be ordered to shut down mid-build, and concurrent
            // transient builds contend for one set of names instead of getting their own servers.
            ServerNodeHandshake resident = new(HandshakeOptions.None);
            ServerNodeHandshake first = new(HandshakeOptions.None, instanceId: "instance-one");
            ServerNodeHandshake second = new(HandshakeOptions.None, instanceId: "instance-two");

            first.ComputeHash().ShouldNotBe(resident.ComputeHash());
            second.ComputeHash().ShouldNotBe(resident.ComputeHash());
            first.ComputeHash().ShouldNotBe(second.ComputeHash());
        }

        [Fact]
        public void ComputeHash_IsStableAcrossInstancesForTheSameInstanceId()
        {
            // The client and the transient server it launches are separate processes that must derive
            // the same names from the id passed on the command line.
            ServerNodeHandshake client = new(HandshakeOptions.None, instanceId: "shared-id");
            ServerNodeHandshake server = new(HandshakeOptions.None, instanceId: "shared-id");

            client.ComputeHash().ShouldBe(server.ComputeHash());
        }

        [Fact]
        public void GetKey_IsUnaffectedByTheInstanceId()
        {
            // The instance id scopes only the derived names. The handshake exchanged over the wire
            // answers "are we compatible", so putting the id in it would be a wire-format change.
            ServerNodeHandshake resident = new(HandshakeOptions.None);
            ServerNodeHandshake transientServer = new(HandshakeOptions.None, instanceId: "instance-one");

            transientServer.GetKey().ShouldBe(resident.GetKey());
        }

        [Fact]
        public void ServerNames_AllDifferBetweenTransientServers()
        {
            ServerNodeHandshake first = new(HandshakeOptions.None, instanceId: "instance-one");
            ServerNodeHandshake second = new(HandshakeOptions.None, instanceId: "instance-two");

            // Every name two transient servers could collide on has to be distinct, not just the pipe:
            // sharing either mutex would serialize them or make one refuse to start.
            OutOfProcServerNode.GetPipeName(first).ShouldNotBe(OutOfProcServerNode.GetPipeName(second));
            OutOfProcServerNode.GetRunningServerMutexName(first).ShouldNotBe(OutOfProcServerNode.GetRunningServerMutexName(second));
            OutOfProcServerNode.GetBusyServerMutexName(first).ShouldNotBe(OutOfProcServerNode.GetBusyServerMutexName(second));
        }
    }
}
