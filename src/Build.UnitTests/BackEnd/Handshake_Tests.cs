// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    /// <summary>
    /// Tests that the change wave a process resolved participates in the handshake. A node caches its
    /// change wave for its whole lifetime, so a node reused by a later build would otherwise keep
    /// applying the change wave of the build that started it.
    /// </summary>
    public sealed class Handshake_Tests : IDisposable
    {
        private const string DisableFeaturesFromVersion = "MSBUILDDISABLEFEATURESFROMVERSION";

        private static readonly HandshakeOptions s_workerNodeOptions = CommunicationsUtilities.GetHandshakeOptions(
            taskHost: false,
            taskHostParameters: TaskHostParameters.Empty);

        private readonly ITestOutputHelper _output;

        public Handshake_Tests(ITestOutputHelper output) => _output = output;

        /// <summary>
        /// The change wave is cached statically, so drop what these tests cached once the environment
        /// variable has been restored.
        /// </summary>
        public void Dispose() => ChangeWaves.ResetStateForTests();

        /// <summary>
        /// Change waves are cached statically, so the cache has to be dropped whenever the environment
        /// variable driving it changes.
        /// </summary>
        private static void SetChangeWave(string? wave, TestEnvironment env)
        {
            ChangeWaves.ResetStateForTests();
            env.SetEnvironmentVariable(DisableFeaturesFromVersion, wave);
        }

        private static string GetKeyForChangeWave(string? wave, TestEnvironment env, HandshakeOptions options)
        {
            SetChangeWave(wave, env);

            return new Handshake(options).GetKey();
        }

        [Fact]
        public void DifferentChangeWavesProduceDifferentHandshakes()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            string firstKey = GetKeyForChangeWave(ChangeWaves.LowestWave.ToString(), env, s_workerNodeOptions);
            string secondKey = GetKeyForChangeWave(ChangeWaves.HighestWave.ToString(), env, s_workerNodeOptions);

            secondKey.ShouldNotBe(firstKey);
        }

        [Fact]
        public void SameChangeWaveProducesTheSameHandshake()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            string firstKey = GetKeyForChangeWave(ChangeWaves.LowestWave.ToString(), env, s_workerNodeOptions);
            string secondKey = GetKeyForChangeWave(ChangeWaves.LowestWave.ToString(), env, s_workerNodeOptions);

            secondKey.ShouldBe(firstKey);
        }

        /// <summary>
        /// Values that resolve to the same change wave have to keep producing the same handshake, otherwise
        /// nodes that behave identically would needlessly refuse to talk to each other.
        /// </summary>
        [Theory]
        [InlineData("")]
        [InlineData("999.999")]
        [InlineData("not a version")]
        public void EquivalentChangeWaveValuesProduceTheSameHandshake(string wave)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            string unsetKey = GetKeyForChangeWave(null, env, s_workerNodeOptions);
            string equivalentKey = GetKeyForChangeWave(wave, env, s_workerNodeOptions);

            equivalentKey.ShouldBe(unsetKey);
        }

        /// <summary>
        /// MSBuild Server clients find their server by a pipe name derived from the handshake, so a server
        /// that resolved a different change wave must not be reused.
        /// </summary>
        [Fact]
        public void DifferentChangeWavesProduceDifferentServerNodeHandshakes()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            HandshakeOptions options = CommunicationsUtilities.GetHandshakeOptions(
                taskHost: false,
                taskHostParameters: TaskHostParameters.Empty,
                architectureFlagToSet: XMakeAttributes.GetCurrentMSBuildArchitecture());

            SetChangeWave(ChangeWaves.LowestWave.ToString(), env);
            string firstHash = new ServerNodeHandshake(options).ComputeHash();

            SetChangeWave(ChangeWaves.HighestWave.ToString(), env);
            string secondHash = new ServerNodeHandshake(options).ComputeHash();

            secondHash.ShouldNotBe(firstHash);
        }

        /// <summary>
        /// A task host connection is the one place where the two ends can be different MSBuild versions, and
        /// the resolved wave is version-relative: <see cref="ChangeWaves.DisabledWave"/> clamps and rounds the
        /// environment variable into that binary's own wave list. The CLR2 task host additionally computes its
        /// handshake from a separate legacy copy of this code that knows nothing about change waves. Unlike a
        /// worker node, a task host mismatch cannot be recovered from by starting a different host, so no task
        /// host may take the change wave into account.
        /// </summary>
        [Theory]
        [InlineData((int)(HandshakeOptions.TaskHost | HandshakeOptions.CLR2))]
        [InlineData((int)(HandshakeOptions.TaskHost | HandshakeOptions.NET))]
        [InlineData((int)(HandshakeOptions.TaskHost | HandshakeOptions.NET | HandshakeOptions.X64))]
        [InlineData((int)(HandshakeOptions.TaskHost | HandshakeOptions.NET | HandshakeOptions.SidecarTaskHost))]
        [InlineData((int)HandshakeOptions.TaskHost)]
        public void TaskHostHandshakeIgnoresChangeWave(int taskHostOptions)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            var options = (HandshakeOptions)taskHostOptions;

            string firstKey = GetKeyForChangeWave(ChangeWaves.LowestWave.ToString(), env, options);
            string secondKey = GetKeyForChangeWave(ChangeWaves.HighestWave.ToString(), env, options);

            secondKey.ShouldBe(firstKey);
        }

        /// <summary>
        /// The .NET task host is the connection at risk: a .NET Framework parent (e.g. Visual Studio) talks to
        /// a child taken from the installed .NET SDK, which resolves the same environment variable against a
        /// different wave list. Its handshake has to stay identical to what a version without change wave
        /// salting computes, otherwise the build fails with MSB4216 and the parent can only relaunch the same
        /// SDK binary rather than start a compatible host.
        /// </summary>
        [Fact]
        public void NetTaskHostHandshakeIsUnaffectedByChangeWave()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            HandshakeOptions options = HandshakeOptions.TaskHost | HandshakeOptions.NET;

            string unsetKey = GetKeyForChangeWave(null, env, options);
            string waveSetKey = GetKeyForChangeWave(ChangeWaves.LowestWave.ToString(), env, options);

            waveSetKey.ShouldBe(unsetKey);
        }
    }
}
