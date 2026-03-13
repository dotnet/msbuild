// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Tests for hash-based pipe naming in NamedPipeUtil and Handshake.ComputeHash.
    /// </summary>
    public class HashBasedPipeNaming_Tests
    {
        #region ComputeHash Tests

        [Fact]
        public void ComputeHash_ReturnsDeterministicValue()
        {
            var handshake = new Handshake(HandshakeOptions.NodeReuse);
            string hash1 = handshake.ComputeHash();
            string hash2 = handshake.ComputeHash();

            hash1.ShouldNotBeNullOrEmpty();
            hash2.ShouldNotBeNullOrEmpty();
            hash1.ShouldBe(hash2);
        }

        [Fact]
        public void ComputeHash_SameOptionsSameHash()
        {
            var h1 = new Handshake(HandshakeOptions.NodeReuse);
            var h2 = new Handshake(HandshakeOptions.NodeReuse);

            h1.ComputeHash().ShouldBe(h2.ComputeHash());
        }

        [Fact]
        public void ComputeHash_DifferentOptionsYieldDifferentHash()
        {
            var h1 = new Handshake(HandshakeOptions.NodeReuse);
            var h2 = new Handshake(HandshakeOptions.None);

            h1.ComputeHash().ShouldNotBe(h2.ComputeHash());
        }

        [Fact]
        public void ComputeHash_NoPaddingOrSlashes()
        {
            var handshake = new Handshake(HandshakeOptions.NodeReuse);
            string hash = handshake.ComputeHash();

            // Hash should be URL/filename-safe: no / or = characters
            hash.ShouldNotContain("/");
            hash.ShouldNotContain("=");
        }

        [Fact]
        public void ComputeHash_IsCached()
        {
            var handshake = new Handshake(HandshakeOptions.NodeReuse);
            string hash1 = handshake.ComputeHash();
            string hash2 = handshake.ComputeHash();

            // Should be the exact same object reference (cached)
            ReferenceEquals(hash1, hash2).ShouldBeTrue();
        }

        #endregion

        #region GetHashBasedPipeName Tests

        [Fact]
        public void GetHashBasedPipeName_ContainsHashAndPid()
        {
            string hash = "abc123";
            int pid = 42;
            string pipeName = NamedPipeUtil.GetHashBasedPipeName(hash, pid);

            pipeName.ShouldContain("MSBuild-abc123-42");
        }

        [Fact]
        public void GetHashBasedPipeName_DefaultsToCurrentPid()
        {
            string hash = "testhash";
            string pipeName = NamedPipeUtil.GetHashBasedPipeName(hash);

            int currentPid = EnvironmentUtilities.CurrentProcessId;
            pipeName.ShouldContain($"MSBuild-testhash-{currentPid}");
        }

        [UnixOnlyFact]
        public void GetHashBasedPipeName_OnUnix_IsAbsolutePath()
        {
            string pipeName = NamedPipeUtil.GetHashBasedPipeName("hash", 123);
            pipeName.ShouldStartWith("/tmp/");
        }

        #endregion

        #region FindNodesByHandshakeHash Tests

        [WindowsOnlyFact]
        public void FindNodesByHandshakeHash_ReturnsEmptyOnWindows()
        {
            var pids = NamedPipeUtil.FindNodesByHandshakeHash("nonexistent");
            pids.ShouldBeEmpty();
        }

        [UnixOnlyFact]
        public void FindNodesByHandshakeHash_FindsMatchingPipeFiles()
        {
            string testHash = $"test-{Guid.NewGuid():N}";

            // Create fake pipe files in /tmp
            string pipe1 = $"/tmp/MSBuild-{testHash}-1001";
            string pipe2 = $"/tmp/MSBuild-{testHash}-1002";
            string pipeOther = $"/tmp/MSBuild-otherhash-9999";

            try
            {
                File.WriteAllText(pipe1, "");
                File.WriteAllText(pipe2, "");
                File.WriteAllText(pipeOther, "");

                var pids = NamedPipeUtil.FindNodesByHandshakeHash(testHash);

                pids.ShouldContain(1001);
                pids.ShouldContain(1002);
                pids.ShouldNotContain(9999);
            }
            finally
            {
                File.Delete(pipe1);
                File.Delete(pipe2);
                File.Delete(pipeOther);
            }
        }

        [UnixOnlyFact]
        public void FindNodesByHandshakeHash_IgnoresMalformedFileNames()
        {
            string testHash = $"test-{Guid.NewGuid():N}";
            string pipeGood = $"/tmp/MSBuild-{testHash}-5555";
            string pipeBad = $"/tmp/MSBuild-{testHash}-notanumber";

            try
            {
                File.WriteAllText(pipeGood, "");
                File.WriteAllText(pipeBad, "");

                var pids = NamedPipeUtil.FindNodesByHandshakeHash(testHash);

                pids.ShouldContain(5555);
                pids.Count.ShouldBe(1);
            }
            finally
            {
                File.Delete(pipeGood);
                File.Delete(pipeBad);
            }
        }

        [UnixOnlyFact]
        public void FindNodesByHandshakeHash_ReturnsEmptyWhenNoMatches()
        {
            var pids = NamedPipeUtil.FindNodesByHandshakeHash($"nopipes-{Guid.NewGuid():N}");
            pids.ShouldBeEmpty();
        }

        #endregion
    }
}
