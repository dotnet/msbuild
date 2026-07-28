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

            string firstKey = GetKeyForChangeWave(ChangeWaves.Wave17_12.ToString(), env, s_workerNodeOptions);
            string secondKey = GetKeyForChangeWave(ChangeWaves.HighestWave.ToString(), env, s_workerNodeOptions);

            secondKey.ShouldNotBe(firstKey);
        }

        [Fact]
        public void SameChangeWaveProducesTheSameHandshake()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            string firstKey = GetKeyForChangeWave(ChangeWaves.Wave17_12.ToString(), env, s_workerNodeOptions);
            string secondKey = GetKeyForChangeWave(ChangeWaves.Wave17_12.ToString(), env, s_workerNodeOptions);

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

            SetChangeWave(ChangeWaves.Wave17_12.ToString(), env);
            string firstHash = new ServerNodeHandshake(options).ComputeHash();

            SetChangeWave(ChangeWaves.HighestWave.ToString(), env);
            string secondHash = new ServerNodeHandshake(options).ComputeHash();

            secondHash.ShouldNotBe(firstHash);
        }

        /// <summary>
        /// The CLR2 task host computes its handshake with its own copy of this code in MSBuildTaskHost.exe,
        /// which knows nothing about change waves, so it must be left out.
        /// </summary>
        [Fact]
        public void Clr2TaskHostHandshakeIgnoresChangeWave()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            HandshakeOptions options = HandshakeOptions.CLR2 | HandshakeOptions.TaskHost;

            string firstKey = GetKeyForChangeWave(ChangeWaves.Wave17_12.ToString(), env, options);
            string secondKey = GetKeyForChangeWave(ChangeWaves.HighestWave.ToString(), env, options);

            secondKey.ShouldBe(firstKey);
        }
    }
}
