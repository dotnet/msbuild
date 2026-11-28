// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Tests for NodeProviderOutOfProc.
    /// </summary>
    public class NodeProviderOutOfProc_Tests
    {
        private readonly ITestOutputHelper _output;

        public NodeProviderOutOfProc_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// Simulates a worker node that cannot be reached, for example because a sandbox blocks the pipe.
        /// The build must fail with an error that tells why, instead of failing without any error.
        /// </summary>
        [Fact]
        public void CreateNodes_WhenConnectionFails_LogsErrorWithReason()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            string projectFile = env.CreateFile("empty.proj", """
                <Project>
                    <Target Name="Build" />
                </Project>
                """).Path;

            ExitingNodeLauncher launcher = new();
            using BuildManager buildManager = new();
            IBuildComponentHost host = buildManager;
            host.RegisterFactory(BuildComponentType.NodeLauncher, _ => launcher);
            host.RegisterFactory(BuildComponentType.OutOfProcNodeProvider, _ => new QuickNodeProviderOutOfProc());
            MockLogger logger = new(_output);

            BuildResult result = buildManager.Build(
                new BuildParameters
                {
                    DisableInProcNode = true,
                    MaxNodeCount = 1,
                    EnableNodeReuse = false,
                    Loggers = [logger],
                },
                new BuildRequestData(projectFile, new Dictionary<string, string>(), null, ["Build"], null));

            result.OverallResult.ShouldBe(BuildResultCode.Failure);
            result.Exception.ShouldBeOfType<BuildAbortedException>();
            launcher.LaunchCount.ShouldBe(QuickNodeProviderOutOfProc.Retries);

            BuildErrorEventArgs error = logger.Errors.ShouldHaveSingleItem();
            error.Code.ShouldBe("MSB4218");
            error.Message.ShouldContain("-maxCpuCount:1");

            // The connection error text comes from the OS, so check the text around it.
            string reason = ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword(
                "NodeConnectionFailedProcessExited",
                launcher.LastProcessId,
                NamedPipeUtil.GetPlatformSpecificPipeName(launcher.LastProcessId),
                "\0",
                ExitingNodeLauncher.ExitCode);
            foreach (string part in reason.Split('\0'))
            {
                error.Message.ShouldContain(part);
            }
        }

        /// <summary>
        /// When only some of the requested nodes connect, the build stops too.
        /// The error must still tell why the other nodes did not connect.
        /// </summary>
        [Fact]
        public void CreateNodes_WhenSomeConnectionsFail_LogsErrorWithReason()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            using PartlyWorkingNodeLauncher launcher = new(workingNodeId: 2);
            ConcurrentQueue<string> log = new();
            TestComponentHost host = new(
                new BuildParameters { MaxNodeCount = 2, EnableNodeReuse = false },
                new MockLoggingService(log.Enqueue),
                launcher);

            // The working node must finish the handshake within the timeout, so do not make it too short.
            QuickNodeProviderOutOfProc provider = new(retries: 1, timeout: 3000);
            provider.InitializeComponent(host);

            IList<NodeInfo> nodes = provider.CreateNodes(nextNodeId: 2, launcher, CreateNodeConfiguration, numberOfNodesToCreate: 2);

            nodes.ShouldHaveSingleItem().NodeId.ShouldBe(2);
            log.ShouldContain("CouldNotConnectToMSBuildExe");
            string failedPipeName = NamedPipeUtil.GetPlatformSpecificPipeName(launcher.FailedProcessId);
            log.ShouldContain(line => line != null && line.Contains(failedPipeName));
        }

        private static NodeConfiguration CreateNodeConfiguration(NodeInfo nodeInfo) => new(
            nodeInfo.NodeId,
            new BuildParameters(),
            [],
#if FEATURE_APPDOMAIN
            null,
#endif
            new LoggingNodeConfiguration());

        private sealed class QuickNodeProviderOutOfProc : NodeProviderOutOfProc
        {
            public const int Retries = 2;

            public QuickNodeProviderOutOfProc(int retries = Retries, int timeout = 100)
            {
                NodeCreationRetries = retries;
                TimeoutForNewNodeCreation = timeout;
            }
        }

        private sealed class TestComponentHost(BuildParameters buildParameters, ILoggingService loggingService, IBuildComponent nodeLauncher) : IBuildComponentHost
        {
            public string Name => nameof(TestComponentHost);

            public BuildParameters BuildParameters => buildParameters;

            public LegacyThreadingData LegacyThreadingData => null;

            public ILoggingService LoggingService => loggingService;

            public void RegisterFactory(BuildComponentType factoryType, BuildComponentFactoryDelegate factory) => throw new NotSupportedException();

            public IBuildComponent GetComponent(BuildComponentType type) => type == BuildComponentType.NodeLauncher
                ? nodeLauncher
                : throw new ArgumentException($"Unexpected component type {type}.", nameof(type));

            public TComponent GetComponent<TComponent>(BuildComponentType type)
                where TComponent : IBuildComponent
                => (TComponent)GetComponent(type);
        }

        /// <summary>
        /// For one node id, starts a process and listens on its node pipe in this process, so the connection works.
        /// For other node ids, starts a process that exits at once, so the connection fails.
        /// Also acts as a packet factory that ignores all packets.
        /// </summary>
        private sealed class PartlyWorkingNodeLauncher(int workingNodeId) : INodeLauncher, IBuildComponent, INodePacketFactory, IDisposable
        {
            private readonly ConcurrentQueue<Process> _processes = new();
            private ServerNodeEndpointOutOfProc _endpoint;
            private int _failedProcessId;

            public int FailedProcessId => Volatile.Read(ref _failedProcessId);

            public Process Start(NodeLaunchData launchData, int nodeId)
            {
                bool works = nodeId == workingNodeId;
                ProcessStartInfo startInfo = (NativeMethodsShared.IsWindows, works) switch
                {
                    (true, true) => new ProcessStartInfo("cmd.exe", "/c ping -n 60 127.0.0.1 > nul"),
                    (true, false) => new ProcessStartInfo("cmd.exe", "/c exit 1"),
                    (false, true) => new ProcessStartInfo("/bin/sh", "-c \"sleep 60\""),
                    (false, false) => new ProcessStartInfo("/bin/sh", "-c \"exit 1\""),
                };
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;

                Process process = Process.Start(startInfo);
                _processes.Enqueue(process);

                if (works)
                {
                    _endpoint = new ServerNodeEndpointOutOfProc(
                        NamedPipeUtil.GetPlatformSpecificPipeName(process.Id),
                        NodeProviderOutOfProc.GetHandshake(enableNodeReuse: false, enableLowPriority: false));
                    _endpoint.Listen(this);
                }
                else
                {
                    process.WaitForExit();
                    Volatile.Write(ref _failedProcessId, process.Id);
                }

                return process;
            }

            public void Dispose()
            {
                _endpoint?.Disconnect();
                while (_processes.TryDequeue(out Process process))
                {
                    if (!process.HasExited)
                    {
                        process.KillTree(timeoutMilliseconds: 5000);
                    }

                    process.Dispose();
                }
            }

            public void InitializeComponent(IBuildComponentHost host)
            {
            }

            public void ShutdownComponent()
            {
            }

            public void RegisterPacketHandler(NodePacketType packetType, NodePacketFactoryMethod factory, INodePacketHandler handler)
            {
            }

            public void UnregisterPacketHandler(NodePacketType packetType)
            {
            }

            public void DeserializeAndRoutePacket(int nodeId, NodePacketType packetType, ITranslator translator)
            {
            }

            public INodePacket DeserializePacket(NodePacketType packetType, ITranslator translator) => null;

            public void RoutePacket(int nodeId, INodePacket packet)
            {
            }
        }

        /// <summary>
        /// Starts a process that exits at once, so no node pipe is ever created.
        /// </summary>
        private sealed class ExitingNodeLauncher : INodeLauncher, IBuildComponent
        {
            public const int ExitCode = 42;

            public int LaunchCount { get; private set; }

            public int LastProcessId { get; private set; }

            public void InitializeComponent(IBuildComponentHost host)
            {
            }

            public void ShutdownComponent()
            {
            }

            public Process Start(NodeLaunchData launchData, int nodeId)
            {
                ProcessStartInfo startInfo = NativeMethodsShared.IsWindows
                    ? new ProcessStartInfo("cmd.exe", $"/c exit {ExitCode}")
                    : new ProcessStartInfo("/bin/sh", $"-c \"exit {ExitCode}\"");
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;

                Process process = Process.Start(startInfo);
                process.WaitForExit();
                LaunchCount++;
                LastProcessId = process.Id;
                return process;
            }
        }
        /// <summary>
        /// Test helper class to expose protected methods for testing.
        /// Uses configurable overrides for testing.
        /// </summary>
        private sealed class TestableNodeProviderOutOfProcBase : NodeProviderOutOfProcBase
        {
            private readonly int _systemWideNodeCount;
            private readonly int? _thresholdOverride;

            public TestableNodeProviderOutOfProcBase(int systemWideNodeCount, int? thresholdOverride = null)
            {
                _systemWideNodeCount = systemWideNodeCount;
                _thresholdOverride = thresholdOverride;
            }

            protected override int GetNodeReuseThreshold()
            {
                // If threshold is overridden, use it; otherwise use base implementation
                return _thresholdOverride ?? base.GetNodeReuseThreshold();
            }

            protected override int CountSystemWideActiveNodes()
            {
                return _systemWideNodeCount;
            }

            public bool[] TestDetermineNodesForReuse(int nodeCount, bool enableReuse)
            {
                return DetermineNodesForReuse(nodeCount, enableReuse);
            }

            public int TestGetNodeReuseThreshold()
            {
                return GetNodeReuseThreshold();
            }
        }

        [Fact]
        public void DetermineNodesForReuse_WhenReuseDisabled_AllNodesShouldTerminate()
        {
            var provider = new TestableNodeProviderOutOfProcBase(systemWideNodeCount: 10, thresholdOverride: 4);
            
            bool[] result = provider.TestDetermineNodesForReuse(nodeCount: 3, enableReuse: false);
            
            result.Length.ShouldBe(3);
            result.ShouldAllBe(shouldReuse => shouldReuse == false);
        }

        [Fact]
        public void DetermineNodesForReuse_WhenThresholdIsZero_AllNodesShouldTerminate()
        {
            var provider = new TestableNodeProviderOutOfProcBase(systemWideNodeCount: 10, thresholdOverride: 0);
            
            bool[] result = provider.TestDetermineNodesForReuse(nodeCount: 3, enableReuse: true);
            
            result.Length.ShouldBe(3);
            result.ShouldAllBe(shouldReuse => shouldReuse == false);
        }

        [Fact]
        public void DetermineNodesForReuse_WhenUnderThreshold_AllNodesShouldBeReused()
        {
            // System has 3 nodes total, threshold is 4, so we're under the limit
            var provider = new TestableNodeProviderOutOfProcBase(systemWideNodeCount: 3, thresholdOverride: 4);
            
            bool[] result = provider.TestDetermineNodesForReuse(nodeCount: 3, enableReuse: true);
            
            result.Length.ShouldBe(3);
            result.ShouldAllBe(shouldReuse => shouldReuse == true);
        }

        [Fact]
        public void DetermineNodesForReuse_WhenAtThreshold_AllNodesShouldBeReused()
        {
            // System has 4 nodes total, threshold is 4, so we're at the limit
            var provider = new TestableNodeProviderOutOfProcBase(systemWideNodeCount: 4, thresholdOverride: 4);
            
            bool[] result = provider.TestDetermineNodesForReuse(nodeCount: 4, enableReuse: true);
            
            result.Length.ShouldBe(4);
            result.ShouldAllBe(shouldReuse => shouldReuse == true);
        }

        [Fact]
        public void DetermineNodesForReuse_WhenOverThreshold_ExcessNodesShouldTerminate()
        {
            // System has 10 nodes total, threshold is 4
            // This instance has 3 nodes
            // We should keep 0 nodes from this instance (since 10 - 3 = 7, which is already > threshold)
            var provider = new TestableNodeProviderOutOfProcBase(systemWideNodeCount: 10, thresholdOverride: 4);
            
            bool[] result = provider.TestDetermineNodesForReuse(nodeCount: 3, enableReuse: true);
            
            result.Length.ShouldBe(3);
            result.ShouldAllBe(shouldReuse => shouldReuse == false);
        }

        [Fact]
        public void DetermineNodesForReuse_WhenSlightlyOverThreshold_SomeNodesShouldBeReused()
        {
            // System has 6 nodes total, threshold is 4
            // This instance has 3 nodes
            // Other instances have 6 - 3 = 3 nodes
            // We need to reduce by 2 nodes to reach threshold
            // So we should keep 1 node from this instance
            var provider = new TestableNodeProviderOutOfProcBase(systemWideNodeCount: 6, thresholdOverride: 4);
            
            bool[] result = provider.TestDetermineNodesForReuse(nodeCount: 3, enableReuse: true);
            
            result.Length.ShouldBe(3);
            // First node should be reused, others should terminate
            result[0].ShouldBeTrue();
            result[1].ShouldBeFalse();
            result[2].ShouldBeFalse();
        }

        [Fact]
        public void DetermineNodesForReuse_WithSingleNode_BehavesCorrectly()
        {
            // System has 5 nodes total, threshold is 4
            // This instance has 1 node
            // We're over threshold, but only by 1
            // We should terminate this node since others already meet threshold
            var provider = new TestableNodeProviderOutOfProcBase(systemWideNodeCount: 5, thresholdOverride: 4);
            
            bool[] result = provider.TestDetermineNodesForReuse(nodeCount: 1, enableReuse: true);
            
            result.Length.ShouldBe(1);
            result[0].ShouldBeFalse();
        }
    }
}
