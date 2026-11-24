// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
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
#if NET
        private const HandshakeOptions BaseNet = HandshakeOptions.TaskHost | HandshakeOptions.NET;
#endif

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
        [InlineData(XMakeAttributes.MSBuildRuntimeValues.clr2, (int)TaskHostArchitecture.Unspecified, false)]
        [InlineData(XMakeAttributes.MSBuildRuntimeValues.net, (int)TaskHostArchitecture.Unspecified, false)]
        [InlineData(XMakeAttributes.MSBuildRuntimeValues.net, (int)TaskHostArchitecture.X64, false)]
        [InlineData(XMakeAttributes.MSBuildRuntimeValues.net, (int)TaskHostArchitecture.Unspecified, true)]
        [InlineData(XMakeAttributes.MSBuildRuntimeValues.clr4, (int)TaskHostArchitecture.Unspecified, false)]
        public void TaskHostHandshakeIgnoresChangeWave(string runtime, int architecture, bool sidecar)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            TaskHostHandshakeOptions options = new((TaskHostArchitecture)architecture, SidecarTaskHost: sidecar);
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

        [Fact]
        public void TaskHostRequestAndHandshakeExposeTypedOptions()
        {
            TaskHostHandshakeOptions options = new(TaskHostArchitecture.Arm64, NodeReuse: true);
            string toolsDirectory = BuildEnvironmentHelper.Instance.MSBuildToolsDirectoryRoot;

            TaskHostRequest netRequest = new NetTaskHostRequest(options);
            TaskHostRequest frameworkRequest = new FrameworkTaskHostRequest(options);
            TaskHostRequest clr2Request = new Clr2TaskHostRequest(options);

            TaskHostHandshake netHandshake = new NetTaskHostHandshake(options);
            TaskHostHandshake frameworkHandshake = new FrameworkTaskHostHandshake(options);
            TaskHostHandshake clr2Handshake = new Clr2TaskHostHandshake(options, toolsDirectory);

            AssertRequestOptions(netRequest, isNet: true, isClr2: false);
            AssertRequestOptions(frameworkRequest, isNet: false, isClr2: false);
            AssertRequestOptions(clr2Request, isNet: false, isClr2: true);
            AssertHandshakeOptions(netHandshake, isNet: true, isClr2: false);
            AssertHandshakeOptions(frameworkHandshake, isNet: false, isClr2: false);
            AssertHandshakeOptions(clr2Handshake, isNet: false, isClr2: true);

            void AssertRequestOptions(TaskHostRequest request, bool isNet, bool isClr2)
            {
                request.Options.ShouldBe(options);
                request.Architecture.ShouldBe(TaskHostArchitecture.Arm64);
                request.NodeReuse.ShouldBeTrue();
                request.IsNet.ShouldBe(isNet);
                request.IsClr2.ShouldBe(isClr2);
            }

            void AssertHandshakeOptions(TaskHostHandshake handshake, bool isNet, bool isClr2)
            {
                handshake.Options.ShouldBe(options);
                handshake.Architecture.ShouldBe(TaskHostArchitecture.Arm64);
                handshake.NodeReuse.ShouldBeTrue();
                handshake.IsNet.ShouldBe(isNet);
                handshake.IsClr2.ShouldBe(isClr2);
            }
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
            TaskHostHandshakeOptions x64Options = new(TaskHostArchitecture.X64);
            TaskHostHandshake netHost = new NetTaskHostHandshake(x64Options, taskHostDirectory);
            await VerifyConnection(netHost, HandshakeOptions.TaskHost | HandshakeOptions.NET | HandshakeOptions.X64, taskSalt, new Version(99, 99, 99, 99), sessionId);
            await VerifyConnection(new FrameworkTaskHostHandshake(x64Options, taskHostDirectory, taskHostVersion),
                HandshakeOptions.TaskHost | HandshakeOptions.X64, taskSalt, taskHostVersion, sessionId);
            await VerifyConnection(new Clr2TaskHostHandshake(x64Options, taskHostDirectory, taskHostVersion),
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

                NodeConnectionResult result = NodeConnection.TryConnectToPipeStream(client, pipeName, handshake, 10_000);
                await peer;
                NodeConnectionResult.Connected connected = result.Value.ShouldBeOfType<NodeConnectionResult.Connected>();
                connected.Stream.ShouldBeSameAs(client);
                connected.NegotiatedPacketVersion.ShouldBe((byte)(negotiateVersion ? 2 : 1));
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ProcessConnection_TransfersOnlySuccessfulStreams(bool accept)
        {
            using Process process = Process.GetCurrentProcess();
            string pipeName = NamedPipeUtil.GetPlatformSpecificPipeName(process.Id);
            using NamedPipeServerStream server = new(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            Task<int> peer = Task.Run(async () =>
            {
                await server.WaitForConnectionAsync();
                ReadHandshake(server);
                if (accept)
                {
                    server.WriteIntForHandshake(Handshake.PacketVersionFromChildMarker);
                    server.WriteIntForHandshake(2);
                    server.WriteEndOfHandshakeSignal();
                }
                else
                {
                    server.WriteIntForHandshake(42);
                }

                return server.ReadByte();
            });

            NodeConnectionResult result = NodeConnection.TryConnectToProcess(
                process.Id, 10_000, CommunicationsUtilities.GetWorkerNodeHandshake());
            if (accept)
            {
                NodeConnectionResult.Connected connected = result.Value.ShouldBeOfType<NodeConnectionResult.Connected>();
                using Stream connectedStream = connected.Stream;
                connected.NegotiatedPacketVersion.ShouldBe((byte)2);
                connectedStream.ShouldBeOfType<NamedPipeClientStream>().IsConnected.ShouldBeTrue();
            }
            else
            {
                NodeConnectionResult.Failed failed = result.Value.ShouldBeOfType<NodeConnectionResult.Failed>();
                failed.Status.ShouldBe(HandshakeStatus.VersionMismatch);
                failed.ErrorMessage.ShouldContain("42");
            }

            Task completed = await Task.WhenAny(peer, Task.Delay(10_000));
            completed.ShouldBeSameAs(peer);
            (await peer).ShouldBe(-1);
        }

        [Fact]
        public async Task PipeConnection_FailureLeavesTheCallerOwnedStreamOpen()
        {
            string pipeName = $"msbuild-failed-handshake-{Guid.NewGuid():N}";
            using NamedPipeServerStream server = new(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            using NamedPipeClientStream client = new(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            Task peer = Task.Run(async () =>
            {
                await server.WaitForConnectionAsync();
                ReadHandshake(server);
                server.WriteIntForHandshake(42);
            });

            NodeConnectionResult result = NodeConnection.TryConnectToPipeStream(
                client, pipeName, CommunicationsUtilities.GetWorkerNodeHandshake(), 10_000);
            await peer;

            NodeConnectionResult.Failed failed = result.Value.ShouldBeOfType<NodeConnectionResult.Failed>();
            failed.Status.ShouldBe(HandshakeStatus.VersionMismatch);
            failed.ErrorMessage.ShouldContain("42");
            client.SafePipeHandle.IsClosed.ShouldBeFalse();
        }

        [Fact]
        public void ProcessConnection_TransportFailureKeepsTheExistingDiagnostic()
        {
            NodeConnectionResult result = NodeConnection.TryConnectToProcess(
                -EnvironmentUtilities.CurrentProcessId, 0, CommunicationsUtilities.GetWorkerNodeHandshake());

            NodeConnectionResult.Failed failed = result.Value.ShouldBeOfType<NodeConnectionResult.Failed>();
            failed.Status.ShouldBe(HandshakeStatus.Undefined);
            failed.ErrorMessage.ShouldBe("Check the COMM traces to diagnose the issue with communication.");
        }

#if NET
        [Fact]
        public void NoArchBitWorkerNode_X64TaskHost_IsTolerated()
        {
            Handshake.IsAllowedBitnessMismatch(
                expectedOptions: (int)(BaseNet | HandshakeOptions.X64),
                receivedOptions: (int)BaseNet).ShouldBeTrue();
        }

        [Fact]
        public void NoArchBitWorkerNode_Arm64TaskHost_IsTolerated()
        {
            Handshake.IsAllowedBitnessMismatch(
                expectedOptions: (int)(BaseNet | HandshakeOptions.Arm64),
                receivedOptions: (int)BaseNet).ShouldBeTrue();
        }

        [Fact]
        public void X64WorkerNode_X64TaskHost_NotConsideredMismatch()
        {
            // Equal handshakes never hit IsAllowedBitnessMismatch in production; verify it
            // still returns false so the tolerance is scoped to the no-arch-bit worker node only.
            Handshake.IsAllowedBitnessMismatch(
                expectedOptions: (int)(BaseNet | HandshakeOptions.X64),
                receivedOptions: (int)(BaseNet | HandshakeOptions.X64)).ShouldBeFalse();
        }

        [Fact]
        public void X64WorkerNode_Arm64TaskHost_NotTolerated()
        {
            // True architecture mismatch (worker node sent X64, TaskHost node expects Arm64) is rejected.
            Handshake.IsAllowedBitnessMismatch(
                expectedOptions: (int)(BaseNet | HandshakeOptions.Arm64),
                receivedOptions: (int)(BaseNet | HandshakeOptions.X64)).ShouldBeFalse();
        }

        [Fact]
        public void Arm64WorkerNode_X64TaskHost_NotTolerated()
        {
            Handshake.IsAllowedBitnessMismatch(
                expectedOptions: (int)(BaseNet | HandshakeOptions.X64),
                receivedOptions: (int)(BaseNet | HandshakeOptions.Arm64)).ShouldBeFalse();
        }

        [Fact]
        public void NoArchBitWorkerNode_NoArchBitTaskHost_NotTolerated()
        {
            // The tolerance is scoped to x64/arm64 TaskHost nodes; an x86-equivalent (no arch bit)
            // TaskHost node must not silently accept any handshake. Such a parent and TaskHost node
            // agree exactly anyway, so IsHandshakePartValid accepts them before reaching here.
            Handshake.IsAllowedBitnessMismatch(
                expectedOptions: (int)BaseNet,
                receivedOptions: (int)BaseNet).ShouldBeFalse();
        }

        [Fact]
        public void X64WorkerNode_X86TaskHost_NotTolerated()
        {
            // x86 is encoded as the absence of X64/Arm64 rather than a flag of its own, so an
            // x86 TaskHost node expects no architecture bit. A parent that positively declared x64
            // is a genuine bitness mismatch and must be rejected: the tolerance only ever widens
            // what an x64/arm64 TaskHost node accepts, never what an x86 one does.
            Handshake.IsAllowedBitnessMismatch(
                expectedOptions: (int)BaseNet,
                receivedOptions: (int)(BaseNet | HandshakeOptions.X64)).ShouldBeFalse();
        }

        [Fact]
        public void Arm64WorkerNode_X86TaskHost_NotTolerated()
        {
            Handshake.IsAllowedBitnessMismatch(
                expectedOptions: (int)BaseNet,
                receivedOptions: (int)(BaseNet | HandshakeOptions.Arm64)).ShouldBeFalse();
        }

        [Fact]
        public void NonSidecarParent_SidecarTaskHost_NotTolerated()
        {
            // NodeReuse is what separates a long-lived sidecar TaskHost from one that exits with
            // the build. A parent that did not ask for a sidecar must not be admitted to one just
            // because it omitted its architecture bit, or it would take the sidecar over and later
            // return it to the global reconnectable pool.
            Handshake.IsAllowedBitnessMismatch(
                expectedOptions: (int)(BaseNet | HandshakeOptions.NodeReuse | HandshakeOptions.X64),
                receivedOptions: (int)BaseNet).ShouldBeFalse();
        }

        [Fact]
        public void SidecarParent_NonSidecarTaskHost_NotTolerated()
        {
            Handshake.IsAllowedBitnessMismatch(
                expectedOptions: (int)(BaseNet | HandshakeOptions.X64),
                receivedOptions: (int)(BaseNet | HandshakeOptions.NodeReuse)).ShouldBeFalse();
        }

        [Fact]
        public void SidecarParent_SidecarTaskHost_ArchitectureStillTolerated()
        {
            // Scoping the tolerance to architecture must not break the case it exists for.
            Handshake.IsAllowedBitnessMismatch(
                expectedOptions: (int)(BaseNet | HandshakeOptions.NodeReuse | HandshakeOptions.X64),
                receivedOptions: (int)(BaseNet | HandshakeOptions.NodeReuse)).ShouldBeTrue();
        }

        [Fact]
        public void DifferingLowPriority_NotTolerated()
        {
            Handshake.IsAllowedBitnessMismatch(
                expectedOptions: (int)(BaseNet | HandshakeOptions.LowPriority | HandshakeOptions.X64),
                receivedOptions: (int)BaseNet).ShouldBeFalse();
        }

        [Fact]
        public void DifferingAdministrator_NotTolerated()
        {
            Handshake.IsAllowedBitnessMismatch(
                expectedOptions: (int)(BaseNet | HandshakeOptions.Administrator | HandshakeOptions.X64),
                receivedOptions: (int)BaseNet).ShouldBeFalse();
        }

        [Fact]
        public void HandshakeVersionInUpperByte_IsIgnored()
        {
            // The upper byte carries the handshake version and must not affect the comparison.
            const int VersionBits = 13 << 24;

            Handshake.IsAllowedBitnessMismatch(
                expectedOptions: (int)(BaseNet | HandshakeOptions.NodeReuse | HandshakeOptions.X64) | VersionBits,
                receivedOptions: (int)(BaseNet | HandshakeOptions.NodeReuse)).ShouldBeTrue();
        }
#endif

        [Fact]
        public void ConnectionResult_RejectsInvalidCases()
        {
            Should.Throw<ArgumentNullException>(() => new NodeConnectionResult.Connected(null!, 1));
            Should.Throw<InternalErrorException>(() => new NodeConnectionResult.Failed(HandshakeStatus.Success, "unexpected"));
        }

        private static void ReadHandshake(NamedPipeServerStream server)
        {
            for (int i = 0; i < 8 * sizeof(int); i++)
            {
                server.ReadByte().ShouldNotBe(-1);
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
            TaskHostHandshakeOptions options = new(TaskHostArchitecture.X64);
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
            TaskHostHandshakeOptions options = new(TaskHostArchitecture.X64);
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
