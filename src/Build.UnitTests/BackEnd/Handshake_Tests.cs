// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
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

        private static string GetKeyForChangeWave(string? wave, TestEnvironment env, Func<Handshake>? createHandshake = null)
        {
            SetChangeWave(wave, env);

            Handshake handshake = createHandshake is null ? CommunicationsUtilities.GetWorkerNodeHandshake() : createHandshake();
            return handshake.GetKey();
        }

        [Fact]
        public void DifferentChangeWavesProduceDifferentHandshakes()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            string firstKey = GetKeyForChangeWave(ChangeWaves.LowestWave.ToString(), env);
            string secondKey = GetKeyForChangeWave(ChangeWaves.HighestWave.ToString(), env);

            secondKey.ShouldNotBe(firstKey);
        }

        [Fact]
        public void SameChangeWaveProducesTheSameHandshake()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            string firstKey = GetKeyForChangeWave(ChangeWaves.LowestWave.ToString(), env);
            string secondKey = GetKeyForChangeWave(ChangeWaves.LowestWave.ToString(), env);

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

            string unsetKey = GetKeyForChangeWave(null, env);
            string equivalentKey = GetKeyForChangeWave(wave, env);

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

            SetChangeWave(ChangeWaves.LowestWave.ToString(), env);
            string firstHash = CommunicationsUtilities.GetServerNodeHandshake().ComputeHash();

            SetChangeWave(ChangeWaves.HighestWave.ToString(), env);
            string secondHash = CommunicationsUtilities.GetServerNodeHandshake().ComputeHash();

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
        [InlineData(XMakeAttributes.MSBuildRuntimeValues.clr2, (int)TaskHostHandshakeOptions.None)]
        [InlineData(XMakeAttributes.MSBuildRuntimeValues.net, (int)TaskHostHandshakeOptions.None)]
        [InlineData(XMakeAttributes.MSBuildRuntimeValues.net, (int)TaskHostHandshakeOptions.X64)]
        [InlineData(XMakeAttributes.MSBuildRuntimeValues.net, (int)TaskHostHandshakeOptions.SidecarTaskHost)]
        [InlineData(XMakeAttributes.MSBuildRuntimeValues.clr4, (int)TaskHostHandshakeOptions.None)]
        public void TaskHostHandshakeIgnoresChangeWave(string runtime, int taskHostOptions)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            var options = (TaskHostHandshakeOptions)taskHostOptions;
            Func<Handshake> createHandshake = runtime switch
            {
                XMakeAttributes.MSBuildRuntimeValues.clr2 => () => new Clr2TaskHostHandshake(options, BuildEnvironmentHelper.Instance.MSBuildToolsDirectoryRoot),
                XMakeAttributes.MSBuildRuntimeValues.net => () => new NetTaskHostHandshake(options),
                XMakeAttributes.MSBuildRuntimeValues.clr4 => () => new FrameworkTaskHostHandshake(options),
                _ => throw new ArgumentOutOfRangeException(nameof(runtime)),
            };

            string firstKey = GetKeyForChangeWave(ChangeWaves.LowestWave.ToString(), env, createHandshake);
            string secondKey = GetKeyForChangeWave(ChangeWaves.HighestWave.ToString(), env, createHandshake);

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

            string unsetKey = GetKeyForChangeWave(null, env, () => CommunicationsUtilities.GetCurrentNetTaskHostHandshake());
            string waveSetKey = GetKeyForChangeWave(ChangeWaves.LowestWave.ToString(), env, () => CommunicationsUtilities.GetCurrentNetTaskHostHandshake());

            waveSetKey.ShouldBe(unsetKey);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task TypedConnectionsPreserveWireIdentityAndPacketNegotiation(bool negotiateVersion)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            env.SetEnvironmentVariable("MSBUILDNODEHANDSHAKESALT", "typed-handshake-test");
            SetChangeWave(ChangeWaves.LowestWave.ToString(), env);

            const string taskHostDirectory = @"C:\external-msbuild";
            Version taskHostVersion = new(18, 1, 2, 3);
            Version localVersion = new(typeof(IBuildEngine).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()!.Version);
            using Process process = Process.GetCurrentProcess();
            int sessionId = NativeMethods.IsWindows ? process.SessionId : 0;
            string toolsDirectory = BuildEnvironmentHelper.Instance.MSBuildToolsDirectoryRoot;
            int taskSalt = CommunicationsUtilities.GetHashCode("typed-handshake-test" + taskHostDirectory);
            int nodeSalt = CommunicationsUtilities.GetHashCode("typed-handshake-test" + toolsDirectory + ChangeWaves.DisabledWave);
            WorkerNodeHandshake workerHandshake = CommunicationsUtilities.GetWorkerNodeHandshake(nodeReuse: true, lowPriority: true);
            HandshakeOptions workerOptions = workerHandshake.HandshakeOptions;

            await VerifyConnection(workerHandshake, workerOptions, nodeSalt, localVersion, sessionId);
            await VerifyConnection(CommunicationsUtilities.GetServerNodeHandshake(nodeReuse: true, lowPriority: true), workerOptions, nodeSalt, localVersion, 0);
            TaskHostHandshake netHost = new NetTaskHostHandshake(TaskHostHandshakeOptions.X64, taskHostDirectory);
            await VerifyConnection(netHost, HandshakeOptions.TaskHost | HandshakeOptions.NET | HandshakeOptions.X64, taskSalt, new Version(99, 99, 99, 99), sessionId);
            await VerifyConnection(new FrameworkTaskHostHandshake(TaskHostHandshakeOptions.X64, taskHostDirectory, taskHostVersion),
                HandshakeOptions.TaskHost | HandshakeOptions.X64, taskSalt, taskHostVersion, sessionId);
            await VerifyConnection(new Clr2TaskHostHandshake(TaskHostHandshakeOptions.X64, taskHostDirectory, taskHostVersion),
                HandshakeOptions.TaskHost | HandshakeOptions.CLR2 | HandshakeOptions.X64, taskSalt, taskHostVersion, sessionId);

            async Task VerifyConnection<THandshake>(THandshake handshake, HandshakeOptions options, int salt, Version version, int expectedSessionId)
                where THandshake : INodeHandshake
            {
                string pipeName = $"msbuild-typed-handshake-{Guid.NewGuid():N}";
                using NamedPipeServerStream server = new(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                using NamedPipeClientStream client = new(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                Task peer = Task.Run(async () =>
                {
                    using IDisposable peerLifetime = server;
                    await server.WaitForConnectionAsync();
                    int[] expected = [
                        (int)options | (CommunicationsUtilities.handshakeVersion << 24),
                        salt, version.Major, version.Minor, version.Build, version.Revision, expectedSessionId];
                    foreach (int component in expected)
                    {
                        ReadInt().ShouldBe(CommunicationsUtilities.AvoidEndOfHandshakeSignal(component));
                    }

                    ReadInt().ShouldBe(-0x2a2a2a2a);
                    if (negotiateVersion)
                    {
                        server.WriteIntForHandshake(Handshake.PacketVersionFromChildMarker);
                        server.WriteIntForHandshake(2);
                    }

                    server.WriteEndOfHandshakeSignal();

                    int ReadInt()
                        => (server.ReadByte() << 24) | (server.ReadByte() << 16) | (server.ReadByte() << 8) | server.ReadByte();
                });

                bool connected = NodeConnection<THandshake>.TryConnectToPipeStream(client, pipeName, handshake, 10_000, out HandshakeResult result);
                await peer;
                connected.ShouldBeTrue();
                result.NegotiatedPacketVersion.ShouldBe((byte)(negotiateVersion ? 2 : 1));
            }
        }

#if !NETFRAMEWORK
        /// <summary>
        /// A .NET Framework (CLR4) task host launched from .NET MSBuild comes from a Visual Studio installation,
        /// so the handshake has to use that installation's tools directory and file version, not this process's.
        /// </summary>
        [Fact]
        public void Clr4TaskHostHandshakeUsesExplicitToolsDirectoryAndFileVersion()
        {
            TaskHostHandshakeOptions options = TaskHostHandshakeOptions.X64;
            const string toolsDirectory = @"C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin";

            FrameworkTaskHostHandshake handshake = new(options, toolsDirectory, new Version(18, 1, 2, 3));
            HandshakeComponents components = handshake.RetrieveHandshakeComponents();

            components.FileVersionMajor.ShouldBe(18);
            components.FileVersionMinor.ShouldBe(1);
            components.FileVersionBuild.ShouldBe(2);
            components.FileVersionPrivate.ShouldBe(3);

            new FrameworkTaskHostHandshake(options, toolsDirectory, new Version(18, 1, 2, 3)).GetKey().ShouldBe(handshake.GetKey());
            new FrameworkTaskHostHandshake(options, toolsDirectory + "2", new Version(18, 1, 2, 3)).RetrieveHandshakeComponents().Salt.ShouldNotBe(components.Salt);
            new FrameworkTaskHostHandshake(options).RetrieveHandshakeComponents().Salt.ShouldNotBe(components.Salt);
        }

        [Fact]
        public void Clr2TaskHostHandshakeUsesExecutableDirectoryAndFileVersion()
        {
            TaskHostHandshakeOptions options = TaskHostHandshakeOptions.X64;
            const string directory = @"C:\Visual Studio\MSBuild\Current\Bin\amd64";
            Version version = new(18, 1, 2, 3);

            HandshakeComponents components = new Clr2TaskHostHandshake(options, directory, version).RetrieveHandshakeComponents();
            components.FileVersionMajor.ShouldBe(18);
            components.FileVersionMinor.ShouldBe(1);
            components.FileVersionBuild.ShouldBe(2);
            components.FileVersionPrivate.ShouldBe(3);
            new Clr2TaskHostHandshake(options, directory + "2", version).RetrieveHandshakeComponents().Salt.ShouldNotBe(components.Salt);
            new Clr2TaskHostHandshake(options, directory, new Version(18, 1, 2, 4)).RetrieveHandshakeComponents().FileVersionPrivate.ShouldNotBe(components.FileVersionPrivate);
        }
#endif
    }
}
